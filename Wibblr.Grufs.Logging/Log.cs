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

        public static void WriteLine(int verbose, string message)
        {
            if (verbose >= Verbose)
            {
                Console.WriteLine(message);
            }
        }

        public static void WriteStatusLine(int verbose,string message)
        {
            if (!StdOutIsConsole)
            {
                return;
            }
            if (verbose >= Verbose)
            {
                Console.CursorVisible = false;
                Console.Write(message);
                Console.Write(new string(' ', Console.WindowWidth - message.Length));
                Console.CursorLeft = 0;
                Console.CursorVisible = true;
            }
        }
    }
}