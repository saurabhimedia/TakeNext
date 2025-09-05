using gv.ampp.control.demo.app.Model;
using Gv.Ampp.Control.Sdk;
using Gv.Ampp.Control.Sdk.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using static System.Formats.Asn1.AsnWriter;
using System.Net.Sockets;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Security.Authentication.ExtendedProtection;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using gv.ampp.control.demo.app.Mirroring;
using Formatting = Newtonsoft.Json.Formatting;

namespace gv.ampp.control.demo.app
{

    public class AmppChannelMirrorApp : IMirrorEndpoint
    {
        private IMirrorEndpoint _peer;
        private static RollingLogger log = RollingLogger.getInstance("TakeNext");
        //private static LogUtil log = LogUtil.getLogIUtilInstance();
        private readonly AmppControlService amppControlService;
        private readonly DemoConfiguration configuration;
        private String platformUrl;
        //private readonly String rundownID;
        private  String bearerToken;
        private DateTime nextRefreshTime;
        private String apiKey;
        private long lastCmdTime;
        private String ip;
        private short port;
        private String networkID;
        private static int REPEATITIVE_CMD_IGNORE_DURATION = 50000000;
        private static int DELAY_AFTER_CMD=0;
        private static int TAKE_STARTTIME_DELAY = 100;
        private static int TAKE_DELAY_AFTER_CUE = 100;
        private static TimeSpan NEXT_ITEM_ID_FETCH_DURATION_IN_MILLS = TimeSpan.FromMilliseconds(1000);
        private static bool autoCueReadyItem = false;
        private static LogUtil logTimeStamp=null;

        private DemoState[] applicationState = new DemoState[10];
        private String takeNextRegX = "TakeNext";
        private Dictionary<string, string> appCmdMap;
        private String workingDir;

        TCPSocketClient cs;
        /// <summary>
        /// Example Schema for settting State
        /// </summary>
        // Set this in your ctor when you build the instance from ChannelEntry
        public string ChannelName { get;  set; }


        /// Example Schema for settting State
        /// </summary>
        private const string TakeLiveSchema = @"
        {
          'title': 'TakeLive State',
          'type': 'object',
          'properties': {
            
            'Active': {
              'type': 'boolean',
              'title': 'Active'
            },
             
            'RundownID': {
              'type': 'string',
              'title': 'RundownID'
            }
            
          }
        }";
        private const string TakeNextOnPairNetworkSchema = @"
        {
          'title': 'Take Next on Pair Network',
          'type': 'object',
          'properties': {
            
            'Active': {
              'type': 'boolean',
              'title': 'Active'
            }            
          }
        }";
        /// Example Schema for settting State
        /// </summary>
        private const string IPTriggerSchema = @"
        {
          'title': 'Trigger Switcher over IP',
          'type': 'object',
          'properties': {
            
            'Active': {
              'type': 'boolean',
              'title': 'Active'
            },
             
            'Command': {
              'type': 'string',
              'title': 'Command'
            }
            
          }
        }";
        private const string OxtelNotifySchema= @"
        {
          'title': 'Notify Oxytel',
          'type': 'object',
          'properties': {
            
            'Active': {
              'type': 'boolean',
              'title': 'Active'
             },
            'Layer': {
              'type': 'string',
              'title': 'Layer'
            },
            'Category': {
              'type': 'string',
              'title': 'Category'
            },
          'Template': {
              'type': 'string',
              'title': 'Template'
            }
            
          }
        }";

       /// <summary>
        /// Schema for settting some config
        /// </summary>
        private const string ConfigSchema = @"
        {
          'title': 'Updates Configuration',
          'type': 'object',
          'properties': {
            'ConnectionString': {
              'type': 'string',
              'title': 'Connection String'
            },
            'Port': {
              'type': 'integer',
              'title': 'Port Number',
              'minimum' : 1000,
              'maximum' : 65535
            }
          }
        }";

        /// <summary>
        /// Example Schema for settting State
        /// </summary>
        private const string StateSchema = @"
        {
          'title': 'Updates State',
          'type': 'object',
          'properties': {
            'Index': {
              'type': 'integer',
              'title': 'Channel Index',
              'minimum' : 1,
              'maximum' : 10
            },
            'Volume': {
              'type': 'integer',
              'title': 'Volume',
              'minimum' : 0,
              'maximum' : 100
            },
            'Active': {
              'type': 'boolean',
              'title': 'Active'
            },
            'Label': {
              'type': 'string',
              'title': 'Label'
            }
          }
        }";

        /// <summary>
        /// Schema for metrics
        /// </summary>
        private const string MetricsSchema = @"{
          'type': 'object',
          'properties': {
            'offset': {
              'type': 'integer',
              'description': 'Offset from master',
              'unit': 'us',
              'error_criteria': '> 15',
              'warn_criteria': '> 10',
              'minimum': 0,
              'maximum': 100
            },
            'gmid': {
              'type': 'string',
              'description': 'Grand Master ID'
            },
            'source': {
              'type': 'string',
              'enum': [
                'GPS',
                'Boundary Clock',
                'Unlocked'
              ],
              'description': 'Time Source',
              'error_criteria': '== Unlocked'
            },
            'locked': {
              'type': 'boolean',
              'description': 'PTP Locked',
              'error_criteria': '== false'
            },
            'kernel_driver': {
              'type': 'boolean',
              'description': 'Kernel Driver Mode',
              'error_criteria': '== false'
            }
          }
        }";

        string IMirrorEndpoint.ChannelName
        {
            get => this.ChannelName;
            set => this.ChannelName = value; // harmless if interface is get-only; compiler ignores it
        }

