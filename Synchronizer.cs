namespace TimeSync
{
    using System;
    using System.IO;         // File.GetLastWriteTimeUtc(), Path.GetFileNameWithoutExtension()
    using System.Reflection; // Assembly.GetExecutingAssembly().Location

    public class Synchronizer
    {
//      private static readonly DateTime LastModified = new DateTime(2021, 11, 6, 0, 0, 0, DateTimeKind.Local);
        private static readonly DateTime LastModified =
            File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location); // <https://stackoverflow.com/a/2050419>

        private static int Main(string[] args)
        {
            PrintIntro();

            // Defaults
            TimeServerHostnameOrIpAddress = "pool.ntp.org";
            ShouldUpdateSystemTime = true;

            try
            {
                // Check arguments
                if (args.Length > 0)
                {
                    ValidateInput(args);
                }

                Console.Write("Connecting to ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(TimeServerHostnameOrIpAddress);
                Console.ResetColor();
                Console.WriteLine("...");
                Console.WriteLine();

                // Prepare SNTP client and connect to time server
                var client = new SntpClient();
                client.ConnectToTimeServer(TimeServerHostnameOrIpAddress, ShouldUpdateSystemTime);
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {e.Message}");
                Console.ResetColor();
                return -1;
            }

            return 0;
        }
        private static string TimeServerHostnameOrIpAddress;
        private static bool ShouldUpdateSystemTime;

        private static void PrintIntro()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Time Synchronizer (SNTP Client)");
            Console.WriteLine("(C)2001-2019 Valer BOCAN, PhD <valer@bocan.ro>");
//          Console.WriteLine("Modified by Miguel GARCIA-BLANCO [{0:g}{0:%K}]", LastModified.ToUniversalTime());
            Console.WriteLine("Modified by Miguel GARCIA-BLANCO [{0:yyyy/MM/dd HH:mmK}]", LastModified.ToUniversalTime());
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void ValidateInput(string[] args)
        {
            string s;
            if (args.Length > 1)
            {
                if (Char.IsLetterOrDigit(args[1], 0))
                {
                    TimeServerHostnameOrIpAddress = args[1];
                }
                else
                {
                    s = $"Invalid argument: ARGS[1] = {args[1]}. ARGS[1] must be a hostname or IP address.";
                    throw new Exception(s);
                }
            }

            if (Char.IsLetterOrDigit(args[0], 0))
            {
                TimeServerHostnameOrIpAddress = args[0];
            }
            else
            {
                switch (args[0])
                {
                    case "/?": // Print help and exit
                    case "-?":
                    case "-h":
                    case "--help":
                        PrintHelp();
                        Environment.Exit(0);
                        break;
                    case "/q": // Query only - do not set the clock
                    case "-q":
                    case "--query":
                        ShouldUpdateSystemTime = false;
                        break;
                    default:
                        s = $"Invalid argument: {args[0]}";
                        throw new Exception(s);
                }
            }

            if (TimeServerHostnameOrIpAddress.Length < 4)
            {
                s = $"Invalid argument: SERVER = {TimeServerHostnameOrIpAddress}. SERVER must be a valid hostname or IP address.";
                throw new Exception(s);
            }
        }

        private static void PrintHelp()
        {
            var fileName = Environment.GetCommandLineArgs()[0];        // Executable file name
            var baseName = Path.GetFileNameWithoutExtension(fileName); // <https://stackoverflow.com/a/616599>
            Console.WriteLine("Set the system date and time from a remote NTP time server.");
            Console.WriteLine();
            Console.WriteLine($"Usage: {baseName} [-q] [SERVER]");
            Console.WriteLine("  -q, --query   Query only - do not set the clock");
            Console.WriteLine("  -?, --help    Display this help and exit");
            Console.WriteLine();
            Console.WriteLine("If SERVER is not specified, the default server 'pool.ntp.org' will be used.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine($"  {baseName} -q");
            Console.WriteLine($"  {baseName} time.nist.gov");
            Console.WriteLine($"  {baseName} 128.138.141.172");
        }
    }
}
