using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace LambdaRemoteDebug
{
    public class LambdaRemoteDebug
    {
        private static readonly Lazy<bool> Initialized = new Lazy<bool>(Initialize);
        private static readonly string Name = Path.GetFileNameWithoutExtension(typeof(LambdaRemoteDebug).Assembly.Location);

        private static string _ip;
        private static int _port;

        /// <summary>
        /// Connects to a remote debug broker and starts debugging if environment variables 
        /// LAMBDA_REMOTE_DEBUG_IP and LAMBDA_REMOTE_DEBUG_PORT are set correctly.
        /// </summary>
        public static void Attach()
        {
            if (Debugger.IsAttached) return;

            if (!Initialized.Value) return;

            var start = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"/tmp/{Name}.dll {_ip} {_port}",
                UseShellExecute = false
            };

            var process = Process.Start(start);
            if (process == null) return;

            while (!Debugger.IsAttached && !process.HasExited) Thread.Sleep(100);
        }

        private static bool Initialize()
        {
            try
            {
                _ip = Environment.GetEnvironmentVariable("LAMBDA_REMOTE_DEBUG_IP");
                var port = Environment.GetEnvironmentVariable("LAMBDA_REMOTE_DEBUG_PORT");

                if (string.IsNullOrWhiteSpace(_ip) || !int.TryParse(port, out _port))
                {
                    Console.WriteLine($"[{Name}] LAMBDA_REMOTE_DEBUG_IP or LAMBDA_REMOTE_DEBUG_PORT not configured correctly.");
                    return false;
                }

                if (!File.Exists("/opt/vsdbg/vsdbg"))
                {
                    Console.WriteLine($"[{Name}] Debugger not found at /opt/vsdbg/vsdbg (are you missing the Lambda Layer?)");
                    return false;
                }

                if (!File.Exists($"/tmp/{Name}.dll"))
                {
                    File.Copy(typeof(LambdaRemoteDebug).Assembly.Location, $"/tmp/{Name}.dll");
                    File.WriteAllText($"/tmp/{Name}.runtimeconfig.json",
                        "{\"runtimeOptions\":{\"tfm\":\"netcoreapp2.1\",\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"2.1.0\"}}}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{Name}] Initialization failure: " + ex);
                return false;
            }
        }
    }
}