        public String getRundownId()
        {
            return this.networkID;
        }

       
        public AmppChannelMirrorApp(String appName, DemoConfiguration configuration, AmppControlService amppControlService,
            String platformUrl, String apiKey, Dictionary<String, String> props)
        {


            this.amppControlService = amppControlService;
            this.configuration = configuration;
            // this.rundownID = rundownID;
            this.apiKey = apiKey;
            this.nextRefreshTime = DateTime.Now;
            this.lastCmdTime = 0;
            this.ip = Program.getParam(props,"Switcher IP");
            this.port = Program.getParamShort(props, "Switcher Port");
            this.networkID = Program.getParam(props,"NETWORKID");
            this.platformUrl = platformUrl;
            this.workingDir = Program.workingDir;
            String val = Program.getParam(props,"TIME_BETWEEN_COMMANDS");
            if (val != null)
            {
                REPEATITIVE_CMD_IGNORE_DURATION = int.Parse(val);
            }
            val = Program.getParam(props, "DELAY");
            if (val != null)
            {
                DELAY_AFTER_CMD = int.Parse(val);
            }
            val = Program.getParam(props, "LogGoLiveTimeStamp");
            if (val != null && val.ToLower().Equals("yes"))// only for Take Live or Take commands needed
            {
                if (logTimeStamp == null)
                {
                    LogUtil.getLogIUtilInstance();

                    logTimeStamp = new LogUtil("GoLiveTimeStamp.txt", FileMode.Create | FileMode.OpenOrCreate );
                }
            }
            val = Program.getParam(props, "TAKE_STARTTIME_DELAY");
            if (!String.IsNullOrEmpty(val))
            {
                TAKE_STARTTIME_DELAY = int.Parse(val);
            }
            val = Program.getParam(props, "TAKE_DELAY_AFTER_CUE");
            if (!String.IsNullOrEmpty(val))
            {
                TAKE_DELAY_AFTER_CUE = int.Parse(val);
            }
            
            val = Program.getParam(props, "NEXT_ITEM_ID_FETCH_DURATION");
            if (!String.IsNullOrEmpty(val))
            {
                NEXT_ITEM_ID_FETCH_DURATION_IN_MILLS = TimeSpan.FromMilliseconds(int.Parse(val));
            }
            val = Program.getParam(props, "AutoCueReadyItem");
            if (!String.IsNullOrEmpty(val) && val.ToLower().Equals("yes"))
            {
                autoCueReadyItem = true;
            }
            //Start the platform connection and brearer token refreshing thread
            //Thread refreshThread =new Thread(new ThreadStart(refreshPlatformConnection));
            Thread refreshThread = new Thread(new ThreadStart(() => refreshPlatformConnection(appName)));
            refreshThread.Start();

            this.appCmdMap = props;// LoadConfiguration("..\\Config\\appCmdMap.xml");
                
            val = Program.getParam(appCmdMap, "TIME_BETWEEN_COMMANDS");
            if (val != null)
            {
                REPEATITIVE_CMD_IGNORE_DURATION = int.Parse(val);
            }
            val = Program.getParam(appCmdMap, "DELAY");
            if (val != null)
            {
                DELAY_AFTER_CMD = int.Parse(val);
            }

            for (int i = 0; i < 10; i++)
            {
                this.applicationState[i] = new DemoState()
                {
                    Active = true,
                    Index = i + 1,
                    Label = $"Channel:{i + 1}",
                    Volume = 0,
                };
            }


        }
        private async void refreshPlatformConnection(String appName)
        {
            
            long time2wait4Refresh;
            Boolean isSubscribed = false;
            while (true)
            {
                try
                {
                   log.log("Next Refresh time " + LogUtil.getDtTime(this.nextRefreshTime) + " current time " + 
                        LogUtil.getCurrentDtTime());

                    
                    if (this.nextRefreshTime.Ticks > DateTime.Now.Ticks)
                    {
                        time2wait4Refresh = (this.nextRefreshTime - DateTime.Now).Ticks / 10000;
                        log.log("Sleeping for " + LogUtil.convertMillsToTimeStr(time2wait4Refresh) + ".... for refresh of connection,bearer token!!");
                        Thread.Sleep((int)time2wait4Refresh);
                    }
                    log.log("Initiating bearer token refresh from Refresh thread");
                    await refreshBearerToken();
                    log.log("Initiating connection  refresh to GV platform from Refresh thread");
                    await amppControlService.ConnectToGVPlatform();
                    log.log("Connect to  GV platform successful!!");
                    if (!isSubscribed)
                    {
                        await amppControlService.InitializeWorkloadAsync(appName, "3rd Party", this);
                        log.log("subscribed to workload and...Initialised");
                        isSubscribed = true;
                    }
                }
                catch (Exception e)
                {
                    LogUtil.LogWithDtTime("Exception in Connection + Token Refresh Thread..\n" +
                        e.ToString());
                }
                // log.log("Next ID "+await getNextID("xl03-plx-01"));

                //await TakeNext("93739936-94db-4708-8b46-dd52e279259b");
            }
        }
        private  async Task refreshBearerToken()
        {
            if (DateTime.Now.Ticks < this.nextRefreshTime.Ticks)
            {
                //          LogUtil.LogWithDtTime("  refreshBearerToken " + DateTime.Now.Millisecond + " next RefreshTime " + this.nextRefreshTime);

                return;
            }
            log.log("Got to Refresh Bearer token.. " );
            String url = platformUrl + "/identity/connect/token";
            //String url = "https://apac1.gvampp.com/identity/connect/token";
            ///identity/connect/token

            using HttpClient client = new HttpClient();


            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

           // LogUtil.LogWithDtTime("API KEY:" + apiKey);
            //apiKey = "YjZjM2YyYzFmOGFiNDA2YTk2YmY3YThjZWY4NTFhZGY6dUh4WWMyMFZVZ1NpeCsvbkI1VEZiZGZ6WmtrVWZlQVhnSW0xaks4YjFTN2ZzSGhyQlFKdy9VQ05XaEgxcm1mUDVtR0tvWUU5ajZCVS9iL1IwVWRDVFE9PQ==";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", apiKey);
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url));
            // CONTENT-TYPE header
            //platform cluster platform.readonly cluster.readonly playout.orchestration playout.orchestration.readonly
            request.Content = new StringContent("grant_type=client_credentials&scope=platform%20cluster%20playout.orchestration%20platform.readonly%20playout.orchestration.readonly%20cluster.readonly", Encoding.UTF8, "application/x-www-form-urlencoded");

            HttpResponseMessage response = await client.PostAsync(url, request.Content);

           if (response.IsSuccessStatusCode)
           {
                 String responseBody = await response.Content.ReadAsStringAsync();
                 JObject responseJson = JObject.Parse(responseBody);
                 this.bearerToken = (String)responseJson.GetValue("access_token");
                 long expiry = long.Parse((String)responseJson.GetValue("expires_in"));
                if(expiry>7200)
                    expiry = 7200;
                log.log("Expiry in "+LogUtil.convertMillsToTimeStr(expiry * 1000));
                this.nextRefreshTime = DateTime.Now.AddTicks( (expiry-60)*10000000);// every 59th minute
                log.log("Completed Bearer token refresh.. New refresh time " + 
                     LogUtil.getDtTime(this.nextRefreshTime)/*New Bearer token:!" +this.bearerToken*/);
             }
             else
             {
                log.log(" Error in getting bearer tocken response!! "+
                     response.StatusCode +" " +response.Content );
                this.nextRefreshTime = DateTime.Now.AddTicks(50000000);
            }
        }
        

        [AMPPCommand("golive", Schema = TakeLiveSchema, Version = "1.0", Markdown = "Markdown.golive.md")]
        public async Task goLive(JObject payload, string reconKey)
        {

            log.log("Received goLive command");
            logTimeStamp.log(0,"GoLive Received!!");//Log the timeStamp
            //this.refreshBearerToken();
            log.log(JsonConvert.SerializeObject(payload, Newtonsoft.Json.Formatting.Indented));

            // Do some work 
            var demoState = payload.ToObject<DemoState>();
            //sendIPTrigger(payload, reconKey);
            if (demoState.Active==true)
            {
                //String rundownID = "n18-playout1";
                String liveItemId = await getLiveID(demoState.rundownID);
                log.log("Id to take Live" + liveItemId);
                if(!string.IsNullOrEmpty(liveItemId)) 
                  await takeLive(demoState.rundownID, liveItemId);
            }

            
            // Inform all clients that the configuration has changed.
            //await amppControlService.PushAmppControlMessageAsync("goLive","", reconKey);

        }

        [AMPPCommand("triggerip", Schema = IPTriggerSchema, Version = "1.0", Markdown = "Markdown.triggerip.md")]
        public async Task triggerIP(JObject payload, string reconKey)
        {

            log.log("Received trigggerIP command");
            logTimeStamp.log(0, "trigggerIP Received!!");//Log the timeStamp
            //this.refreshBearerToken();
            log.log(JsonConvert.SerializeObject(payload, Newtonsoft.Json.Formatting.Indented));

            // Do some work 
            var demoState = payload.ToObject<DemoState>();
            await sendIPTrigger(payload, reconKey);
          // Inform all clients that the configuration has changed.
          //await amppControlService.PushAmppControlMessageAsync("goLive","", reconKey);

        }

        [AMPPCommand("notifyoxytel", Schema = OxtelNotifySchema, Version = "1.0", Markdown = "Markdown.notifyoxytel.md")]
        public async Task notifyOxytel(JObject payload, string reconKey)
        {
            try
            {
                // Do some work 
                var oxytelCmd = payload.ToObject<OxyTelCommand>();

                log.log("Received notifyOxtel command");
                if ((DateTime.Now.Ticks - this.lastCmdTime) < REPEATITIVE_CMD_IGNORE_DURATION)
                {
                    log.log("Ignored notifyOxtel command as it is within " +
                        REPEATITIVE_CMD_IGNORE_DURATION / 10000000 + "secs");// 1 sec is 1/10000000 ticks, 1 ms is 1/10000 ticks
                    return;
                }
                this.lastCmdTime = DateTime.Now.Ticks;

                //String ip = "192.168.5.100"; int port = 5006; // To Do: Soumen how to take from env variable
               // String ip = "172.16.8.123"; int port = 5007;
                TCPSocketClient cs;
                try
                {
                    cs = new TCPSocketClient(ip, port,null,null);
                }
                catch (Exception e) {
                    log.log("Failed to connect to socket to " + ip + ":" + port +" ,Notification to Oxytel failed!!");
                    return;
                }
                Thread t = new Thread(new ThreadStart(() => cs.SendLogoActivationCmdAsync(oxytelCmd.layer, oxytelCmd.Category, oxytelCmd.Template)));
                //Thread t = new Thread(new ThreadStart(cs.sendLogoActivationCmd));
                 t.Start();
                //await cs.sendLogoActivationCmd();
                
            }
            catch(Exception e)
            {
                log.log(e.ToString());
                
            }

        }
      
       
        public async Task sendIPTrigger(JObject payload, string reconKey)
        {
            try
            {
                log.log("Received IPTrigger command->" + payload.ToString());
                // Do some work 
                var cmd = payload.ToObject<DemoState>();

                log.log("Received IPTtigger command->" + cmd.Command);
                if ((DateTime.Now.Ticks - this.lastCmdTime) < REPEATITIVE_CMD_IGNORE_DURATION)
                {
                    log.log("Ignored  command as it is within " +
                        REPEATITIVE_CMD_IGNORE_DURATION / 10000000 + "secs");// 1 sec is 1/10000000 ticks, 1 ms is 1/10000 ticks
                    return;
                }
                this.lastCmdTime = DateTime.Now.Ticks;

                string ackRegX = Program.getParam(appCmdMap, "ACK_REGX");
                string initialHandshakeCmd = Program.getParam(appCmdMap, "INITIAL_HANDSHAKE_COMMAND");
                try
                {
                    if(cs== null )
                        cs = new TCPSocketClient(ip, port, ackRegX, initialHandshakeCmd);
                }
                catch (Exception e)
                {
                    log.log("Failed to create socket Endpoint with " + ip + ":" + port + " ,Notification  failed!!");
                    return;
                }
                String rsCmd = Program.getParam(appCmdMap,cmd.Command);
                if (rsCmd == null)
                {
                    log.log("No command is avaiable in Configuration for Key  " + cmd.Command + " ,Notification to Application ignored!!");
                    
                    return;
                }
                string command=CommonUtils.MakeProperCommandString(rsCmd);
                Thread t = new Thread(new ThreadStart(() => cs.SendIPCmdAsync(command, DELAY_AFTER_CMD)));

                t.Start();
                Thread.Sleep(2000);
                //await t.CurrentUICulture();

            }
            catch (Exception e)
            {
                log.log(e.ToString());

            }

        }


        private async Task<String> getLiveID(String rundownID)
        {
            String liveItemId = "";
            try
            {  
                Boolean onAir = false;
                String url = "https://apac1.gvampp.com/orchestration/api/v1/schedules/" + rundownID + "?withState=true&withContent=false";
                using HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJSUzI1NiIsImtpZCI6IkE2ODEwREZENTg3MkUzMjFBQkY1NjMzNjZEMUM1MEYwQUQzMUY3MjQiLCJ0eXAiOiJKV1QiLCJ4NXQiOiJwb0VOX1ZoeTR5R3I5V00yYlJ4UThLMHg5eVEifQ.eyJuYmYiOjE2OTcxMzAwMDYsImV4cCI6MTY5NzE0NDQwNiwiaXNzIjoiaHR0cHM6Ly9hcGFjMS5ndmFtcHAuY29tL2lkZW50aXR5IiwiYXVkIjoiaHR0cHM6Ly9hcGFjMS5ndmFtcHAuY29tL2lkZW50aXR5L3Jlc291cmNlcyIsImNsaWVudF9pZCI6ImY5MWJiNTFiZTA3YTQ2YmM5ZTkzNTE2MDJiZjI1ZTVkIiwic3ViIjoiYThjNTEwYjE2OTFjNGMwZDg3YWU2NmRkZTYzMWFhOTgiLCJhdXRoX3RpbWUiOjE2OTcxMDYxNTUsImlkcCI6ImxvY2FsIiwiYWNjIjoiUHJpbWVhc3VyZUV4cGxvcmVyIiwicm9sZSI6WyJ1c2VyIiwiZGFzaGJvYXJkLmVkaXRvciIsImRhc2hib2FyZC51c2VyIiwiZ3YudWkudXNlciIsImNsdXN0ZXIuYWRtaW4iLCJndWVzdCIsImd2LnVpLmFkbWluIiwiZ3YudWkuZWRpdG9yIl0sImp0aSI6IjM2OTVDREI0RkM4NkNDRDJDRjI3MEI1MkM2N0Y3QzAwIiwic2lkIjoiNjY0RUFBMEJCQzREMzBCQkNFMjFGMkEwMTdFMkM4RDkiLCJpYXQiOjE2OTcxMzAwMDYsInNjb3BlIjpbInBsYXRmb3JtIiwicGxhdGZvcm0ucmVhZG9ubHkiLCJwbGF5b3V0Lm9yY2hlc3RyYXRpb24ucmVhZG9ubHkiLCJwbGF5b3V0Lm9yY2hlc3RyYXRpb24iXSwiYW1yIjpbInB3ZCJdfQ.pc0e1AdMgImG2qT-bL3BpL_Si9wMUfgzwUvK8wHpb8udx7bSKm586HTCM9mQGNf1dCbd0kqnMT-cmFWwjboeEPziyJdSYfQp55kO4ECjYN48FcqhxBfGWDXmAw51FYU1nsjT0pD2MzMoi0pLScAsYCdBzIktryE3GN5IUN66teq6MCy0EDNMnWgmLTr2k-2ylJWTImtvnm4Wkx1CpmrJxAVJWDrAAvqp4O394Ht9-NB-UF4eFlX9wu4y5AqpWsdA1VF2qrpBBTgKU4vbqVZvRK6tWbdvQo41Twa249AOK919h6MQwQOtAOgBKtcyODB1Vc6THYSxwDYU3FxgSHb2lQ");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                HttpResponseMessage response = await client.GetAsync(url);
                log.log("################################  ------ " + response.StatusCode);
                if (response.IsSuccessStatusCode)
                {
                    String responseBody = await response.Content.ReadAsStringAsync();
                    //LogUtil.LogWithDtTime(responseBody);
                    //var objects = JsonConvert.SerializeObject(responseBody, Formatting.Indented);
                    JObject o = JObject.Parse(responseBody);
                    JArray jArray = (JArray)o.GetValue("items");
                    foreach (var data in jArray)
                    {

                        if (onAir && ((JObject)data["payload"]).GetValue("type").ToString().Equals("Live"))
                        {
                            liveItemId = (string)data["id"];
                            //LogUtil.LogWithDtTime(data["id"]);
                            break;
                        }

                        JArray channelItemStates = (JArray)data["channelItemStates"];
                        foreach (var channelItem in channelItemStates)
                        {
                            JObject b = (JObject)channelItem["state"];
                            if (b.GetValue("status").ToString().Equals("OnAir") && !((JObject)data["payload"]).GetValue("type").ToString().Equals("Live"))//check if type is not Live ((JObject)data["payload"]).GetValue("type").ToString()
                            {
                                onAir = true;
                                //LogUtil.LogWithDtTime(channelItem["itemId"]);
                            }
                        }
                    }


                }
            }
            catch(Exception ex)
            {
                log.log(ex.ToString());
            }
            return liveItemId;

        }

        public async Task takeLive(String rundownID, String liveItemId)
        {
            try
            {
                //this.refreshBearerToken();
                JArray array = new JArray();
                string cT = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd'T'hh:mm:ss.fffffff'Z'");
                JObject takeLive = new JObject(
                    new JProperty("account", "string"),
                    new JProperty("operation", "Take"),
                    new JProperty("networkId", rundownID),
                    new JProperty("itemId", liveItemId),
                    // new JProperty("startTime", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"))); 

                    new JProperty("startTime", cT));
                //new JProperty("startTime", "2022-10-01T12:00:00.0000000Z"));
               
                array.Add(takeLive);
                log.log("Body  " + array.ToString());

                String url = "https://apac1.gvampp.com/orchestration/api/v1/commands";
                using HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                String jsonString = JsonConvert.SerializeObject(array);
                //var requestContent = new StringContent(jsonString, Encoding.UTF8, "application/json");
                log.log("Sent Request to Take Live  Item !!" + liveItemId + ",Rundown:" + rundownID);
                HttpResponseMessage takeLiveresponse = await client.PutAsync(url, new StringContent(jsonString, Encoding.UTF8, "application/json"));
                log.log("Take Live Response is " + takeLiveresponse.StatusCode.ToString());
            }
            catch (Exception ex)
            {
                log.log(ex.ToString());
            }
        }

        private async Task takeItem(String nextItemID,String rundownId)
        {
            try
            {
                //this.refreshBearerToken();
                JArray array = new JArray();
                string cT = DateTime.Now.ToUniversalTime().AddMilliseconds(TAKE_STARTTIME_DELAY).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                JObject takeLive = new JObject(
                    // new JProperty("account", "string"),
                    new JProperty("operation", "Take"),
                    new JProperty("networkId", rundownId),
                    new JProperty("takeId", 0),
                    new JProperty("itemId", nextItemID),
                    // new JProperty("startTime", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"))); 

                    new JProperty("startTime", cT));
                //new JProperty("startTime", "2022-10-01T12:00:00.0000000Z"));

                array.Add(takeLive);
                log.log("Request Body  " + array.ToString());
                
                String url = $"{platformUrl}/orchestration/api/v1/commands";
                using HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                String jsonString = JsonConvert.SerializeObject(array);
                //var requestContent = new StringContent(jsonString, Encoding.UTF8, "application/json");
                log.log("Sent Request to Take Item: " + nextItemID);
                HttpResponseMessage takeItemresponse = await client.PutAsync(url, new StringContent(jsonString, Encoding.UTF8, "application/json"));
                log.log("Take Item result " + takeItemresponse.StatusCode.ToString());
            }
            catch (Exception ex)
            {
                log.log(ex.ToString());
            }
        }
        public async Task TakeNext(String runDownId)
        {
            //TimeSpan completionDuration = TimeSpan.FromSeconds(1); // Or change to variable in config.xml in milliseconds
            var stopwatch = Stopwatch.StartNew();
            var itmDetails = await getNextID(runDownId);
            String nextItemId, status;
            //var itmDetails = GetStatusAndItemId(itmStr);
            status = itmDetails.status;
            nextItemId = itmDetails.itemId;
            if (!nextItemId.Equals(""))
            {
                if (status.Equals("Ready"))
                { 
                    if (autoCueReadyItem)
                    {
                        //TO DO:if to "Cue on Ready" is true then cue and then take the item
                        var cued = await SendCueCommandAsync(runDownId, nextItemId);
                        if (!cued)
                        {
                            log.log($"Cue failed for item {nextItemId} in '{runDownId}'.");
                            return;
                        }
                        status = "Cued";
                        Task.Delay(TAKE_DELAY_AFTER_CUE).Wait();// slight delay to ensure item is in cued state before take command is sent
                    }
                    else
                    {
                        log.log("One item found in Ready State, item Id: " + nextItemId + "however it auto Cue for Ready Item is disabled, Requested to manually cue");
                    }
                }
                stopwatch.Stop();
                TimeSpan elapsed = stopwatch.Elapsed;
               if(elapsed< NEXT_ITEM_ID_FETCH_DURATION_IN_MILLS)
                {
                    // Wait for the remaining time
                    TimeSpan remaining = NEXT_ITEM_ID_FETCH_DURATION_IN_MILLS - elapsed; 
                    Console.WriteLine($"GetNext API response received early. Waiting for remaining {remaining.TotalMilliseconds} ms...");
                    log.log($"GetNext API response received early. Waiting for remaining {remaining.TotalMilliseconds} ms...");
                    await Task.Delay(remaining).ConfigureAwait(false);
                }
                else
                {
                    //API response exceeded completion time by {(elapsed - completionDuration).TotalSeconds:F2} seconds.
                    log.log($"Warning!! Fetching Next Item Id took more than {NEXT_ITEM_ID_FETCH_DURATION_IN_MILLS.TotalMilliseconds}  ms, it took  {elapsed.TotalMilliseconds}  ms");
                }
                if (status.Equals("Cued"))

                {

                    await takeItem(nextItemId, runDownId);
                }
                else
                {
                    log.log("Error:ItemId to take is " + nextItemId + " status is " + status
                        + ",Should be cued/Ready state to take!! Ignoring the request");
                }
            }
            else
                log.log("Warning!! No Valid Next Item id to take is found with status Cued to takeNext!!" + runDownId);

        }
        public static (string Status, string ItemId) GetStatusAndItemId(string itemDetails)
        {
            // Split the input string by the colon character ":"
            var parts = itemDetails.Split(':');

            if (parts.Length == 2)
            {
                return (Status: parts[0], ItemId: parts[1]);
            }
            else
            {
                throw new ArgumentException("Input string is not in the correct format 'Status:itemId'.");
            }
        }

        public async Task<bool> SendCueCommandAsync( string networkId, string itemId)
        {
            JArray array = new JArray();
            string url = $"{platformUrl}/orchestration/api/v1/commands";
            JObject cueItem = new JObject(
                    // new JProperty("account", "string"),
                    new JProperty("operation", "Cue"),
                    new JProperty("networkId", networkId),
                    new JProperty("itemId", itemId)
                    );

            array.Add(cueItem);
            log.log("Cue Request Body  " + array.ToString());

            //String url = $"{platformUrl}/orchestration/api/v1/commands";
            using HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            String jsonString = JsonConvert.SerializeObject(array);
            try
            {
                log.log("Cue Request Sent to Cue Item: " + itemId);
                HttpResponseMessage response = await client.PutAsync(url, new StringContent(jsonString, Encoding.UTF8, "application/json"));
                log.log("Cue Item result " + response.StatusCode.ToString());
                // Read response
                string responseBody = await response.Content.ReadAsStringAsync();
                
                log.log("Cue Item result" + response);
                if (response.IsSuccessStatusCode)
                {
                    log.log($"✅ Successfuly Cued Item :{itemId}, {responseBody}");
                    return true;
                }
                else
                {
                    log.log($"❌ Failed to Cue Item {itemId}: {response.StatusCode} - {responseBody}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.log($"❗ Error: {ex.Message}");
                return false;
            }
          
        }

        public async Task takeLiveV2(String rundownID, String liveItemId)
        {
            try
            {
                //this.refreshBearerToken();
                JArray array = new JArray();
                string cT = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd'T'hh:mm:ss'Z'");
                JObject takeLive = new JObject(
                    new JProperty("account", "string"),
                    new JProperty("operation", "Take"),
                    new JProperty("networkId", rundownID),
                    new JProperty("itemId", liveItemId),
                    // new JProperty("startTime", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"))); 

                    new JProperty("startTime", cT));
                //new JProperty("startTime", "2022-10-01T12:00:00.0000000Z"));

                array.Add(takeLive);
                log.log("Body  " + array.ToString());

                String url = "https://apac1.gvampp.com/ampp/control/api/v1/control/commit";
                using HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                String jsonString = JsonConvert.SerializeObject(array);
                //var requestContent = new StringContent(jsonString, Encoding.UTF8, "application/json");
                HttpResponseMessage takeLiveresponse = await client.PutAsync(url, new StringContent(jsonString, Encoding.UTF8, "application/json"));
                log.log("Take Live is " + takeLiveresponse.StatusCode.ToString());
            }
            catch (Exception ex)
            {
                log.log(ex.ToString());
            }
        }

        public static async Task  getJSONStr(String itemId,String networkId)
        {
            // Prepare the request payload
            var payload = new[]
            {
                new
                {
                    operation = "Cue",
                    networkId = networkId,
                    itemId = itemId
                }
            };

            // Convert to JSON string
            string jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            string result = await content.ReadAsStringAsync();
            log.log("Sending Command ->" + result);
        }
        
        private async Task<(string itemId, string status)> getNextID(String rundownID)
        {
            String nextItemId = "", nextItemStatus;
            while (true)
            {
                try
                {
                    StringBuilder urlBuilder = new StringBuilder();
                    //String url = $"{platformUrl}/orchestration/api/v1/schedules";
                    urlBuilder.Append($"{platformUrl}/orchestration/api/v1/schedules/{rundownID}/next?itemsToReturn=1&sameParent=false");
                    if (!string.IsNullOrEmpty(nextItemId))
                    {
                        urlBuilder.Append($"&itemId={nextItemId}");
                    }

                    string url = urlBuilder.ToString();
                    log.log("Requesting next item using URL: " + url);

                    using HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri(url);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                    log.log($"Sent request to Query next Item to take for networkID: {rundownID} itemId: {nextItemId}" );

                    HttpResponseMessage response = await client.GetAsync(url);
                    log.log("Response received - " + response.StatusCode);

                    if (response.IsSuccessStatusCode)
                    {
                        String responseBody = await response.Content.ReadAsStringAsync();
                        //log.log("Response Body:\n" + responseBody);
                        JObject jsonObject = JObject.Parse(responseBody);

                        JArray itemsArray = (JArray)jsonObject.GetValue("items");

                        if (itemsArray != null && itemsArray.Count > 0)
                        {
                            // Start recursive parsing of the items array
                            var nextItemDetails = await GetNextItemRecursive(itemsArray);
                            nextItemId = nextItemDetails.itemId;
                            nextItemStatus = nextItemDetails.status;
                            if (nextItemStatus.Equals("Cued") || nextItemStatus.Equals("Ready"))
                            {
                                return nextItemDetails;
                            }
                            else if (String.IsNullOrEmpty(nextItemId))
                            {
                                return ("", "Missing");
                            }
                        }
                        else
                        {
                            log.log("No valid items found in the response!!");
                            break;
                        }
                    }

                    else
                    {
                        log.log("Response to API for getNextId received:" + response.StatusCode + "\n" + response.Content);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    log.log(ex.ToString());
                    break;
                }
            }
            return ("","");
        }
        private static async Task<(string itemId, string status)> GetNextItemRecursive(JArray itemsArray/*, String statustoCheck*/)
        {
            string lastItemId = "";
            string lastStatus = "";

            foreach (var item in itemsArray)
            {
                // Check if the item contains another "items" array (nested structure)
                JArray innerItemsArray = (JArray)item["items"];

                if (innerItemsArray != null && innerItemsArray.Count > 0)
                {
                    // Recursively process the inner "items" array
                    var foundItem = await GetNextItemRecursive(innerItemsArray);
                    if (!string.IsNullOrEmpty(foundItem.itemId))
                    {
                        return foundItem; // Return if a valid item is found
                    }
                }
                else
                {
                    // If there's no nested "items" array, process this item
                    string nextItemId = item["id"]?.ToString();
                    JArray channelItemStates = (JArray)item["channelItemStates"];

                    if (channelItemStates != null && channelItemStates.Count > 0)
                    {
                        JObject stateObj = (JObject)channelItemStates[0];
                        JObject state = (JObject)stateObj["state"];
                        string status = state.GetValue("status")?.ToString();
                        string nowNextStatus = state.GetValue("nowNextStatus")?.ToString();
                        nextItemId = stateObj["itemId"]?.ToString();
                        //if (status == statustoCheck)
                        if ((status == "Cued" /*&& nowNextStatus == "Next"*/) || status == "Ready")
                        {
                            log.log($"Valid Next Item id to take: {nextItemId}, Status: {status}");
                            return (nextItemId, status); // Return the valid item
                        }

                        // Track the last item in case no valid item is found
                        lastItemId = nextItemId;
                        lastStatus = status;

                        log.log($"Skipping item {nextItemId} - status: {status}, nowNextStatus: {nowNextStatus}");
                    }
                    else
                    {
                        log.log($"No channelItemStates found for item {nextItemId}");
                    }
                }
            }

            // Return the last processed item if no valid match is found
            log.log($"Returning last processed item: {lastItemId}, Status: {lastStatus}");
            return (lastItemId, lastStatus);
        }

        // Recursive function to find the next item with state "Cued" and nowNextStatus "Next"
        private static async Task<String> GetNextItemRecursiveOld(JArray itemsArray)
        {
            foreach (var item in itemsArray)
            {
                // Check if the item contains another "items" array (nested structure)
                JArray innerItemsArray = (JArray)item["items"];

                if (innerItemsArray != null && innerItemsArray.Count > 0)
                {
                    // Recursively process the inner "items" array
                    String foundItem = await GetNextItemRecursiveOld(innerItemsArray);
                    if (!string.IsNullOrEmpty(foundItem))
                    {
                        return foundItem; // Return if a valid item is found
                    }
                }
                else
                {
                    // If there's no nested "items" array, process this item
                    String nextItemId = item["id"]?.ToString();

                    // Check if "channelItemStates" exists and contains elements
                    JArray channelItemStates = (JArray)item["channelItemStates"];
                    if (channelItemStates != null && channelItemStates.Count > 0)
                    {
                        JObject stateObj = (JObject)channelItemStates[0];
                        JObject state = (JObject)stateObj["state"];
                        String status = state.GetValue("status")?.ToString();
                        String nowNextStatus = state.GetValue("nowNextStatus")?.ToString();
                        nextItemId = stateObj["itemId"]?.ToString();
                        if (status == "Cued" && nowNextStatus == "Next")
                        {
                            log.log("Valid Next Item id to take: " + nextItemId + ", Status: " + status + ", nowNextStatus: " + nowNextStatus);
                            return nextItemId; // Return only if it meets the criteria
                        }
                        else
                        {
                            log.log("Skipping item " + nextItemId + " - status: " + status + ", nowNextStatus: " + nowNextStatus);
                        }
                    }
                    else
                    {
                        log.log("No channelItemStates found for item " + nextItemId);
                    }
                }
            }
            //log.log("Warning!! No Valid Next Item id to take is found with status Cued to takeNext!!");

            return ""; // Return empty string if no valid item found
        }
    /*private async Task<string> GetNextIDAsync(string rundownID)
    {
        string foundResult = string.Empty;
        string currentItemId = null; // For the first API call, we do NOT pass itemId.

        try
        {
            while (true)
            {
                // 1) Build the URL
                //    - For first call, no `itemId` param
                //    - For subsequent calls, append `&itemId={currentItemId}`
                StringBuilder urlBuilder = new StringBuilder();
                urlBuilder.Append($"https://apac1.gvampp.com/orchestration/api/v1/schedules/{rundownID}/next?itemsToReturn=1&sameParent=false");
                if (!string.IsNullOrEmpty(currentItemId))
                {
                    urlBuilder.Append($"&itemId={currentItemId}");
                }

                string url = urlBuilder.ToString();
                log.log("Requesting next item using URL: " + url);

                // 2) Make the HTTP request
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                HttpResponseMessage response = await client.GetAsync(url);
                log.log("Response received - " + response.StatusCode);

                // 3) Check the response
                if (!response.IsSuccessStatusCode)
                {
                    // If not success, log and stop trying
                    log.log($"Failed API call: {response.StatusCode}\n{await response.Content.ReadAsStringAsync()}");
                    break;
                }

                // 4) Parse the JSON
                string responseBody = await response.Content.ReadAsStringAsync();
                log.log("Response Body:\n" + responseBody);

                JObject jsonObject = JObject.Parse(responseBody);
                JArray itemsArray = (JArray)jsonObject.GetValue("items");

                if (itemsArray == null || itemsArray.Count == 0)
                {
                    // No items found - no more to process
                    log.log("No items returned. Stopping search.");
                    break;
                }

                // 5) Potentially multiple items in the array, but typically 1 if itemsToReturn=1
                bool foundMatchingItem = false;
                string actualItemId ;

                foreach (var item in itemsArray)
                {
                    // Extract the itemId from the top-level item
                    string itemId = item["id"]?.ToString() ?? string.Empty;

                    // Check for channelItemStates
                    JArray channelItemStates = (JArray)item["channelItemStates"];
                    if (channelItemStates == null || channelItemStates.Count == 0)
                    {
                        log.log("No channelItemStates for item " + itemId + ". Moving on.");
                        // We'll just loop to the next item
                        continue;
                    }

                    // Typically there's at least one state object
                    JObject stateObj = (JObject)channelItemStates[0];
                    JObject state = (JObject)stateObj["state"];
                    string status = state?["status"]?.ToString();
                    // Some APIs store itemId in channelItemStates array; confirm or adapt as needed
                    actualItemId = stateObj["itemId"]?.ToString() ?? itemId;

                    log.log($"Checking item {actualItemId}: status={status}");

                    // 6) Check if status is "Cued" or "Ready"
                    if (status == "Cued" || status == "Ready")
                    {
                        // Found what we want -> build the result as "itemId:status"
                        foundResult = $"{actualItemId}:{status}";
                        log.log("Found a matching item: " + foundResult);
                        foundMatchingItem = true;
                        break;
                    }
                    else
                    {
                        log.log($"Skipping item {actualItemId} - status: {status}");
                    }
                }

                if (foundMatchingItem)
                {
                    // We can stop searching
                    break;
                }

                // 7) If we reached here, none in this batch matched "Cued" or "Ready".
                //    We'll pick the *last* item from itemsArray to feed as the next itemId
                //    to continue searching beyond it. (Or adapt logic as needed.)
                var lastItem = itemsArray[itemsArray.Count - 1];
                currentItemId = lastItem["id"]?.ToString() ?? string.Empty;
                // If channelItemStates is where itemId truly is, fetch from there:
                // currentItemId = lastItem["channelItemStates"]?[0]?["itemId"]?.ToString() ?? currentItemId;

                // 8) If for some reason that item is empty, break to avoid infinite loop
                if (string.IsNullOrEmpty(currentItemId))
                {
                    log.log("No further itemId to request. Ending.");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            log.log("Error in GetNextIDAsync: " + ex);
        }

        // Return either "itemId:status" or empty if none found
        return foundResult;
    }*/

    public static async Task<(String,String)> getNextIDTestNew(String jsonStr)
        {
            var nextItemDetails= ( "","");
            try
            {
                
                {
                    String responseBody = jsonStr; //"{\"id\":\"93739936-94db-4708-8b46-dd52e279259b\",\"name\":\"93739936-94db-4708-8b46-dd52e279259b\",\"type\":\"Network\",\"items\":[{\"id\":\"04d6b218-188c-8375-a20f-5fbac00866b1\",\"name\":\"Ice Cold Killers (Season 5) Year 2017\",\"payload\":{\"type\":\"Show\",\"triggerable\":false,\"timeDetails\":{\"startTime\":\"\",\"notionalStartTime\":\"2025-03-03T17:20:45.0000000\",\"duration\":\"00:52:30.0000000\",\"timeMode\":\"Auto\",\"externalTriggerWindow\":{}}},\"items\":[{\"id\":\"477851c2-30cc-e587-2ffe-995316472a1f\",\"name\":\"Break 0 : 03/03 17:42:45\",\"payload\":{\"type\":\"Break\",\"timeDetails\":{\"duration\":\"00:04:00.0000000\",\"timeMode\":\"Manual\",\"externalTriggerWindow\":{}},\"triggerable\":true},\"items\":[{\"id\":\"7b5b35e4-9068-454f-723a-1177829c3e5d\",\"name\":\"BAY AUDIOLOGY\",\"payload\":{\"ibmsEventId\":410278437,\"type\":\"Video\",\"timeDetails\":{\"duration\":\"00:00:15.0000000\",\"timeMode\":\"Auto\",\"fluidDuration\":false,\"externalTriggerWindow\":{}},\"materialId\":\"AC083465\",\"materialType\":\"Commercial\",\"transitionDetails\":{\"type\":\"Cut\",\"duration\":\"00:00:00.0000000\"},\"subtitlesSource\":\"None\",\"triggerable\":false,\"aggregatedDetails\":{\"duration\":\"00:00:15.0000000\",\"inPoint\":\"00:01:00.0000000\",\"outPoint\":\"00:01:15.0000000\"}},\"items\":[],\"alternatives\":[],\"channelItemStates\":[{\"itemId\":\"7b5b35e4-9068-454f-723a-1177829c3e5d\",\"timeStamp\":\"2025-03-03T06:12:28.0085201Z\",\"channelId\":\"efc37e09-02e1-4a76-9ca9-f3db6f8ae796\",\"state\":{\"status\":\"Cued\",\"reason\":\"\",\"startTime\":\"2025-03-03T06:27:26.3600000Z\",\"duration\":\"00:00:15.0000000\",\"holdStatus\":\"Off\",\"isOnBlackHold\":false,\"nowNextStatus\":\"Next\",\"inPoint\":\"00:01:00.0000000\",\"frameRate\":\"FPS25\",\"isPrimaryDeparture\":true,\"isRouted\":false,\"contentCachingStatus\":\"Completed\",\"contentCachingErrorMessage\":\"\",\"audioStates\":[{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#1', Rule is 'P#1,SILENCE'\\r\\n\",\"output\":1},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#2,SILENCE'\\r\\n\",\"output\":2},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#3,SILENCE'\\r\\n\",\"output\":3},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#4,SILENCE'\\r\\n\",\"output\":4},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#5,SILENCE'\\r\\n\",\"output\":5},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#6,SILENCE'\\r\\n\",\"output\":6},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#7,SILENCE'\\r\\n\",\"output\":7},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#8,SILENCE'\\r\\n\",\"output\":8},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#9,SILENCE'\\r\\n\",\"output\":9},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#10,SILENCE'\\r\\n\",\"output\":10},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#11,SILENCE'\\r\\n\",\"output\":11},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#12,SILENCE'\\r\\n\",\"output\":12},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#13,SILENCE'\\r\\n\",\"output\":13},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#14,SILENCE'\\r\\n\",\"output\":14},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#15,SILENCE'\\r\\n\",\"output\":15},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#16,SILENCE'\\r\\n\",\"output\":16}],\"subtitlesStates\":[],\"rootItemId\":\"04d6b218-188c-8375-a20f-5fbac00866b1\"}},{\"itemId\":\"7b5b35e4-9068-454f-723a-1177829c3e5d\",\"timeStamp\":\"2025-03-03T06:12:32.4098303Z\",\"channelId\":\"d544da32-00e5-460c-8759-cefeb7d59ed0\",\"state\":{\"status\":\"Cued\",\"reason\":\"\",\"startTime\":\"2025-03-03T06:27:26.3600000Z\",\"duration\":\"00:00:15.0000000\",\"holdStatus\":\"Off\",\"isOnBlackHold\":false,\"nowNextStatus\":\"Next\",\"inPoint\":\"00:01:00.0000000\",\"frameRate\":\"FPS25\",\"isPrimaryDeparture\":false,\"isRouted\":false,\"contentCachingStatus\":\"Completed\",\"contentCachingErrorMessage\":\"\",\"audioStates\":[{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#1', Rule is 'P#1,SILENCE'\\r\\n\",\"output\":1},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#2,SILENCE'\\r\\n\",\"output\":2},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#3,SILENCE'\\r\\n\",\"output\":3},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#4,SILENCE'\\r\\n\",\"output\":4},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#5,SILENCE'\\r\\n\",\"output\":5},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#6,SILENCE'\\r\\n\",\"output\":6},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#7,SILENCE'\\r\\n\",\"output\":7},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#8,SILENCE'\\r\\n\",\"output\":8},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#9,SILENCE'\\r\\n\",\"output\":9},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#10,SILENCE'\\r\\n\",\"output\":10},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#11,SILENCE'\\r\\n\",\"output\":11},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#12,SILENCE'\\r\\n\",\"output\":12},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#13,SILENCE'\\r\\n\",\"output\":13},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#14,SILENCE'\\r\\n\",\"output\":14},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#15,SILENCE'\\r\\n\",\"output\":15},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#16,SILENCE'\\r\\n\",\"output\":16}],\"subtitlesStates\":[],\"rootItemId\":\"04d6b218-188c-8375-a20f-5fbac00866b1\"}}],\"version\":0}],\"alternatives\":[],\"channelItemStates\":[{\"itemId\":\"477851c2-30cc-e587-2ffe-995316472a1f\",\"timeStamp\":\"2025-03-03T06:12:28.0240591Z\",\"channelId\":\"efc37e09-02e1-4a76-9ca9-f3db6f8ae796\",\"state\":{\"status\":\"Missing\",\"reason\":\"\",\"startTime\":\"2025-03-03T06:27:26.3600000Z\",\"duration\":\"00:04:00.0000000\",\"holdStatus\":\"Off\",\"isOnBlackHold\":false,\"nowNextStatus\":\"None\",\"frameRate\":\"None\",\"isPrimaryDeparture\":true,\"isRouted\":false,\"audioStates\":[{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#1', Rule is 'P#1,SILENCE'\\r\\n\",\"output\":1},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#2,SILENCE'\\r\\n\",\"output\":2},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#3,SILENCE'\\r\\n\",\"output\":3},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#4,SILENCE'\\r\\n\",\"output\":4},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#5,SILENCE'\\r\\n\",\"output\":5},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#6,SILENCE'\\r\\n\",\"output\":6},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#7,SILENCE'\\r\\n\",\"output\":7},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#8,SILENCE'\\r\\n\",\"output\":8},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#9,SILENCE'\\r\\n\",\"output\":9},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#10,SILENCE'\\r\\n\",\"output\":10},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#11,SILENCE'\\r\\n\",\"output\":11},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#12,SILENCE'\\r\\n\",\"output\":12},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#13,SILENCE'\\r\\n\",\"output\":13},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#14,SILENCE'\\r\\n\",\"output\":14},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#15,SILENCE'\\r\\n\",\"output\":15},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#16,SILENCE'\\r\\n\",\"output\":16}],\"subtitlesStates\":[],\"rootItemId\":\"04d6b218-188c-8375-a20f-5fbac00866b1\"}},{\"itemId\":\"477851c2-30cc-e587-2ffe-995316472a1f\",\"timeStamp\":\"2025-03-03T06:12:32.4302220Z\",\"channelId\":\"d544da32-00e5-460c-8759-cefeb7d59ed0\",\"state\":{\"status\":\"Missing\",\"reason\":\"\",\"startTime\":\"2025-03-03T06:27:26.3600000Z\",\"duration\":\"00:04:00.0000000\",\"holdStatus\":\"Off\",\"isOnBlackHold\":false,\"nowNextStatus\":\"None\",\"frameRate\":\"None\",\"isPrimaryDeparture\":false,\"isRouted\":false,\"audioStates\":[{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#1', Rule is 'P#1,SILENCE'\\r\\n\",\"output\":1},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#2,SILENCE'\\r\\n\",\"output\":2},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#3,SILENCE'\\r\\n\",\"output\":3},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#4,SILENCE'\\r\\n\",\"output\":4},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#5,SILENCE'\\r\\n\",\"output\":5},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#6,SILENCE'\\r\\n\",\"output\":6},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#7,SILENCE'\\r\\n\",\"output\":7},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#8,SILENCE'\\r\\n\",\"output\":8},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#9,SILENCE'\\r\\n\",\"output\":9},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#10,SILENCE'\\r\\n\",\"output\":10},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#11,SILENCE'\\r\\n\",\"output\":11},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#12,SILENCE'\\r\\n\",\"output\":12},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#13,SILENCE'\\r\\n\",\"output\":13},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#14,SILENCE'\\r\\n\",\"output\":14},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#15,SILENCE'\\r\\n\",\"output\":15},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#16,SILENCE'\\r\\n\",\"output\":16}],\"subtitlesStates\":[],\"rootItemId\":\"04d6b218-188c-8375-a20f-5fbac00866b1\"}}],\"recorderStates\":[],\"version\":0}],\"alternatives\":[],\"channelItemStates\":[{\"itemId\":\"04d6b218-188c-8375-a20f-5fbac00866b1\",\"timeStamp\":\"2025-03-03T06:12:28.0215131Z\",\"channelId\":\"efc37e09-02e1-4a76-9ca9-f3db6f8ae796\",\"state\":{\"status\":\"OnAir\",\"reason\":\"\",\"startTime\":\"2025-03-03T04:20:26.3600000Z\",\"duration\":\"02:37:30.0000000\",\"holdStatus\":\"Off\",\"isOnBlackHold\":false,\"nowNextStatus\":\"None\",\"frameRate\":\"None\",\"isPrimaryDeparture\":true,\"isRouted\":false,\"audioStates\":[{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#1'\\r\\n\",\"output\":1},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#2'\\r\\n\",\"output\":2},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#3'\\r\\n\",\"output\":3},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#4'\\r\\n\",\"output\":4},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#5'\\r\\n\",\"output\":5},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#6'\\r\\n\",\"output\":6},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#7'\\r\\n\",\"output\":7},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#8'\\r\\n\",\"output\":8},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#9'\\r\\n\",\"output\":9},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#10'\\r\\n\",\"output\":10},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#11'\\r\\n\",\"output\":11},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#12'\\r\\n\",\"output\":12},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#13'\\r\\n\",\"output\":13},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#14'\\r\\n\",\"output\":14},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#15'\\r\\n\",\"output\":15},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#16'\\r\\n\",\"output\":16}],\"subtitlesStates\":[],\"rootItemId\":\"04d6b218-188c-8375-a20f-5fbac00866b1\"}},{\"itemId\":\"04d6b218-188c-8375-a20f-5fbac00866b1\",\"timeStamp\":\"2025-03-03T06:12:32.4281131Z\",\"channelId\":\"d544da32-00e5-460c-8759-cefeb7d59ed0\",\"state\":{\"status\":\"OnAir\",\"reason\":\"\",\"startTime\":\"2025-03-03T04:20:26.3600000Z\",\"duration\":\"02:37:30.0000000\",\"holdStatus\":\"Off\",\"isOnBlackHold\":false,\"nowNextStatus\":\"None\",\"frameRate\":\"None\",\"isPrimaryDeparture\":false,\"isRouted\":false,\"audioStates\":[{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#1'\\r\\n\",\"output\":1},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#2'\\r\\n\",\"output\":2},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#3'\\r\\n\",\"output\":3},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#4'\\r\\n\",\"output\":4},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#5'\\r\\n\",\"output\":5},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#6'\\r\\n\",\"output\":6},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#7'\\r\\n\",\"output\":7},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#8'\\r\\n\",\"output\":8},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#9'\\r\\n\",\"output\":9},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#10'\\r\\n\",\"output\":10},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#11'\\r\\n\",\"output\":11},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#12'\\r\\n\",\"output\":12},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#13'\\r\\n\",\"output\":13},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#14'\\r\\n\",\"output\":14},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#15'\\r\\n\",\"output\":15},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#16'\\r\\n\",\"output\":16}],\"subtitlesStates\":[],\"rootItemId\":\"04d6b218-188c-8375-a20f-5fbac00866b1\"}}],\"recorderStates\":[],\"version\":0}],\"creationId\":\"00000000-0000-0000-0000-000000000000\",\"version\":0}";
                    JObject jsonObject = JObject.Parse(responseBody);

                    JArray itemsArray = (JArray)jsonObject.GetValue("items");

                    if (itemsArray != null && itemsArray.Count > 0)
                    {
                        // Start recursive parsing of the items array
                        nextItemDetails = await GetNextItemRecursive(itemsArray);
                    }
                    else
                    {
                        log.log("No valid items found in the response.");
                    }
                }
            }
            catch (Exception ex)
            {
                log.log(ex.ToString());
            }
            return nextItemDetails;
        }
        public static async Task<String> getNextIDTest(String rundownID)
        {
            String nextItemId = "",status="";
            try
            {
                
                {
                    String responseBody = "{\"id\":\"93739936-94db-4708-8b46-dd52e279259b\",\"name\":\"93739936-94db-4708-8b46-dd52e279259b\",\"type\":\"Network\",\"items\":[{\"id\":\"d41591dc-3e2e-eee8-6047-fc3b5983f7f9\",\"name\":\"MANUAL INSERT\",\"payload\":{\"type\":\"Show\",\"timeDetails\":{\"duration\":\"00:01:00.0000000\",\"timeMode\":\"Manual\"}},\"items\":[{\"id\":\"7859aefb-22b8-e0a0-999e-a55134989a7d\",\"name\":\"ANIMATES BRAND\",\"payload\":{\"type\":\"Video\",\"timeDetails\":{\"timeMode\":\"Auto\",\"fluidDuration\":true,\"duration\":\"00:00:15.0000000\"},\"materialId\":\"AC087354\",\"materialType\":\"Unknown\",\"transitionDetails\":{\"type\":\"Cut\"},\"aggregatedDetails\":{\"duration\":\"00:00:15.0000000\",\"inPoint\":\"00:01:00.0000000\",\"outPoint\":\"00:01:15.0000000\"}},\"items\":[],\"alternatives\":[],\"channelItemStates\":[{\"itemId\":\"7859aefb-22b8-e0a0-999e-a55134989a7d\",\"timeStamp\":\"2025-02-19T03:20:52.0339616Z\",\"channelId\":\"efc37e09-02e1-4a76-9ca9-f3db6f8ae796\",\"state\":{\"status\":\"Ready\",\"reason\":\"\",\"startTime\":\"2025-02-19T11:02:40.5200000Z\",\"duration\":\"00:00:15.0000000\",\"holdStatus\":\"Off\",\"isOnBlackHold\":false,\"nowNextStatus\":\"Next\",\"inPoint\":\"00:01:00.0000000\",\"frameRate\":\"FPS25\",\"isPrimaryDeparture\":true,\"isRouted\":false,\"audioStates\":[{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#1', Rule is 'P#1,SILENCE'\\r\\n\",\"output\":1},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#2', Rule is 'P#2,SILENCE'\\r\\n\",\"output\":2},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#3', Rule is 'P#3,SILENCE'\\r\\n\",\"output\":3},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#4', Rule is 'P#4,SILENCE'\\r\\n\",\"output\":4},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#5,SILENCE'\\r\\n\",\"output\":5},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#6,SILENCE'\\r\\n\",\"output\":6},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#7,SILENCE'\\r\\n\",\"output\":7},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#8,SILENCE'\\r\\n\",\"output\":8},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#9,SILENCE'\\r\\n\",\"output\":9},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#10,SILENCE'\\r\\n\",\"output\":10},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#11,SILENCE'\\r\\n\",\"output\":11},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#12,SILENCE'\\r\\n\",\"output\":12},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#13,SILENCE'\\r\\n\",\"output\":13},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#14,SILENCE'\\r\\n\",\"output\":14},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#15,SILENCE'\\r\\n\",\"output\":15},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#16,SILENCE'\\r\\n\",\"output\":16}],\"subtitlesStates\":[],\"rootItemId\":\"d41591dc-3e2e-eee8-6047-fc3b5983f7f9\"}},{\"itemId\":\"7859aefb-22b8-e0a0-999e-a55134989a7d\",\"timeStamp\":\"2025-02-19T03:21:36.9301769Z\",\"channelId\":\"d544da32-00e5-460c-8759-cefeb7d59ed0\",\"state\":{\"status\":\"Ready\",\"reason\":\"\",\"startTime\":\"2025-02-19T11:02:40.5200000Z\",\"duration\":\"00:00:15.0000000\",\"holdStatus\":\"Off\",\"isOnBlackHold\":false,\"nowNextStatus\":\"Next\",\"inPoint\":\"00:01:00.0000000\",\"frameRate\":\"FPS25\",\"isPrimaryDeparture\":false,\"isRouted\":false,\"audioStates\":[{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#1', Rule is 'P#1,SILENCE'\\r\\n\",\"output\":1},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#2', Rule is 'P#2,SILENCE'\\r\\n\",\"output\":2},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#3', Rule is 'P#3,SILENCE'\\r\\n\",\"output\":3},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'P#4', Rule is 'P#4,SILENCE'\\r\\n\",\"output\":4},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#5,SILENCE'\\r\\n\",\"output\":5},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#6,SILENCE'\\r\\n\",\"output\":6},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#7,SILENCE'\\r\\n\",\"output\":7},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#8,SILENCE'\\r\\n\",\"output\":8},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#9,SILENCE'\\r\\n\",\"output\":9},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#10,SILENCE'\\r\\n\",\"output\":10},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#11,SILENCE'\\r\\n\",\"output\":11},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#12,SILENCE'\\r\\n\",\"output\":12},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#13,SILENCE'\\r\\n\",\"output\":13},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#14,SILENCE'\\r\\n\",\"output\":14},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#15,SILENCE'\\r\\n\",\"output\":15},{\"status\":\"Ready\",\"reason\":\"Matched on Preferred Rule 'SILENCE', Rule is 'P#16,SILENCE'\\r\\n\",\"output\":16}],\"subtitlesStates\":[],\"rootItemId\":\"d41591dc-3e2e-eee8-6047-fc3b5983f7f9\"}}],\"version\":1}],\"alternatives\":[],\"channelItemStates\":[{\"itemId\":\"d41591dc-3e2e-eee8-6047-fc3b5983f7f9\",\"timeStamp\":\"2025-02-19T03:20:52.0792047Z\",\"channelId\":\"efc37e09-02e1-4a76-9ca9-f3db6f8ae796\",\"state\":{\"status\":\"OnAir\",\"reason\":\"\",\"startTime\":\"2025-02-16T21:57:20.8000000Z\",\"duration\":\"121:07:11.3200000\",\"holdStatus\":\"Off\",\"isOnBlackHold\":false,\"nowNextStatus\":\"None\",\"frameRate\":\"None\",\"isPrimaryDeparture\":true,\"isRouted\":false,\"audioStates\":[{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#1'\\r\\n\",\"output\":1},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#2'\\r\\n\",\"output\":2},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#3'\\r\\n\",\"output\":3},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#4'\\r\\n\",\"output\":4},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#5'\\r\\n\",\"output\":5},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#6'\\r\\n\",\"output\":6},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#7'\\r\\n\",\"output\":7},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#8'\\r\\n\",\"output\":8},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#9'\\r\\n\",\"output\":9},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#10'\\r\\n\",\"output\":10},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#11'\\r\\n\",\"output\":11},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#12'\\r\\n\",\"output\":12},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#13'\\r\\n\",\"output\":13},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#14'\\r\\n\",\"output\":14},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#15'\\r\\n\",\"output\":15},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#16'\\r\\n\",\"output\":16}],\"subtitlesStates\":[],\"rootItemId\":\"d41591dc-3e2e-eee8-6047-fc3b5983f7f9\"}},{\"itemId\":\"d41591dc-3e2e-eee8-6047-fc3b5983f7f9\",\"timeStamp\":\"2025-02-19T03:21:37.0008374Z\",\"channelId\":\"d544da32-00e5-460c-8759-cefeb7d59ed0\",\"state\":{\"status\":\"OnAir\",\"reason\":\"\",\"startTime\":\"2025-02-16T21:57:20.8800000Z\",\"duration\":\"111:11:49.7200000\",\"holdStatus\":\"Off\",\"isOnBlackHold\":false,\"nowNextStatus\":\"None\",\"frameRate\":\"None\",\"isPrimaryDeparture\":false,\"isRouted\":false,\"audioStates\":[{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#1'\\r\\n\",\"output\":1},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#2'\\r\\n\",\"output\":2},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#3'\\r\\n\",\"output\":3},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#4'\\r\\n\",\"output\":4},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#5'\\r\\n\",\"output\":5},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#6'\\r\\n\",\"output\":6},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#7'\\r\\n\",\"output\":7},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#8'\\r\\n\",\"output\":8},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#9'\\r\\n\",\"output\":9},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#10'\\r\\n\",\"output\":10},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#11'\\r\\n\",\"output\":11},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#12'\\r\\n\",\"output\":12},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#13'\\r\\n\",\"output\":13},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#14'\\r\\n\",\"output\":14},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#15'\\r\\n\",\"output\":15},{\"status\":\"Ready\",\"reason\":\"Manually overridden to 'P#16'\\r\\n\",\"output\":16}],\"subtitlesStates\":[],\"rootItemId\":\"d41591dc-3e2e-eee8-6047-fc3b5983f7f9\"}}],\"recorderStates\":[],\"version\":18}],\"creationId\":\"00000000-0000-0000-0000-000000000000\",\"version\":0}";
                    /*JObject o = JObject.Parse(responseBody);
                    JArray jArray = (JArray)o.GetValue("items");
     
                    foreach (var data in jArray)
                    {
                        nextItemId = (string)data["itemId"];
                        log.log("Next Item id to take :" + nextItemId);
                        break;
                    }*/

                    // Parse the JSON string into a JObject
                    JObject jsonObject = JObject.Parse(responseBody);
                    // Access the 2nd container inside the "items" array (index 1)
                    JArray itemsArray = (JArray)jsonObject.GetValue("items");
                    try
                    {

                        JObject secondContainer = (JObject)itemsArray[0];
                        // Access the "items" array inside the second container
                        JArray innerItemsArray = (JArray)secondContainer["items"];
                        JObject state;
                        // Iterate through the inner items and extract the "id"
                        foreach (JObject innerItem in innerItemsArray)
                        {
                            nextItemId = innerItem["id"].ToString();
                            state = (JObject)((JArray)innerItem["channelItemStates"])[0];
                            state = (JObject)state["state"];
                            status = state.GetValue("status").ToString();
                            log.log("Next Item id to take :" + nextItemId + ",Status:"+status);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // check if the item is not wrapped inside another container of items
                        foreach (var data in itemsArray)
                        {
                            nextItemId = (string)data["itemId"];
                            log.log("Next Item id to take :" + nextItemId);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.log(ex.ToString());
            }
            return status+":"+nextItemId;

        }

        public void AttachPeer(IMirrorEndpoint peer) => _peer = peer;

        public Task Internal_TakeAsync(string sourceOrKey)
        {
            return Task.Run(async () => { await takeItem(sourceOrKey,networkID).ConfigureAwait(false); });
        }
        //public async Task  Internal_TakeNextAsync() => Task.Run(() => TakeNext(networkID));
        public Task Internal_TakeNextAsync()
        {
            return Task.Run(async () => { await TakeNext(this.networkID).ConfigureAwait(false); });
        }



        // ===== External entrypoints (called by AMPP/UI/socket handlers) =====
        /// <summary>
        /// Execute TAKE locally and on the peer in parallel.
        /// If awaitPeer = true, waits for both; otherwise returns after local completes.
        /// Peer errors never fail the local op.
        /// </summary>
        public async Task MirrorAware_TakeAsync(string sourceOrKey, bool awaitPeer = false)
        {
            var localTask = Internal_TakeAsync(sourceOrKey);
            var peerTask  = _peer != null ? _peer.Internal_TakeAsync(sourceOrKey) : Task.CompletedTask;
            if (awaitPeer)
            {
                await Task.WhenAll(localTask, IgnorePeerErrors(peerTask)).ConfigureAwait(false);
            }
            else
            {
                _ = IgnorePeerErrors(peerTask);
                await localTask.ConfigureAwait(false);
            }
        }
        [AMPPCommand("takenextmirror", Schema = TakeNextOnPairNetworkSchema, Version = "1.0", Markdown = "Markdown.takepair.md")]
        public async Task takeNextMirror(JObject payload, string reconKey)
        {
            bool awaitPeer = false;

            log.log("Received takenext command to take on master & backup");
            //logTimeStamp.log(0, "takenext Received!!");//Log the timeStamp
            //this.refreshBearerToken();
            log.log(JsonConvert.SerializeObject(payload, Newtonsoft.Json.Formatting.Indented));

            // Do some work 
            var demoState = payload.ToObject<DemoState>();
            //sendIPTrigger(payload, reconKey);
            if (demoState.Active == true)
            {

                var localTask = Internal_TakeNextAsync();
                var peerTask = _peer != null ? _peer.Internal_TakeNextAsync() : Task.CompletedTask;
                if (awaitPeer)
                {
                    await Task.WhenAll(localTask, IgnorePeerErrors(peerTask)).ConfigureAwait(false);
                }
                else
                {
                    _ = IgnorePeerErrors(peerTask);
                    await localTask.ConfigureAwait(false);
                }
            }
            else
            {
                log.log("Channel is not Active. Ignoring takenext command");
            }
        }
        private static async Task IgnorePeerErrors(Task t)
        {
            try { await t.ConfigureAwait(false); }
            catch { /* log and continue */ }
        }

        // ===== Helpers for AMPP Control "commit" commands =====
        private async Task CommitControlCommandAsync(string command, string reconKey)
        {
            try
            {
                // Prefer ChannelName as AMPP workload id; fall back to networkID if unset.
                var workload = !string.IsNullOrWhiteSpace(this.ChannelName) ? this.ChannelName : this.networkID;

                var url = $"{platformUrl}/ampp/control/api/v1/control/commit";
                var payloadObj = new JObject
                {
                    ["application"] = "Channel",
                    ["command"] = command,        // "cuenext" or "skipnext"
                    ["workload"] = workload,       // workload GUID/alias; matches your takenext mirror context
                    ["formData"] = "{}",
                    ["reconKey"] = reconKey        // pass through what AMPP invokes us with
                };

                var json = payloadObj.ToString(Newtonsoft.Json.Formatting.None);
                log.log($"Sending Control Commit -> {json}");

                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(url);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                    var resp = await client.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"))
                                           .ConfigureAwait(false);
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                    log.log($"Control Commit '{command}' => {resp.StatusCode}");
                    if (!resp.IsSuccessStatusCode)
                    {
                        log.log($"❌ Commit '{command}' failed: {resp.StatusCode} - {body}");
                    }
                    else
                    {
                        log.log($"✅ Commit '{command}' ok: {body}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.log($"CommitControlCommandAsync('{command}') error: {ex}");
            }
        }

        // ===== Internal (local) ops that mirror the TakeNext style =====
        private Task Internal_CueNextAsync(string reconKey)
        {
            return Task.Run(async () =>
            {
                log.log("Internal_CueNextAsync: issuing 'cuenext'");
                await CommitControlCommandAsync("cuenext", reconKey).ConfigureAwait(false);
            });
        }

        private Task Internal_SkipNextAsync(string reconKey)
        {
            return Task.Run(async () =>
            {
                log.log("Internal_SkipNextAsync: issuing 'skipnext'");
                await CommitControlCommandAsync("skipnext", reconKey).ConfigureAwait(false);
            });
        }

        // ===== External entrypoints (mirror-aware), same structure as takeNextMirror =====
        [AMPPCommand("cuenextmirror", Schema = TakeNextOnPairNetworkSchema, Version = "1.0", Markdown = "Markdown.cuepair.md")]
        public async Task cueNextMirror(JObject payload, string reconKey)
        {
            bool awaitPeer = false; // match takeNextMirror behavior

            log.log("Received cuenext command to cue next on master & backup");
            log.log(JsonConvert.SerializeObject(payload, Newtonsoft.Json.Formatting.Indented));

            var demoState = payload.ToObject<DemoState>();
            if (demoState.Active == true)
            {
                var localTask = Internal_CueNextAsync(reconKey);
                var peerTask = _peer != null ? (_peer as AmppChannelMirrorApp)?.Internal_CueNextAsync(reconKey) ?? Task.CompletedTask
                                              : Task.CompletedTask;

                if (awaitPeer)
                {
                    await Task.WhenAll(localTask, IgnorePeerErrors(peerTask)).ConfigureAwait(false);
                }
                else
                {
                    _ = IgnorePeerErrors(peerTask);
                    await localTask.ConfigureAwait(false);
                }
            }
            else
            {
                log.log("Channel is not Active. Ignoring cuenext command");
            }
        }

        [AMPPCommand("skipnextmirror", Schema = TakeNextOnPairNetworkSchema, Version = "1.0", Markdown = "Markdown.skippair.md")]
        public async Task skipNextMirror(JObject payload, string reconKey)
        {
            bool awaitPeer = false; // match takeNextMirror behavior

            log.log("Received skipnext command to skip next on master & backup");
            log.log(JsonConvert.SerializeObject(payload, Formatting.Indented));

            var demoState = payload.ToObject<DemoState>();
            if (demoState.Active == true)
            {
                var localTask = Internal_SkipNextAsync(reconKey);
                var peerTask = _peer != null ? (_peer as AmppChannelMirrorApp)?.Internal_SkipNextAsync(reconKey) ?? Task.CompletedTask
                                              : Task.CompletedTask;

                if (awaitPeer)
                {
                    await Task.WhenAll(localTask, IgnorePeerErrors(peerTask)).ConfigureAwait(false);
                }
                else
                {
                    _ = IgnorePeerErrors(peerTask);
                    await localTask.ConfigureAwait(false);
                }
            }
            else
            {
                log.log("Channel is not Active. Ignoring skipnext command");
            }
        }

    }
}
