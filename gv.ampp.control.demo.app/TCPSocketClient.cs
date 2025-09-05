using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
namespace gv.ampp.control.demo.app
{
    public class TCPSocketClient : IDisposable
    {
        private readonly string ip;
        private readonly int port;
        private Socket client;
        private readonly IPEndPoint ipEndPoint;
        private static readonly RollingLogger log = RollingLogger.getInstance("TakeNext");
        private readonly string ackRegXPattern=null;
        private readonly string connectHandShakeComnand=null;
        private Regex _responseRegex=null;
        private bool _disposed = false;

        public TCPSocketClient(string ip, int port, string ackRegXPattern, string initialHandshake)
        {
            this.ip = ip;
            this.port = port;
            IPAddress ipAddress = IPAddress.Parse(ip);
            ipEndPoint = new IPEndPoint(ipAddress, port);
            this.ackRegXPattern = ackRegXPattern;
            if(ackRegXPattern!=null )
                 _responseRegex = new Regex(ackRegXPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            
            connectHandShakeComnand = (initialHandshake!=null?CommonUtils.MakeProperCommandString(initialHandshake):null);
        }
        private bool IsValidResponse(string response)
        {
            if (_responseRegex != null)
                return _responseRegex.IsMatch(response);
            else
                return true;
        }
        private bool isACKRequired()
        {
            return _responseRegex != null;
        }

        private static async Task ConnectWithTimeout(Socket s, EndPoint ep, int timeoutMs)
        {
            var connectTask = s.ConnectAsync(ep);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask)
            {
                SafeClose(s);
                throw new TimeoutException($"TCP connect timed out after {timeoutMs} ms.");
            }
            log.log("Connection established to "+ep.ToString() );
            await connectTask; // propagate real exceptions if any
        }
        private static void SafeClose(Socket s)
        {
            try { s?.Shutdown(SocketShutdown.Both); } catch { }
            try { s?.Close(); } catch { }
            try { s?.Dispose(); } catch { }
        }
        public async Task SendIPCmdAsync(string cmd, int delayMilliseconds)
        {
            log.log($"Received command to send {cmd}, time to wait {delayMilliseconds} ms");

            int attempt = 0;
            const int maxAttempts = 2;  // 1 original try + 1 retry
            byte[] buffer = new byte[256];

            while (attempt < maxAttempts)
            {
                try
                {
                    await EnsureConnectedAsync();

                    byte[] byteArray = Encoding.UTF8.GetBytes(cmd);
                    await client.SendAsync(byteArray, SocketFlags.None);

                    log.log($"Command sent successfully: \"{cmd}\"");

                    if (isACKRequired())
                    {
                        var receiveTask = client.ReceiveAsync(buffer, SocketFlags.None);
                        var completedTask = await Task.WhenAny(receiveTask, Task.Delay(delayMilliseconds));
                        // Wait for ACK
                        //log.log("Raw ACK Response: [" + Encoding.UTF8.GetString(buffer, 0, 10) + "]");
                        //int received = await networkStream.ReadAsync(buffer, 0, buffer.Length);

                        if (completedTask == receiveTask)
                        {
                            int received = receiveTask.Result;
                            if (received > 0)
                            {
                                string response = Encoding.UTF8.GetString(buffer, 0, received);
                                if (IsValidResponse(response))
                                {
                                    log.log("Command Response: " + response);
                                }
                                else
                                {
                                    log.log("Invalid Response: " + response);
                                }
                            }
                            else
                            {
                                log.log("No data received (0 bytes).");
                            }
                        }
                        else
                        {
                            log.log("Receive timed out after "+ delayMilliseconds+ " ms.");
                        }
                    }

                    //await Task.Delay(delayMilliseconds);

                    return; // success, exit method
                }
                catch (SocketException sex)
                {
                    log.log($"SocketException on attempt {attempt + 1} to {ip}:{port}-> {sex.Message}");

                    // Check if error is connection reset or connection aborted
                    if ((sex.SocketErrorCode == SocketError.ConnectionReset ||
                         sex.SocketErrorCode == SocketError.ConnectionAborted) && attempt == 0)
                    {
                        log.log("Connection reset detected, attempting to reconnect and retry...");
                        Close();  // Close existing socket, force reconnect on next loop
                        attempt++;
                        continue; // retry
                    }
                    else
                    {
                        // Non-recoverable or second failure, give up
                        log.log("Unrecoverable socket error or retry failed. Giving up.");
                        break;
                    }
                }
                catch (Exception e)
                {
                    log.log($"Exception Unable to send command: {e.Message}");
                    break;
                }
            }
        }


