using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VSRAD.DebugServer
{
    static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint lpMode);
        [DllImport("kernel32.dll")]
        static extern uint GetLastError();
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        const uint ENABLE_QUICK_EDIT = 0x0040;
        const int STD_INPUT_HANDLE = -10;

        static void Main(string[] args)
        {
            /* disable Quick Edit cmd feature to prevent server hanging */
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var consoleHandle = GetStdHandle(STD_INPUT_HANDLE);
                if (!GetConsoleMode(consoleHandle, out var consoleMode))
                    Console.WriteLine($"Warning! Cannot get console mode. Error code={GetLastError()}");
                consoleMode &= ~ENABLE_QUICK_EDIT;
                if (!SetConsoleMode(consoleHandle, consoleMode))
                    Console.WriteLine($"Warning! Cannot set console mode. Error code={GetLastError()}");
            }

            if (CliOptions.TryParse(args, out var options))
            {
                var server = new Server(IPAddress.Any, options.Port, options.Verbose);
                Logging.GlobalLogger.ServerStarted(options);
                Task.Run(server.LoopAsync).Wait();
            }
            else
            {
                Logging.GlobalLogger.Usage();
            }
        }
    }
}
