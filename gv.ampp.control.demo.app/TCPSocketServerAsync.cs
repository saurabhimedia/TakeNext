using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace gv.ampp.control.demo.app
{
    // ------------------------------------------

    public class TCPSocketServerAsync : IDisposable
    {
        private readonly IPEndPoint _localEndPoint;
        private Socket _listener;
        private readonly CrashLiveApp _crashLiveApp;
        private static readonly RollingLogger _log = RollingLogger.getInstance("TakeNext");

        private CancellationTokenSource _cts;
        private bool _isRunning;

        public TCPSocketServerAsync(int port, CrashLiveApp cs)
        {
            // Create the endpoint (here we bind to any IP, on the specified port)
            _localEndPoint = new IPEndPoint(IPAddress.Any, port);
            _crashLiveApp = cs;
        }

        /// <summary>
        /// Starts the server asynchronously, listening for incoming connections.
        /// This method will not return until StopServer() is called 
        /// or an exception occurs.
        /// </summary>
        public async Task StartServerAsync(CancellationToken token = default)
        {
            if (_isRunning)
            {
                _log.log("Server is already running.");
                return;
            }
            _isRunning = true;

            _log.log("Starting the TCP Socket Server...");
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            try
            {
                // Create the listening socket
                _listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
                _listener.Bind(_localEndPoint);
                _listener.Listen(backlog: 10);

                _log.log($"Server listening on port {_localEndPoint.Port}");

                while (!_cts.Token.IsCancellationRequested)
                {
                    _log.log("Waiting for a new client...");

                    // Accept a client (async)
                    var clientSocket = await _listener.AcceptAsync().ConfigureAwait(false);

                    if (clientSocket == null)
                    {
                        // If AcceptAsync returns null in some edge cases, break out.
                        _log.log("Listener returned a null client socket. Stopping.");
                        break;
                    }

                    _log.log($"Accepted connection from {clientSocket.RemoteEndPoint}");

                    // Handle the client in a separate task (thread-pool)
                    _ = Task.Run(() => HandleClientAsync(clientSocket, _cts.Token));
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
            {
                // Likely the socket was closed or canceled
                _log.log($"Server socket closed or disposed: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Other unhandled errors
                _log.log($"Fatal server error: {ex}");
                throw;
            }
            finally
            {
                _isRunning = false;
                // Clean up the listener if needed
                ShutdownListener();
                _log.log("Server stopped.");
            }
        }

        /// <summary>
        /// Gracefully stops the server by cancelling the acceptance loop
        /// and closing the listener socket.
        /// </summary>
        public void StopServer()
        {
            if (!_isRunning) return;
            _log.log("Stopping the server...");
            _cts?.Cancel();
        }

        /// <summary>
        /// This is the core loop for each connected client.
        /// We read data line-by-line until the client disconnects or we hit an error.
        /// </summary>
        private async Task HandleClientAsync(Socket clientSocket, CancellationToken token)
        {
            // We'll accumulate incoming bytes until we see a newline
            var buffer = new byte[1024];
            var sb = new StringBuilder();
            string rundownId = _crashLiveApp.getRundownId();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    _log.log("Waiting for client data...");

                    int bytesRead = await clientSocket
                        .ReceiveAsync(buffer, SocketFlags.None, token)
                        .ConfigureAwait(false);

                    // If 0 bytes, client closed connection
                    if (bytesRead == 0)
                    {
                        _log.log("Client disconnected gracefully.");
                        break;
                    }

                    // Decode and append to string builder
                    string chunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    sb.Append(chunk);

                    // Process commands line by line
                    while (TryReadLine(sb, out string line))
                    {
                        // Example command parsing
                        ProcessCommand(line, ref rundownId);
                    }
                }
            }
            catch (SocketException ex)
            {
                _log.log($"Client socket error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                _log.log("Client handler canceled.");
            }
            catch (Exception ex)
            {
                _log.log($"Unexpected client handler error: {ex}");
            }
            finally
            {
                // Clean up
                SafeClose(clientSocket);
                _log.log("Client handler exited.");
            }
        }

        /// <summary>
        /// Extracts a single line (delimited by '\n') from the StringBuilder
        /// If a full line exists, returns true and sets 'line' (excluding newline).
        /// Otherwise returns false.
        /// </summary>
        private bool TryReadLine(StringBuilder sb, out string line)
        {
            line = null;
            var str = sb.ToString();
            int newlineIndex = str.IndexOf('\n');
            if (newlineIndex < 0) return false;

            // Extract up to the newline (trim \r if present)
            line = str.Substring(0, newlineIndex).Trim('\r');
            // Remove that line (plus the newline) from the buffer
            sb.Remove(0, newlineIndex + 1);
            return true;
        }

        /// <summary>
        /// Parses and executes commands from the client.
        /// Example format: "TAKENEXT:myRundownId"
        /// </summary>
        private void ProcessCommand(string data, ref string rundownId)
        {
            _log.log($"Received command: {data}");

            // Split on ':'
            var cmds = data.Split(':');
            if (cmds.Length == 0)
            {
                _log.log("Empty command received. Ignoring.");
                return;
            }

            var cmdName = cmds[0].Trim().ToUpperInvariant();

            switch (cmdName)
            {
                case "TAKENEXT":
                    if (cmds.Length > 1)
                    {
                        rundownId = cmds[1].Trim();
                        _log.log($"Take next item for rundown: {rundownId}");
                        _ = _crashLiveApp.TakeNext(rundownId);
                    }
                    else
                    {
                        _log.log("TAKENEXT command missing rundownId parameter.");
                    }
                    break;

                default:
                    _log.log($"Command not handled: {data}");
                    break;
            }
        }

        /// <summary>
        /// Closes the listener socket if it's open.
        /// </summary>
        private void ShutdownListener()
        {
            try
            {
                if (_listener != null)
                {
                    _listener.Close();
                    _listener.Dispose();
                    _listener = null;
                }
            }
            catch (Exception ex)
            {
                _log.log($"Error closing listener: {ex.Message}");
            }
        }

        /// <summary>
        /// Safely shutdown and close a client socket.
        /// </summary>
        private void SafeClose(Socket socket)
        {
            if (socket == null) return;
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // ignore
            }
            finally
            {
                socket.Close();
                socket.Dispose();
            }
        }

        /// <summary>
        /// If someone uses 'using' on this class, we stop the server and release resources.
        /// </summary>
        public void Dispose()
        {
            StopServer();
            ShutdownListener();
            _cts?.Dispose();
        }
    }
}
