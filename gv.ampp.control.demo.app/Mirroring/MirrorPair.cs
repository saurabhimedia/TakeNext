using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Gv.Ampp.Control.Sdk;
using gv.ampp.control.demo.app.Model;
using Microsoft.Extensions.Configuration;
namespace gv.ampp.control.demo.app
{
    /// <summary>
    /// Owns exactly two channel instances and wires them for internal mirroring.
    /// </summary>
    public sealed class MirrorPair : IAsyncDisposable
    {
        public ChannelEntry LeftEntry  { get; }
        public ChannelEntry RightEntry { get; }

        public AmppChannelMirrorApp Left  { get; private set; }
        public AmppChannelMirrorApp Right { get; private set; }

        private readonly string _workloadPrefix;
        private readonly DemoConfiguration _seedConfig;
        private readonly RollingLogger log;
        private readonly Dictionary<string,string> _baseParams;


        public MirrorPair(DemoConfiguration seedConfig,
                          RollingLogger _log,
                          ChannelEntry leftEntry,
                          ChannelEntry rightEntry,
                          Dictionary<string,string> baseParams)
        {
            _seedConfig     = seedConfig     ?? throw new ArgumentNullException(nameof(seedConfig));
            log            = _log            ?? throw new ArgumentNullException(nameof(log));
            LeftEntry  = leftEntry  ?? throw new ArgumentNullException(nameof(leftEntry));
            RightEntry = rightEntry ?? throw new ArgumentNullException(nameof(rightEntry));
            //_leftCmdPort  = leftCmdPort;
           // _rightCmdPort = rightCmdPort;
            _baseParams = baseParams ?? new(StringComparer.OrdinalIgnoreCase);
            log.log($"[PAIR {LeftEntry.Name}<->{RightEntry.Name}] Created ");
        }

        public async Task StartAsync()
        {
            log.log($"[Starting PAIR {LeftEntry.Name}<->{RightEntry.Name}] Ready ");
            Left  = await StartOneAsync(LeftEntry).ConfigureAwait(false);
            Right = await StartOneAsync(RightEntry).ConfigureAwait(false);

            // Wire internal peers (external -> local -> peer internal)
            Left.AttachPeer(Right);
            Right.AttachPeer(Left);

            log.log($"[PAIR {LeftEntry.Name}<->{RightEntry.Name}] Ready ");
        }

        private async Task<AmppChannelMirrorApp> StartOneAsync(ChannelEntry ce/*, int cmdPort*/)
        {
            var workloadName = Program.getParam(_baseParams,"Application Name");
            log.log($"Trying to start  \"{workloadName}\"");
            var ampp = new AmppControlService(ce.PlatformUri.ToString(), ce.PlatformApiKey, workloadName);
            log.log($"Attempting to authenticate and connect with platform... {workloadName}");
            if (!await ampp.ConnectToGVPlatform())
            {
                log.log($"AMPP connect failed for {ce.Name} with {ce.PlatformUri.ToString()}, " +
                    $"{ce.PlatformApiKey}");
                throw new InvalidOperationException($"AMPP connect failed for {ce.Name} with {ce.PlatformUri.ToString()}, " +
                    $"{ce.PlatformApiKey}");
            }

            var cfg = await ampp.GetConfigurationAsync().ConfigureAwait(false);
            var demoConfig = cfg == null ? _seedConfig
                                         : (JsonConvert.DeserializeObject<DemoConfiguration>(cfg.Value) ?? _seedConfig);

            var p = new Dictionary<string,string>(_baseParams, StringComparer.OrdinalIgnoreCase) {
                ["NETWORKID"]      = ce.NetworkId,
                ["PLATFORMURI"]    = ce.PlatformUri.ToString(),
                ["PLATFORMAPIKEY"] = ce.PlatformApiKey,
                ["CHANNEL_NAME"]   = ce.Name
            };

            var app = new AmppChannelMirrorApp(workloadName, demoConfig, ampp,
                                               ce.PlatformUri.ToString(), ce.PlatformApiKey, p)
            {
                ChannelName = ce.Name
            };
            /*
            if (cmdPort > 0)
            {
                var srv = new TCPSocketServer((short)cmdPort, app);
                srv.StartServer();
                if (ReferenceEquals(app, Left)) _leftSrv = srv; else _rightSrv = srv;
                _log.log($"[{ce.Name}] Command server @ {cmdPort}");
            }
            */
            return app;
        }

        public ValueTask DisposeAsync()
        {
            //try { _leftSrv?.StopServer(); } catch { }
            //try { _rightSrv?.StopServer(); } catch { }
            return ValueTask.CompletedTask;
        }
        public string toString()
            => $"{LeftEntry.Name}<->{RightEntry.Name}"; 
    }
}