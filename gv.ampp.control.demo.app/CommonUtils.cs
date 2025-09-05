using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace gv.ampp.control.demo.app
{
    public class CommonUtils
    {
        public static Dictionary<string, string> LoadConfiguration(string xmlFilePath) 
        {
            Dictionary<string, string> configMap = new Dictionary<string, string>();

            // Load the XML document
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlFilePath);

            // Find all <ConfigEntry> elements in the XML
            XmlNodeList configEntryNodes = doc.GetElementsByTagName("ConfigEntry");

            foreach (XmlNode node in configEntryNodes)
            {
                // Ensure the node has the 'PlayoutCommand' and 'RossTalkCommand' attributes
                if (node.Attributes != null)
                {
                    string playoutCommand = node.Attributes["Key"]?.Value;
                    string rossTalkCommand = node.Attributes["Value"]?.Value;

                    // Check if both 'PlayoutCommand' and 'RossTalkCommand' exist
                    if (!string.IsNullOrEmpty(playoutCommand) && !string.IsNullOrEmpty(rossTalkCommand))
                    {
                        configMap[playoutCommand.ToUpper()] = rossTalkCommand;
                    }
                }
            }

            return configMap;
        }
        private static string getDictAsStr(Dictionary<string, string> props)
        {
            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<string, string> kvp in props)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }

            return sb.ToString();
        }
        public static void printDict(Dictionary<string, string> props)
        {
            string result = getDictAsStr(props);

            Console.WriteLine(result);

        }
        public static void printDict(Dictionary<string, string> props, RollingLogger log)
        {
            string result = getDictAsStr(props);

            log.log("Dict content ->\n" + result +"\n");

        }
        public static string MakeProperCommandString(string configCommand)
        {
            //return configCommand.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\0","\0");
            return configCommand.Replace("\\n", "\n")
                            .Replace("\\r", "\r")
                            .Replace("\\0", "\0");
        }
    }
}
