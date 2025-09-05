using System;
namespace gv.ampp.smpl.util
{
    public class LogUtil
    {
        public static String getCurrentDtTime()
        {
            return DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff");
        }
        public static void LogWithDtTime(String statement)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff':'") + statement);
        }
        
    }
}