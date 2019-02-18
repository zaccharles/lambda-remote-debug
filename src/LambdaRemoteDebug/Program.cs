using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LambdaRemoteDebug
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                ShowUsage();
                return;
            }

            if (!IPAddress.TryParse(args[0], out var ip))
            {
                Console.WriteLine("IP address is not valid.");
                Console.WriteLine();
                ShowUsage();
                return;
            }

            if (!int.TryParse(args[1], out var port))
            {
                Console.WriteLine($"Port must be between {IPEndPoint.MinPort} and {IPEndPoint.MaxPort}");
                Console.WriteLine();
                ShowUsage();
                return;
            }

            var start = new ProcessStartInfo
            {
                FileName = "/opt/vsdbg/vsdbg",
                Arguments = "--interpreter=vscode",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            try
            {
                using (var client = new TcpClient())
                {
                    Console.WriteLine("Connecting to broker...");
                    client.ConnectAsync(ip, port).Wait(3000);

                    if (!client.Connected)
                    {
                        Console.WriteLine("Couldn't connect.");
                        return;
                    }

                    Console.WriteLine("Starting debugger...");
                    using (var process = Process.Start(start))
                    {
                        if (process == null)
                        {
                            Console.WriteLine("Debugger did not start.");
                            return;
                        }

                        var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
                        var writer = new StreamWriter(client.GetStream(), Encoding.UTF8);

                        Console.WriteLine("Copying streams...");
                        Task.WhenAny(
                            // ReSharper disable AccessToDisposedClosure
                            Task.Run(() => Proxy(reader, process.StandardInput)),
                            Task.Run(() => Proxy(process.StandardOutput, writer))
                        // ReSharper restore AccessToDisposedClosure
                        ).Wait();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception thrown. Stopping.");
                Console.WriteLine(ex.GetType().FullName);
                Console.WriteLine(ex.Message);
            }
        }

        private static void Proxy(TextReader from, TextWriter to)
        {
            while (true)
            {
                var header = from.ReadLine();
                from.ReadLine();

                if (!int.TryParse(header?.Split(": ").ElementAtOrDefault(1), out var length))
                {
                    return;
                }

                var read = Read(from, length, out var buffer);
                if (read != length)
                {
                    return;
                }

                Console.WriteLine(new string(buffer));
                to.Write($"{header}\r\n\r\n");
                to.Write(buffer, 0, length);
                to.Flush();
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
            var assembly = typeof(Program).Assembly;
            var name = Path.GetFileNameWithoutExtension(assembly.Location);

            var versionString = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;

            Console.WriteLine($"{name} v{versionString}");
            Console.WriteLine("-------------");
            Console.WriteLine("\nUsage:");
            Console.WriteLine($"  dotnet {name}.dll <broker ip> <broker port>");
        }
    }
}
