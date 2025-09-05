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
using System.Text.RegularExpressions;

namespace gv.ampp.control.demo.app
{

    public class Program
    {
        /// <summary>
        /// The required scopes.
        /// </summary>
        internal const string Scopes = "platform cluster.readonly";
        private static Dictionary<string, string> commonParams = null;
        public static string workingDir = null;

        private static readonly AutoResetEvent _closing = new AutoResetEvent(false);
        private static RollingLogger log;
        static async Task Main(string[] args)
        {
            short cmdServerPort = 16502;
            string workloadName = null;

            
            if (args != null && args.Length < 1)
            {
                Console.WriteLine(" App Config File Name should be provided!! !!");
                UsageNew();

                Console.ReadLine();
                Environment.Exit(0);
            }
            try
            {
                commonParams = CommonUtils.LoadConfiguration(args[0]);

            }
            catch (Exception ex)
            {
                Console.WriteLine(" Error loading configuration file!! !!" +ex.ToString());
            }
            try
            {
                workingDir = commonParams["Working Directory"];
            }
            catch (Exception ex)
            {
                if (workingDir == null)
                    workingDir = Directory.GetCurrentDirectory();
            }
            String AppName = getParam(commonParams,"Application Name");
            if (AppName == null)
                AppName = "TakeNextPair";



            log = RollingLogger.getInstance(AppName);
            log.log("Initializing the \"" + AppName + "\" Application");
            string ackRegX = getParam(commonParams, "ACK_REGX");
         
            CommonUtils.printDict(commonParams, log);
         
            // This class reads config from either environment variables, or  command line commonParams
            // When running under node agent then will be injected as Environment variables

            AmppConfiguration amppConfiguration = new AmppConfiguration();
            workloadName = amppConfiguration.GetworkloadName();

            //string workloadName = amppConfiguration.GetworkloadName();
            //           CrashLiveApp.getNextIDTest("");
            /*
            string apiKey = amppConfiguration.GetAPIKey();
            string platformUrl = amppConfiguration.GetPlatformUrl();


            log.log($"WorkloadName:\t\t{workloadName}");
            log.log($"PlatformUri:\t\t{platformUrl}");
            log.log($"ApiKey:\t\t\t{apiKey}");
            log.log($"Working Dir:\t\t\t{workingDir}");
            log.log($"Command Listener Server Port:\t\t\t{cmdServerPort}");
            if (string.IsNullOrEmpty(platformUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(workloadName))
            {
                log.log(" PlatformUri , APIKey ,workloadName Must be provided \r\nConfigure system variables GVCLUSTER_PLATFORMAPIKEY,GVCLUSTER_PLATFORMURI,GVCLUSTER_WORKLOADNAME");
                Usage();
                Console.ReadLine();
                Environment.Exit(0);
            }
            */

            // Read workloadId for this app from the application configuration
            // If one doesn't exists we create a new one and write it back to the appsettings.json file
            // so that we can reuse it
            /*

            // Attempt to connect to GVPlatform
            log.log("*******************************************");
            log.log("Attempting to authenticate with platform...");

            AmppControlService amppControl = new AmppControlService(platformUrl, apiKey, workloadName);
            if (!await amppControl.ConnectToGVPlatform())
            {
                //amppControl.RegisterHandler

                log.log("Error Connecting to GV Platform!!!!");
                Console.ReadLine();
                Environment.Exit(0);
            }

            log.log("Connected Okay...");
            log.log("*******************************************");

            // Read Our Configuration from the Configuration Service.
            var config = await amppControl.GetConfigurationAsync();

            DemoConfiguration demoConfig = null;

            if (config == null)
            {
                demoConfig = new DemoConfiguration() { ConnectionString = "foo://abc/123", Port = 80085 };
                var jsonConfig = JsonConvert.SerializeObject(demoConfig);

                log.log("Creating new Configuration");
                await amppControl.CreateConfigurationAsync(jsonConfig);
            }
            else
            {
                demoConfig = JsonConvert.DeserializeObject<DemoConfiguration>(config.Value);
                log.log("Configuration ReadFromPlatform:");
                log.log($"Connection String: {demoConfig.ConnectionString}");
                log.log($"Port             : {demoConfig.Port}");
            }

            */

            // Create our Application and Initialise it
            // This sets up all the method handlers and listens to the SignalR notifications
            // As well as writing details of our app to the AMPP Control Registry
            await MirrorManager.StartAllMirrorInstancesAsync(args[0], commonParams, log);
			/*
			
			AmmChannelMirrorApp demoApplication = new AmmChannelMirrorApp(workloadName, demoConfig, amppControl, platformUrl,
                apiKey, commonParams);
            log.log("Initialising Socket Server Service...");

            val = Program.getParam(commonParams, "TIME_BETWEEN_COMMANDS");
            if (val != null)
            {
                TCPSocketServer.SetIgnoreInterval(int.Parse(val));
            }
            val= getParam(commonParams, "Command Server Port");
            if (val != null)
            {
                cmdServerPort = short.Parse(val);

                TCPSocketServer svr = new TCPSocketServer(cmdServerPort, demoApplication);
                svr.StartServer();
            }*/
            //Thread t = new Thread(new ThreadStart(() => svr.StartServer()));
            //await amppControl.InitializeWorkloadAsync("DemoService", "3rd Party", demoApplication);

            //just to test
            //await demoApplication.notifyOxtel(null,null);
            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnExit);
            _closing.WaitOne();


            log.log("Bye!");
            Environment.Exit(0);
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