        public async void SendLogoActivationCmdAsync(string layer, string category, string logo)
        {
            log.log($"Received layer {layer}, category {category}, logo {logo}");
            string CUE_LOGO_STR = $"V0 {layer}|1|XMS\\3ATemplates.{category}\\3A{logo}";
            string ACTIVATE_LOGO_STR = $"V1 {layer}|-1";

            try
            {
                byte[] CUE_LOGO_CMD = OxtelEncoderDecoder.getEncodedMessage(CUE_LOGO_STR);
                byte[] ACTIVATE_LOGO_CMD = OxtelEncoderDecoder.getEncodedMessage(ACTIVATE_LOGO_STR);
                byte[] buffer = new byte[1024];

                // Create and connect a new socket specifically for this operation
                using (var tempClient = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    await tempClient.ConnectAsync(ipEndPoint);

                    while (true)
                    {
                        // Send the Cue command for the logo
                        await tempClient.SendAsync(CUE_LOGO_CMD, SocketFlags.None);
                        log.log($"Socket client sent message: \"{Encoding.UTF8.GetString(CUE_LOGO_CMD)}\"");

                        // Receive ack
                        int received = await tempClient.ReceiveAsync(buffer, SocketFlags.None);
                        log.log($"Socket client received msg: \"{Encoding.UTF8.GetString(buffer, 0, received)}\"");

                        if (buffer[0] == 0x04 || buffer[0] == 0x05)
                        {
                            log.log("Ack Received");
                        }

                        // Send activate command
                        await tempClient.SendAsync(ACTIVATE_LOGO_CMD, SocketFlags.None);
                        log.log($"Socket client sent message: \"{ACTIVATE_LOGO_STR}\"");

                        // Receive ack
                        received = await tempClient.ReceiveAsync(buffer, SocketFlags.None);
                        log.log($"Socket client received msg: \"{Encoding.UTF8.GetString(buffer, 0, received)}\"");

                        if (buffer[0] == 0x04 || buffer[0] == 0x05)
                        {
                            log.log("Ack Received for Take");
                            break;
                        }
                    }

                    tempClient.Shutdown(SocketShutdown.Both);
                    tempClient.Close();
                }
            }
            catch (Exception e)
            {
                log.log($"Exception Unable to send command: {e.Message}");
            }
        }

        private async Task EnsureConnectedAsync()
        {
            if (client == null || !client.Connected)
            {
                client?.Dispose();
                client = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await ConnectWithTimeout(client, ipEndPoint, 10000);
                //await client.ConnectAsync(ipEndPoint);
                if(connectHandShakeComnand!=null)
                {
                    await this.SendIPCmdAsync(connectHandShakeComnand, 1000);
                }
            }
        }

        public void Close()
        {
            if (client != null)
            {
                try
                {
                    if (client.Connected)
                    {
                        client.Shutdown(SocketShutdown.Both);
                    }
                }
                catch { /* Ignore exceptions on shutdown */ }

                client.Close();
                client.Dispose();
                client = null;
            }
        }

        // IDisposable implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Close();
                }
                _disposed = true;
            }
        }

        ~TCPSocketClient()
        {
            Dispose(false);
        }
    }
}
