using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using global::gv.ampp.control.demo.app.Model;
namespace gv.ampp.control.demo.app
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using gv.ampp.control.demo.app.Model;
    using Gv.Ampp.Control.Sdk;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;




    public class MirrorManager
    {
        /// <summary>
        /// The required scopes.
        /// </summary>
        internal const string Scopes = "platform cluster.readonly";
        private static Dictionary<string, ChannelEntry> channels = null;
        private static Dictionary<string, string> mirrorMap = null;
        public static string workingDir = null;

        private static readonly AutoResetEvent _closing = new AutoResetEvent(false);
        private static RollingLogger log;
        public static async Task StartAllMirrorInstancesAsync(string xmlConfigPath,
                                               Dictionary<string, string> parameters,
                                               RollingLogger log)
        {
            log.log("*******************************************");
            log.log("Loading all Channels Information...");
            channels = ChannelConfig.LoadChannels(xmlConfigPath,log);
            log.log("Loading mirroring map of Channels ");
            mirrorMap = ChannelConfig.LoadMirrorMap(xmlConfigPath);

            if (mirrorMap.Count == 0)
            {
                log.log("MirrorMap empty. Nothing to start.");
                return;
            }
            log.log("Channels configured are " + channels.ToString());
            channels.ToList().ForEach(kv =>
                    log.log($"Key: {kv.Key}, Value: {kv.Value}")
            );

            /*short baseCmdPort = 16502;
            if (parameters != null && parameters.TryGetValue("Command Server Port", out var p) && short.TryParse(p, out var parsed))
                baseCmdPort = parsed;
            */
            //var workloadPrefix = new AmppConfiguration().GetworkloadName();
            log.log("Trying to read demo configuration .. ");
            var seedConfig = new DemoConfiguration { ConnectionString = "foo://abc/123", Port = 80085 };

            var pairs = new List<MirrorPair>();

            log.log("Checking mirror pairs to validate all channel pairs.. ");
            foreach (var kv in mirrorMap)
            {
                if (!channels.TryGetValue(kv.Key, out var left)) { log.log($"[WARN] Unknown '{kv.Key}' skipping.."); continue; }
                if (!channels.TryGetValue(kv.Value, out var right)) { log.log($"[WARN] Unknown '{kv.Value}' skipping.."); continue; }
                var pair = new MirrorPair( seedConfig, log,
                                        left, right,
                                        parameters);
                    pairs.Add(pair);

            }
            log.log("Trying to start all valid channel pairs.. ");

            await Task.WhenAll(pairs.ConvertAll(p => p.StartAsync())).ConfigureAwait(false);
        }

        protected static void OnExit(object sender, ConsoleCancelEventArgs args)
        {
            log.log("Exit");
            _closing.Set();
        }

        private static void Usage()
        {
            LogUtil.LogWithDtTime("***************************gv.ampp.control.demo.app**********************************************");
            LogUtil.LogWithDtTime("Usage: ");
            LogUtil.LogWithDtTime("IPTrigger.exe <Switcher IP> <Switcher Port> <networkId> [Working Dir] [Command Server Port]");
            LogUtil.LogWithDtTime("Socket server to accept command is started at 16502 command server Port is not provided\n");
            LogUtil.LogWithDtTime("\n");
            LogUtil.LogWithDtTime(" Following  Variables to be defined in appsettings,json!!");
            LogUtil.LogWithDtTime("\"GVCLUSTER_PLATFORMAPIKEY\":<Value:  | ApiKey for connecting to GVPlatform>");
            LogUtil.LogWithDtTime("\"GVCLUSTER_PLATFORMURI\" :< Uri for for connecting to GVPlatform>");
            LogUtil.LogWithDtTime("\"\"GVCLUSTER_WORKLOADNAME\"\" :< Workload Name>");
            LogUtil.LogWithDtTime("*************************************************************************************************");
        }

        private static void UsageNew()
        {
            LogUtil.LogWithDtTime("***************************gv.ampp.control.demo.app**********************************************");
            LogUtil.LogWithDtTime("Usage: ");
            LogUtil.LogWithDtTime("<Exec Name> <ConfigFile Name>");
            LogUtil.LogWithDtTime("\n");
            LogUtil.LogWithDtTime(" Following  Variables to be defined in appsettings,json!!");
            LogUtil.LogWithDtTime("\"GVCLUSTER_PLATFORMAPIKEY\":<Value:  | ApiKey for connecting to GVPlatform>");
            LogUtil.LogWithDtTime("\"GVCLUSTER_PLATFORMURI\" :< Uri for for connecting to GVPlatform>");
            LogUtil.LogWithDtTime("\"\"GVCLUSTER_WORKLOADNAME\"\" :< Workload Name>");
            LogUtil.LogWithDtTime("*************************************************************************************************");
        }
        static Dictionary<string, string> ParseArguments(string[] args)
        {
            var dict = new Dictionary<string, string>();

            foreach (var arg in args)
            {
                // Ensure the argument starts with '-' and contains '='
                if (arg.StartsWith("-") && arg.Contains("="))
                {
                    // Remove the leading '-' and split by '='
                    var parts = arg.Substring(1).Split('=');

                    // Check if we have exactly two parts: name and value
                    if (parts.Length == 2)
                    {
                        string name = parts[0].Trim();
                        string value = parts[1].Trim();

                        // Add the key-value pair to the dictionary
                        dict[name] = value;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid argument format: {arg}");
                    }
                }
                else
                {
                    //Console.WriteLine($"Skipping invalid argument: {arg}");
                }
            }

            return dict;
        }
        public static string getParam(Dictionary<string, string> commonParams, String key)
        {
            string val = null;
            try
            {
                val = commonParams[key.ToUpper()];
            }
            catch (Exception e)
            {

            }
            return val;
        }
        public static short getParamShort(Dictionary<string, string> commonParams, String key)
        {
            string val = null;
            try
            {
                val = commonParams[key.ToUpper()];
                return short.Parse(val);
            }
            catch (Exception e)
            {

            }
            return -1;
        }
        private static void printMap(Dictionary<String, String> dict)
        {
            Console.WriteLine("Key:Values of the dictionary are:");

            foreach (var pair in dict)
            {
                Console.WriteLine($"{pair.Key} : {pair.Value}");
            }

        }
        private static void printConfig(IConfiguration config)
        {
            foreach (var key in config.AsEnumerable())
            {
                Console.WriteLine($"{key.Key}: {key.Value}");
            }
        }
        public static string getParam1(IConfiguration commonParams, String key)
        {
            string val = null;
            try
            {
                val = commonParams[key];
            }
            catch (Exception e)
            {
                ;
            }
            return val;
        }
    }


}
