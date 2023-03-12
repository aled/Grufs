namespace Wibblr.Grufs.Logging
{
    /// <summary>
    /// Super simple logging class. Can add a full featured
    /// package later if necessary.
    /// 
    /// Allow non-interactive use (e.g. unit testing) to have 
    /// less output.
    /// </summary>
    public class Log
    {
        public static bool StdOutIsConsole { get; set; }
        public static int Verbose { get; set; } = 0;
        public static bool HumanFormatting { get; set; }
        public static bool Progress { get; set; }

        static Log()
        {
            // the VS test output console does not support this.
            // Use this to detect and disable console status messages
            try 
            {
                Console.CursorVisible = true;
            }
            catch (Exception)
            {
                StdOutIsConsole = false;
            }
        }

        public static void Write(int verbose, string message)
        {
            if (Verbose >= verbose)
            {
                Console.Write(message);
            }
        }

        public static void WriteLine(int verbose, string message)
        {
            if (Verbose >= verbose)
            {
                Console.WriteLine(message);
            }
        }

        public static void WriteStatusLine(int verbose, string message)
        {
            if (!StdOutIsConsole || !Progress)
            {
                return;
            }
            if (Verbose >= verbose)
            {
                if (message.Length > Console.WindowWidth)
                {
                    message = message.Substring(0, Console.WindowWidth - 1);
                }
                Console.CursorVisible = false;
                Console.Write(message);
                Console.Write(new string(' ', Math.Clamp(Console.WindowWidth - message.Length, 0, 1000)));
                Console.CursorLeft = 0;
                Console.CursorVisible = true;
            }
        }
    }
}