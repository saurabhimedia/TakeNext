using System.Threading.Tasks;

namespace gv.ampp.control.demo.app.Mirroring
{
    /// <summary>
    /// Internal-only mirror surface (never expose to AMPP/UI).
    /// </summary>
    public interface IMirrorEndpoint
    {
        string ChannelName { get; set; }
        void AttachPeer(IMirrorEndpoint peer);

        // Follow ops triggered by the peer (local work only; do not call peer again)
        Task Internal_TakeAsync(string itemId);
        Task Internal_TakeNextAsync();
    }
}