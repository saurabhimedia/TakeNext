using System;
using gv.ampp.smpl.util;
namespace gv.ampp.control.smpl.tcp.client
{ 
    public class TCPSocketClient
    {
	    private string ip;
	    private int port;
        using Socket client;
        
        private static final byte[] CUE_LOGO = OxtelEncoderDecoder.getEncodedMessage("V0 7|1|XMS\3ATemplates.INDIADEMO\3ABUGIN");
        private static final byte[] ACTIVATE_LOGO = OxtelEncoderDecoder.getEncodedMessage("V1 7|-1");
        public TCPSocketClient(string ip,int port)
	    {
            IPAddress ipAddress= Parse(ip); ;
            IPEndPoint ipEndPoint = new(ipAddress, port);
        
            client = new(
                ipEndPoint.AddressFamily,
                SocketType.Stream,
                ProtocolType.Tcp);
        }
        public void sendLogoActivationCmd()
        {
            await client.ConnectAsync(ipEndPoint);
            while (true)
            {
                // Send message.
                char message[1024];
                
                char* messageBytes = getEncodedMessage(CUE_LOGO, message);
                _ = await client.SendAsync(messageBytes, SocketFlags.None);
                LogUtil.LogWithDtTime($"Socket client sent message: \"{message}\"");
               
                // Receive ack.
                var buffer = new byte[1_024];
                var received = await client.ReceiveAsync(buffer, SocketFlags.None);
                //var response = Encoding.UTF8.GetString(buffer, 0, received);
                if (buffer[0] == 0x04 ||)
                {
                    LogUtil.LogWithDtTime(
                        $"Socket client received acknowledgment: \"{response}\"");
                    break;
                }
                // Sample output:
                //     Socket client sent message: "Hi friends 👋!<|EOM|>"
                //     Socket client received acknowledgment: "<|ACK|>"
            }
            client.Close();
        }
        ~TCPSocketClient()
        {
            client.Shutdown(SocketShutdown.Both);
        }
        // Note these codes bear no relation to the ASCII defined codes with similar names.
                         
    }
}