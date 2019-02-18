using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LambdaRemoteDebug.Tools
{
    public class Program
    {
        static Program()
        {
            Console.CancelKeyPress += (s, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cancel requested. Stopping.");
                Console.ResetColor();
                Process.GetCurrentProcess().Kill();
            };
        }

        public static void Main(string[] args)
        {
            if (args.Length != 3 || args[0] != "broker")
            {
                ShowUsage();
                return;
            }

            if (!int.TryParse(args[1], out var cport) || !int.TryParse(args[2], out var lport)
                || cport < IPEndPoint.MinPort || cport > IPEndPoint.MaxPort
                || lport < IPEndPoint.MinPort || lport > IPEndPoint.MaxPort)
            {
                Console.WriteLine($"Ports must be between {IPEndPoint.MinPort} and {IPEndPoint.MaxPort}");
                Console.WriteLine();
                ShowUsage();
                return;
            }

            var clients = new TcpListener(IPAddress.Any, cport);
            var relays = new TcpListener(IPAddress.Any, lport);

            while (true)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Waiting for a client on port {cport}...");
                    clients.Start();
                    var client = clients.AcceptTcpClient();
                    clients.Stop();

                    Console.WriteLine($"Waiting for a lambda on port {lport}...");
                    relays.Start();
                    var relay = relays.AcceptTcpClient();
                    relays.Stop();

                    using (client)
                    using (relay)
                    {
                        Console.WriteLine("Copying streams...");
                        Console.ForegroundColor = ConsoleColor.DarkGray;

                        Task.WhenAny(
                            Task.Run(() => Proxy(client.GetStream(), relay.GetStream())),
                            Task.Run(() => Proxy(relay.GetStream(), client.GetStream()))
                        ).Wait();
                    }
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Exception thrown. Restarting.");
                }

                Console.WriteLine();
            }
        }

        private static void Proxy(Stream from, Stream to)
        {
            var fromReader = new StreamReader(from, Encoding.UTF8);
            var toWriter = new StreamWriter(to, Encoding.UTF8);

            while (true)
            {
                var header = fromReader.ReadLine()?.TrimStart();
                fromReader.ReadLine();

                if (!int.TryParse(header?.Split(": ").ElementAtOrDefault(1), out var length)) return;

                var read = Read(fromReader, length, out var buffer);
                if (read != length) return;

                var content = new string(buffer);
                Console.WriteLine(content);
                toWriter.Write($"{header}\r\n\r\n");
                toWriter.Write(buffer, 0, length);
                toWriter.Flush();

                if (!content.Contains("\"type\":\"request\"") || !content.Contains("\"command\":\"disconnect\"")) continue;

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Client wants to disconnect.");
                var fromWriter = new StreamWriter(from, Encoding.UTF8);
                fromWriter.Write("Content-Length: 57\r\n\r\n{\"type\":\"response\",\"command\":\"disconnect\",\"success\":true}");
                toWriter.Flush();
                return;
            }
        }

        private static int Read(TextReader reader, int length, out char[] buffer)
        {
            buffer = new char[length];

            var remaining = length;
            var totalRead = 0;

            while (remaining > 0)
            {
                var read = reader.Read(buffer, totalRead, remaining);
                if (read == 0) break;

                totalRead += read;
                remaining -= read;
            }

            return totalRead;
        }

        private static void ShowUsage()
        {
            var versionString = Assembly.GetEntryAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;

            Console.WriteLine($"lrdbg v{versionString}");
            Console.WriteLine("-------------");
            Console.WriteLine("\nUsage:");
            Console.WriteLine("  lrdbg broker <client port> <lambda port>");
        }
    }
}
