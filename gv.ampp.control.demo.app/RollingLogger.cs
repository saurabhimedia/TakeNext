using System;
using System.IO;
using System.Text;
namespace gv.ampp.control.demo.app
{
    public class RollingLogger : IDisposable
    {
        private string _logDirectory;
        private string _logFileName;
        private long _maxFileSize;
        private const int MaxLogFiles = 10;
        private StreamWriter _currentLogWriter;
        private static RollingLogger inst = null;
        // Flag to track whether Dispose has been called
        private bool _disposed = false;
        public static RollingLogger getInstance(String logfileName)
        {
            try
            {
                if (inst == null)
                {
                    //createLogDir();
                    inst = new RollingLogger(logfileName, 1);
                }
                return inst;
            }
            catch (Exception e) { LogWithDtTime(e.StackTrace); return null; }
        }

        private  void createLogDir()
        { // Get the current working directory

            Console.WriteLine("Current Working Directory: " + Program.workingDir);

            // Define the path for the log directory
            _logDirectory = Path.Combine(Program.workingDir, "log");

            // Check if the directory exists, if not, create it
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
                Console.WriteLine("Log directory created at: " + _logDirectory);
            }
            else
            {
                Console.WriteLine("Log directory already exists at: " + _logDirectory);
            }
        }
        private RollingLogger(string logFileName, long maxFileSizeMB)
        {
            // _logDirectory = logDirectory;
            createLogDir();
            _logFileName = logFileName;
            _maxFileSize = maxFileSizeMB * 1024 * 1024 ; // Convert MB to bytes
            RollLogFiles();
            // Initialize the StreamWriter
            //_currentLogWriter = new StreamWriter(GetLogFilePath(0), true, Encoding.UTF8);
        }

        // Method to get the log file path with a specific index (0-9)
        private string GetLogFilePath(int index)
        {
            return Path.Combine(_logDirectory, $"{_logFileName}_{index}.log");
        }

        // Method to write log entries
        public void log(string message)
        {
            // Check if the current log file (log 0) exceeds the max file size
            if (File.Exists(GetLogFilePath(0)) && new FileInfo(GetLogFilePath(0)).Length > _maxFileSize)
            {
                // Roll over the log files if log 0 is full
                RollLogFiles();
            }
            //DateTime.SpecifyKind(DateTime.Now,
            //           DateTimeKind.Local).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff':'")
            // Write to the current log file (log 0)
            _currentLogWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd'T'HH:mm:ss.fff} : {message}");
            _currentLogWriter.Flush();  // Ensure the data is written immediately
        }

        // Method to roll over log files: Shift all logs and delete the oldest (log 9)
        private void RollLogFiles()
        {
            // Close the current log file and reopen a new one for log 0
            _currentLogWriter?.Close();
            _currentLogWriter = null;
            // Shift all log files down from 9 to 1 (log 0 becomes the most recent)
            for (int i = MaxLogFiles - 1; i > 0; i--)
            {
                string oldFile = GetLogFilePath(i - 1);
                string newFile = GetLogFilePath(i);

                if (File.Exists(oldFile))
                {
                    try
                    {
                        File.Move(oldFile, newFile,true);
                    }
                    catch (Exception e) {
                        Console.WriteLine(e.ToString());
                    }
                }
            }


            _currentLogWriter = new StreamWriter(GetLogFilePath(0), true, Encoding.UTF8);
        }

        // Dispose method to clean up resources
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected Dispose method to release unmanaged resources
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources like StreamWriter
                    _currentLogWriter?.Close();
                    _currentLogWriter = null;
                }
                _disposed = true;
            }
        }

        // Destructor (finalizer) in case Dispose wasn't called
        ~RollingLogger()
        {
            Dispose(false);
        }

        public static void Test(string[] args)
        {
            // Example usage:
            using (RollingLogger logger = RollingLogger.getInstance("TakeNext")) // 5 MB max size for each log file
            {
                // Write some log entries
                for (int i = 0; i < 20; i++) // Write 20 log entries to test the rolling
                {
                    logger.log($"Log Entry {i + 1}");
                }

                Console.WriteLine("Log entries written.");
            }
        }
        public static void LogWithDtTime(string statement)
        {
            Console.WriteLine(DateTime.SpecifyKind(DateTime.Now,
                       DateTimeKind.Local).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff':'") + statement);
            //Console.WriteLine(DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff':'") + statement);
        }
    }
}
