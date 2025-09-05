using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace gv.ampp.control.demo.app
{
    /// <summary>
    /// Represents a single <ChannelEntry .../> under <Channels>.
    /// </summary>
    public sealed class ChannelEntry
    {
        public string Name { get; }
        public string NetworkId { get; }
        public string    PlatformUri { get; }
        public string PlatformApiKey { get; }

        public ChannelEntry(XmlNode node, RollingLogger log)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!string.Equals(node.Name, "ChannelEntry", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Expected <ChannelEntry> element.");

            string GetAttr(params string[] names)
            {
                if (node.Attributes != null)
                {
                    foreach (var n in names)
                    {
                        var a = node.Attributes[n];
                        if (!string.IsNullOrEmpty(a?.Value)) return a.Value;
                    }
                    foreach (XmlAttribute a in node.Attributes)
                        foreach (var n in names)
                            if (a.Name.Equals(n, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(a.Value))
                                return a.Value;
                }
                return null;
            }

            var name = GetAttr("NAME", "Name")?.Trim();
            var nid  = GetAttr("NETWORKID", "NetworkId")?.Trim();
            var puri = GetAttr("PLATFORMURI", "PlatformUri", "PlatformURI")?.Trim();
            var key  = GetAttr("PLATFORMAPIKEY", "PlatformApiKey", "PlatformAPIKey")?.Trim();

            if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("ChannelEntry.NAME is missing.");
            if (string.IsNullOrWhiteSpace(nid))  throw new InvalidDataException($"ChannelEntry({name}): NETWORKID is missing.");
            if (string.IsNullOrWhiteSpace(puri)) throw new InvalidDataException($"ChannelEntry({name}): PLATFORMURI is missing.");
            if (!Uri.TryCreate(puri, UriKind.Absolute, out var uri))
                throw new InvalidDataException($"ChannelEntry({name}): PLATFORMURI is not a valid absolute URI: {puri}");
            if (string.IsNullOrWhiteSpace(key))  throw new InvalidDataException($"ChannelEntry({name}): PLATFORMAPIKEY is missing.");

            Name = name;
            NetworkId = nid;
            PlatformUri = puri;
            PlatformApiKey = key;
            log.log($"Loaded channelEntry: {this}");
        }

        public override string ToString() => $"{Name} | {NetworkId} | {PlatformUri}";
    }

    public static class ChannelConfig
    {
        /// <summary>Load all channels into a dictionary keyed by NAME.</summary>
        public static Dictionary<string, ChannelEntry> LoadChannels(string xmlPath, RollingLogger log)
        {
            if (string.IsNullOrWhiteSpace(xmlPath)) throw new ArgumentNullException(nameof(xmlPath));
            var doc = new XmlDocument();
            doc.Load(xmlPath);

            var result = new Dictionary<string, ChannelEntry>(StringComparer.OrdinalIgnoreCase);
            var nodes = doc.SelectNodes("/configuration/Channels/ChannelEntry");
            if (nodes == null) return result;

            foreach (XmlNode n in nodes)
            {
                var c = new ChannelEntry(n,log);
                result[c.Name] = c; // last one wins if duplicate
            }
            return result;
        }

        /// <summary>
        /// Parse "A=B;C=D" from &lt;MirrorMap&gt; into a dictionary of A->B.
        /// </summary>
        public static Dictionary<string, string> LoadMirrorMap(string xmlPath)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var doc = new XmlDocument();
            doc.Load(xmlPath);

            var node = doc.SelectSingleNode("/configuration/MirrorMap");
            if (node == null || string.IsNullOrWhiteSpace(node.InnerText)) return map;

            foreach (var pair in node.InnerText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (kv.Length == 2)
                {
                    var k = kv[0].Trim();
                    var v = kv[1].Trim();
                    if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                        map[k] = v;
                }
            }
            return map;
        }

    }

    }