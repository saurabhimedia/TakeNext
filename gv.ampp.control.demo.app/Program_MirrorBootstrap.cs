using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace gv.ampp.control.demo.app
{
    /// <summary>
    /// Helper to fan out all channel mirror pairs.
    /// </summary>
    public static class MirrorBootstrap
    {
        public static async Task StartAllAsync(string xmlConfigPath,
                                               Dictionary<string,string> parameters,
                                               RollingLogger log)
        {
            var channels  = CommonUtils.LoadChannels(xmlConfigPath);
            var mirrorMap = CommonUtils.LoadMirrorMap(xmlConfigPath);

            if (mirrorMap.Count == 0)
            {
                log.log("MirrorMap empty. Nothing to start.");
                return;
            }

            short baseCmdPort = 16502;
            if (parameters != null && parameters.TryGetValue("Command Server Port", out var p) && short.TryParse(p, out var parsed))
                baseCmdPort = parsed;

            var workloadPrefix = new AmppConfiguration().GetworkloadName();
            var seedConfig = new DemoConfiguration { ConnectionString = "foo://abc/123", Port = 80085 };

            var pairs = new List<MirrorPair>();
            int idx = 0;
            foreach (var kv in mirrorMap)
            {
                if (!channels.TryGetValue(kv.Key, out var left))  { log.log($"[WARN] Unknown '{kv.Key}'"); continue; }
                if (!channels.TryGetValue(kv.Value, out var right)) { log.log($"[WARN] Unknown '{kv.Value}'"); continue; }

                var pair = new MirrorPair(workloadPrefix, seedConfig, log,
                                          left, right,
                                          baseCmdPort + (idx * 2),
                                          baseCmdPort + (idx * 2) + 1,
                                          parameters);
                pairs.Add(pair);
                idx++;
            }

            await Task.WhenAll(pairs.ConvertAll(p => p.StartAsync())).ConfigureAwait(false);
        }
    }
}