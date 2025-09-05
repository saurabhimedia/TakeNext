using System.Net.Sockets;
using System.Net;
using System.Text;
using System;
using System.Threading;
using System.Text.RegularExpressions;
using gv.ampp.control.demo.app.Model;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace gv.ampp.control.demo.app
{
    public class TCPSocketServer
    {
        private string ip;
        private int port;
        private Socket server;
        private IPEndPoint ipEndPoint;
        private static readonly Dictionary<string, DateTime> _lastTakeNextTimes = new Dictionary<string, DateTime>();

        // Define how long we should ignore subsequent TakeNext calls
        // for the same rundownId after the first.
        //private static  TimeSpan IGNORE_INTERVAL = TimeSpan.FromSeconds(5);
        AmppChannelMirrorApp cs;
        private static RollingLogger log = RollingLogger.getInstance("TakeNext");
        // Instead of readonly, make IGNORE_INTERVAL a private static field
        private static TimeSpan _ignoreInterval = TimeSpan.FromMilliseconds(5000);

        // Public method to set the interval
        public static void SetIgnoreInterval(int intervalInMs)
        {

                _ignoreInterval = TimeSpan.FromMilliseconds(intervalInMs); ;
        }
        public TCPSocketServer(int port, AmppChannelMirrorApp csI)
        {

            // Establish the local endpoint 
            // for the socket. Dns.GetHostName
            // returns the name of the host 
            // running the application.
            IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddr = ipHost.AddressList[0];
            ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            server = null;
            cs = csI;
            log.log("Initialed Socket Server Service instance...");

        }
        public void StartServer()
        {
            // Establish the local endpoint 
            // for the socket. Dns.GetHostName
            // returns the name of the host 
            // running the application.
            // IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
            // IPAddress ipAddr = ipHost.AddressList[0];
            // IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 11111);

            // Creation TCP/IP Socket using 
            // Socket Class Constructor
            log.log("Starting the Server");
            Socket listener=null;
            try
            {

                 listener = new Socket(IPAddress.Any.AddressFamily,
                                SocketType.Stream, ProtocolType.Tcp);
                // Using Bind() method we associate a
                // network address to the Server Socket
                // All client that will connect to this 
                // Server Socket must know this network
                // Address

                log.log("Binding the socket");
                listener.Bind(ipEndPoint);

                // Using Listen() method we create 
                // the Client list that will want
                // to connect to Server
                listener.Listen(10);
                log.log("Listening successfully ");

                while (true)
                {
                    try
                    {
                        log.log("Waiting for new Client..");

                        // Suspend while waiting for
                        // incoming connection Using 
                        // Accept() method the server 
                        // will accept connection of client
                        Socket clientSocket = listener.Accept();
                        log.log("Received new connection request from " + clientSocket.RemoteEndPoint.ToString());
                        //runClientThread(clientSocket);
                        Thread t = new Thread(new ThreadStart(() => runClientThread(clientSocket)));
                        t.Start();
                        
                        // Data buffer
                    }
                    catch(Exception e)
                    {
                        log.log("Error in TCP Server Thread " + e.Message +"\n"+ e.StackTrace);
                    }

                }
            }

            catch (Exception e)
            {
                log.log(e.ToString());
            }
            listener?.Close();
            listener?.Dispose();

        }
        private void runClientThread(Socket clientSocket)
        {
            byte[] bytes = new byte[255];
            string data = null;
            string rundownId = cs.getRundownId();

            log.log("Started the client thread and it's running");

            while (true)
            {
                try
                {
                    int numByte = clientSocket.Receive(bytes);
                    log.log("Waiting for New Command to Execute!!");

                    data = Encoding.ASCII.GetString(bytes, 0, numByte);
                    log.log("cmd: " + data);

                    string[] cmds = data.Split(':');
                    if (cmds.Length > 1 && cmds[0] != null)
                    {
                        switch (cmds[0].ToUpper())
                        {
                            case "TAKENEXT":
                                // We expect something like: "TAKENEXT:someRundownId"
                                string newRundownId = cmds[1];

                                if (ShouldProcessTakeNext(newRundownId))
                                {
                                    log.log($"TakeNext command accepted for rundownId: {newRundownId}");
                                    // Mark the time we processed it
                                    UpdateLastTakeNextTime(newRundownId);

                                    // Perform the actual work
                                    _ = cs.TakeNext(newRundownId);
                                }
                                else
                                {
                                    // Ignore if within the ignore interval
                                    log.log($"TakeNext command IGNORED for rundownId: {newRundownId}");
                                }

                                break;

                            default:
                                log.log("Command not handled: " + data);
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    log.log(e.ToString());
                    if (e.ToString().Contains("closed by the remote host"))
                        break;
                }
            }
            log.log("Client thread exited!!");

            // Clean up the socket
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            clientSocket.Dispose();
        }
        /// <summary>
        /// Returns true if enough time has passed since the last TakeNext for the same rundownId.
        /// Returns false if the last command was too recent and should be ignored.
        /// </summary>
        public static bool ShouldProcessTakeNext(string rundownId)
        {
            lock (_lastTakeNextTimes)
            {
                if (_lastTakeNextTimes.TryGetValue(rundownId, out DateTime lastTime))
                {
                    // If the last TakeNext was within _ignoreInterval, ignore this one
                    if (DateTime.UtcNow - lastTime < _ignoreInterval)
                    {
                        return false;
                    }
                }
                // If not in the dictionary or it's been longer than IGNORE_INTERVAL, we can process
                return true;
            }
        }

        /// <summary>
        /// Updates the timestamp for the last processed TakeNext command for the given rundownId.
        /// </summary>
        public static void UpdateLastTakeNextTime(string rundownId)
        {
            lock (_lastTakeNextTimes)
            {
                _lastTakeNextTimes[rundownId] = DateTime.UtcNow;
            }
        }
        private void runClientThreadOld(Socket clientSocket)
        {
            byte[] bytes = new byte[255];
            string data = null;
            //Task<string> id;
            //Regex reTakeNext = new Regex("(111\\*|9#99.*)");
            //Regex reGoLive= new Regex("444#");
            string rundownId = cs.getRundownId();
            log.log("Started the client thread and its running");
            while (true)
            {
                try
                {
                    int numByte = clientSocket.Receive(bytes);
                    log.log("Waiting for Data!!");

                    data = Encoding.ASCII.GetString(bytes,
                                               0, numByte);
                    log.log("cmd" + data);
                    string[] cmds = data.Split(":");
                    if (cmds[0] != null)
                    {
                        //if (reTakeNext.IsMatch(data))
                        switch (cmds[0].ToUpper())
                        {
                            case "TAKENEXT":
                                log.log("Take next Item , cmd received for network id/name:" + rundownId);
                                //write code for get next
                                rundownId = cmds[1];
                                _ = cs.TakeNext(rundownId);
                                break;
                            default:
                                log.log("Command not handled:" + data);
                                break;

                        }
                    }
                    // else if (reGoLive.IsMatch(data))
                    // {
                    //     log.log("Go Live cmd "+data);
                    //    
                    // }
                }
                catch (Exception e)
                {
                    log.log(e.ToString());
                    if (e.ToString().Contains("closed by the remote host"))
                        break;
                }
            }

            log.log("Client thread existed!!");

            // Send a message to Client 
            // using Send() method
            //clientSocket.Send(message);

            // Close client Socket using the
            // Close() method. After closing,
            // we can use the closed Socket 
            // for a new Client Connection
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            clientSocket.Dispose();

        }
    }
}