using System;
using System.ComponentModel.Design;
using System.IO;
using System.Text;

namespace gv.ampp.control.demo.app
{

    public class LogUtil:IDisposable
    {
        StreamWriter sw;
        FileStream fs;
        static LogUtil inst = null;
        static string logDirectory;
        public static LogUtil getLogIUtilInstance()
        {
            try
            {
                if (inst == null)
                {
                    createLogDir();
                    inst = new LogUtil("TakeNextLog.txt");
                }
                return inst;
            }
            catch (Exception e) { LogWithDtTime(e.StackTrace); return null; }
        }
        private static void createLogDir()
        { // Get the current working directory

            Console.WriteLine("Current Working Directory: " + Program.workingDir);

            // Define the path for the log directory
            logDirectory = Path.Combine(Program.workingDir, "log");

            // Check if the directory exists, if not, create it
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
                Console.WriteLine("Log directory created at: " + logDirectory);
            }
            else
            {
                Console.WriteLine("Log directory already exists at: " + logDirectory);
            }
        }
        private LogUtil(string logFileName)
        {
            string logFile = logDirectory + "\\" + logFileName;

            Console.WriteLine("Log File Name:" + logFile);
            sw = new StreamWriter(logFile);
        }
        public LogUtil(string logFileName, FileMode mode)
        {
            string logFile = logDirectory + "\\" + logFileName;

            Console.WriteLine("Log File Name:" + logFile);
            fs = new FileStream(logFile, mode);
        }
        public static string getCurrentDtTime()
        {
            return DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff");

        }
        public static string getDtTime(DateTime dt)
        {
            return dt.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff");

        }
        public static void LogWithDtTime(string statement)
        {
            Console.WriteLine(DateTime.SpecifyKind(DateTime.Now,
                       DateTimeKind.Local).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff':'") + statement);
            //Console.WriteLine(DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff':'") + statement);
        }
        public void log(string statement)
        {
            sw.WriteLine(DateTime.SpecifyKind(DateTime.Now,
                       DateTimeKind.Local).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff':'") + statement);
            sw.Flush();
            //Console.WriteLine(DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff%k':'") + statement);
        }
        public void log(int position, string statement)
        {
            string timeStamp = DateTime.SpecifyKind(DateTime.Now,
                       DateTimeKind.Local).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff") + ":" + statement;

            byte[] bytes = Encoding.UTF8.GetBytes(timeStamp);
            fs.Seek(position, 0);
            fs.Write(bytes);
            fs.Flush();
            //Console.WriteLine(DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff%k':'") + statement);
        }

        public static string convertMillsToTimeStr(long timeInMillis)
        {
            long ot = timeInMillis;
            long milis = timeInMillis % 1000; timeInMillis /= 1000;

            long secs = timeInMillis % 60; timeInMillis /= 60;
            long min = timeInMillis % 60; timeInMillis /= 60;
            long hr = timeInMillis % 24; timeInMillis /= 24;
            long dd = timeInMillis % 365;
            if (dd != 0)
                return string.Format("{0,2:D2}:{1,2:D2}:{2,2:D2}:{3,2:D2}.{4,2:D2}", dd, hr, min, secs, milis);
            //return ""+ot +"-"+ dd+":"+ hr+":"+ min+":"+ secs+"." +milis;
            else
                return string.Format("{0,2:D2}:{1,2:D2}:{2,2:D2}.{3,2:D2}", hr, min, secs, milis);
            //return ""+ot + "-"+hr + ":" + min + ":" + secs + "." + milis;
        }
        ~LogUtil()
        {
            fs?.Close();
            sw?.Close();
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected Dispose method to release unmanaged resources
        protected virtual void Dispose(bool disposing)
        {
            fs?.Close();
            sw?.Close();
            fs = null;
            sw = null;
        }
    }
}