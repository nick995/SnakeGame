//  Nov, 11, 2022
//  PS7
//  MinGyu Jung & SangYoon Cho.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace NetworkUtil;

public static class Networking
{
    /////////////////////////////////////////////////////////////////////////////////////////
    // Server-Side Code
    /////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Starts a TcpListener on the specified port and starts an event-loop to accept new clients.
    /// The event-loop is started with BeginAcceptSocket and uses AcceptNewClient as the callback.
    /// AcceptNewClient will continue the event-loop.
    /// </summary>
    /// <param name="toCall">The method to call when a new connection is made</param>
    /// <param name="port">The the port to listen on</param>
    public static TcpListener StartServer(Action<SocketState> toCall, int port)
    {
        TcpListener listener = new TcpListener(IPAddress.Any, port);

        try
        {
            // Make a tuple to pass the information of SocketState delegate and port.
            Tuple<Action<SocketState>, TcpListener> tuple = new Tuple<Action<SocketState>, TcpListener>(toCall, listener);

            listener.Start();

            // Start Event-Loop of Acceptance
            listener.BeginAcceptSocket(AcceptNewClient, tuple);
        }
        catch
        {
        }

        return listener;
    }

    /// <summary>
    /// To be used as the callback for accepting a new client that was initiated by StartServer, and
    /// continues an event-loop to accept additional clients.
    ///
    /// Uses EndAcceptSocket to finalize the connection and create a new SocketState. The SocketState's
    /// OnNetworkAction should be set to the delegate that was passed to StartServer.
    /// Then invokes the OnNetworkAction delegate with the new SocketState so the user can take action.
    ///
    /// If anything goes wrong during the connection process (such as the server being stopped externally),
    /// the OnNetworkAction delegate should be invoked with a new SocketState with its ErrorOccurred flag set to true
    /// and an appropriate message placed in its ErrorMessage field. The event-loop should not continue if
    /// an error occurs.
    ///
    /// If an error does not occur, after invoking OnNetworkAction with the new SocketState, an event-loop to accept
    /// new clients should be continued by calling BeginAcceptSocket again with this method as the callback.
    /// </summary>
    /// <param name="ar">The object asynchronously passed via BeginAcceptSocket. It must contain a tuple with
    /// 1) a delegate so the user can take action (a SocketState Action), and 2) the TcpListener</param>
    private static void AcceptNewClient(IAsyncResult ar)
    {
        Tuple<Action<SocketState>, TcpListener> tuple = (Tuple<Action<SocketState>, TcpListener>)ar.AsyncState!;

        Action<SocketState> socketStateValue = tuple.Item1;      // Action<SocketState>
        TcpListener tcpListenerValue = tuple.Item2;              // TcpListener

        // Before trying to invoke BeginAcceptSocket, if state has error
        try
        {
            // The Socket information passed from StartServer
            Socket newClient = tcpListenerValue.EndAcceptSocket(ar);
            SocketState state = new SocketState(socketStateValue, newClient);

            if (state.ErrorOccurred)
                return;

            state.OnNetworkAction(state);
        }
        catch (Exception e)
        {
            // With error state, it doesn't begin accept client process
            SocketState state = new SocketState(socketStateValue, e.ToString());
            state.OnNetworkAction(state);
            return;
        }

        // After trying to invoke BeginAcceptSocket, if some error occurs during the connection process
        try
        {
            tcpListenerValue.BeginAcceptSocket(AcceptNewClient, tuple);
        }
        catch (Exception e2)
        {
            // Notice that state has some error so that server can't begin acceptance.
            // The event-loop should not continue if an error occurs.
            SocketState state = new SocketState(socketStateValue, e2.ToString());
            state.OnNetworkAction(state);
        }
    }

    /// <summary>
    /// Stops the given TcpListener.
    /// </summary>
    public static void StopServer(TcpListener listener)
    {
        listener.Stop();
    }

    /////////////////////////////////////////////////////////////////////////////////////////
    // Client-Side Code
    /////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Begins the asynchronous process of connecting to a server via BeginConnect,
    /// and using ConnectedCallback as the method to finalize the connection once it's made.
    ///
    /// If anything goes wrong during the connection process, toCall should be invoked
    /// with a new SocketState with its ErrorOccurred flag set to true and an appropriate message
    /// placed in its ErrorMessage field. Depending on when the error occurs, this should happen either
    /// in this method or in ConnectedCallback.
    ///
    /// This connection process should timeout and produce an error (as discussed above)
    /// if a connection can't be established within 3 seconds of starting BeginConnect.
    ///
    /// </summary>
    /// <param name="toCall">The action to take once the connection is open or an error occurs</param>
    /// <param name="hostName">The server to connect to</param>
    /// <param name="port">The port on which the server is listening</param>
    public static void ConnectToServer(Action<SocketState> toCall, string hostName, int port)
    {
        // Establish the remote endpoint for the socket.
        IPHostEntry ipHostInfo;
        IPAddress ipAddress = IPAddress.None;

        // Determine if the server address is a URL or an IP
        try
        {
            ipHostInfo = Dns.GetHostEntry(hostName);
            bool foundIPV4 = false;
            foreach (IPAddress addr in ipHostInfo.AddressList)
                if (addr.AddressFamily != AddressFamily.InterNetworkV6)
                {
                    foundIPV4 = true;
                    ipAddress = addr;
                    break;
                }
            // Didn't find any IPV4 addresses
            if (!foundIPV4)
            {
                // Error, invalid IPv4 address
                toCall(new SocketState(toCall, "IPV4 is not found"));
            }
        }
        catch
        {
            // see if host name is a valid ipaddress
            try
            {
                ipAddress = IPAddress.Parse(hostName);
            }
            catch
            {
                // Error, invalid hostName
                toCall(new SocketState(toCall, "Host name is invalid."));
            }
        }

        // Create a TCP/IP socket.
        Socket socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        // This disables Nagle's algorithm (google if curious!)
        // Nagle's algorithm can cause problems for a latency-sensitive
        // game like ours will be
        socket.NoDelay = true;

        SocketState state = new SocketState(toCall, socket);


        //  If anything goes wrong during the connection process, catch exception and invoke
        //  toCall with a new SocketState with its ErrorOccurred flag set to true and error message.
        try
        {
            //https://stackoverflow.com/questions/1062035/how-to-configure-socket-connect-timeout
            IAsyncResult result = state.TheSocket.BeginConnect(ipAddress, port, ConnectedCallback, state);
            bool success = result.AsyncWaitHandle.WaitOne(3000, true);

            if (!(success && socket.Connected))
            {
                toCall(new SocketState(toCall, "Connection issue."));
                state.TheSocket.Close();
            }
        }
        catch (Exception e)
        {
            toCall(new SocketState(toCall, e.ToString()));
        }
    }

    /// <summary>
    /// To be used as the callback for finalizing a connection process that was initiated by ConnectToServer.
    ///
    /// Uses EndConnect to finalize the connection.
    ///
    /// As stated in the ConnectToServer documentation, if an error occurs during the connection process,
    /// either this method or ConnectToServer should indicate the error appropriately.
    ///
    /// If a connection is successfully established, invokes the toCall Action that was provided to ConnectToServer (above)
    /// with a new SocketState representing the new connection.
    ///
    /// </summary>
    /// <param name="ar">The object asynchronously passed via BeginConnect</param>
    private static void ConnectedCallback(IAsyncResult ar)
    {

        SocketState state = (SocketState)ar.AsyncState!;
        try
        {
            state.TheSocket.EndConnect(ar);
            // A connection is succesfully established,
            // Now that we know we are connected, we can start sending messages
            state.OnNetworkAction(state);
        }
        catch
        {
            state.ErrorOccurred = true;
            state.ErrorMessage = "Disconnected.";
        }
    }
    /////////////////////////////////////////////////////////////////////////////////////////
    // Server and Client Common Code
    /////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Begins the asynchronous process of receiving data via BeginReceive, using ReceiveCallback
    /// as the callback to finalize the receive and store data once it has arrived.
    /// The object passed to ReceiveCallback via the AsyncResult should be the SocketState.
    ///
    /// If anything goes wrong during the receive process, the SocketState's ErrorOccurred flag should
    /// be set to true, and an appropriate message placed in ErrorMessage, then the SocketState's
    /// OnNetworkAction should be invoked. Depending on when the error occurs, this should happen either
    /// in this method or in ReceiveCallback.
    /// </summary>
    /// <param name="state">The SocketState to begin receiving</param>
    public static void GetData(SocketState state)
    {
        //Begins the asynchronous process of receiving data via BeginReceive.
        //https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.beginreceive?view=net-7.0

        try
        {
            state.TheSocket.BeginReceive(state.buffer, 0, state.buffer.Length, SocketFlags.None,
                    ReceiveCallback, state);
        }
        catch (Exception e)
        {
            state.ErrorOccurred = true;
            state.ErrorMessage = e.ToString();
            state.OnNetworkAction(state);
        }
    }

    /// <summary>
    /// To be used as the callback for finalizing a receive operation that was initiated by GetData.
    ///
    /// Uses EndReceive to finalize the receive.
    ///
    /// As stated in the GetData documentation, if an error occurs during the receive process,
    /// either this method or GetData should indicate the error appropriately.
    ///
    /// If data is successfully received:
    ///  (1) Read the characters as UTF8 and put them in the SocketState's unprocessed data buffer (its string builder).
    ///      This must be done in a thread-safe manner with respect to the SocketState methods that access or modify its
    ///      string builder.
    ///  (2) Call the saved delegate (OnNetworkAction) allowing the user to deal with this data.
    /// </summary>
    /// <param name="ar">
    /// This contains the SocketState that is stored with the callback when the initial BeginReceive is called.
    /// </param>
    private static void ReceiveCallback(IAsyncResult ar)
    {

        ///  (1) Read the characters as UTF8 and put them in the SocketState's unprocessed data buffer (its string builder).
        ///      This must be done in a thread-safe manner with respect to the SocketState methods that access or modify its
        ///      string builder.
        SocketState state = (SocketState)ar.AsyncState!;

        try
        {
            int numBytes = state.TheSocket.EndReceive(ar);

            //string data = Encoding.UTF8.GetString(state.messageBuffer, 0, numBytes);
            if (numBytes > 0)
            {
                //thread-safe manner
                lock (state.data)
                {
                    state.data.Append(Encoding.UTF8.GetString(state.buffer, 0, numBytes));

                }
                ///  (2) Call the saved delegate (OnNetworkAction) allowing the user to deal with this data.
            }
            //https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.endreceive?view=net-7.0
        }
        catch (Exception e)
        {
            // Error does happen here
            state.ErrorOccurred = true;
            state.ErrorMessage = e.ToString();
        }
        state.OnNetworkAction(state);
    }

    /// <summary>
    /// Begin the asynchronous process of sending data via BeginSend, using SendCallback to finalize the send process.
    ///
    /// If the socket is closed, does not attempt to send.
    ///
    /// If a send fails for any reason, this method ensures that the Socket is closed before returning.
    /// </summary>
    /// <param name="socket">The socket on which to send the data</param>
    /// <param name="data">The string to send</param>
    /// <returns>True if the send process was started, false if an error occurs or the socket is already closed</returns>
    public static bool Send(Socket socket, string data)
    {
        if (!socket.Connected)
        {
            return false;
        }

        else
        {
            try
            {
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                //  Begin the asynchronous process of sending data via BeginSend
                //  using SendCallback to finalize the send process.
                socket.BeginSend(dataBytes, 0, dataBytes.Length, SocketFlags.None, SendCallback, socket);
                return true;
            }
            catch
            {
                /// If the socket is closed, does not attempt to send.
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                return false;
            }
        }
    }

    /// <summary>
    /// To be used as the callback for finalizing a send operation that was initiated by Send.
    ///
    /// Uses EndSend to finalize the send.
    ///
    /// This method must not throw, even if an error occurred during the Send operation.
    /// </summary>
    /// <param name="ar">
    /// This is the Socket (not SocketState) that is stored with the callback when
    /// the initial BeginSend is called.
    /// </param>
    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState!;
            /// Uses EndSend to finalize the send.
            socket.EndSend(ar);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Begin the asynchronous process of sending data via BeginSend, using SendAndCloseCallback to finalize the send process.
    /// This variant closes the socket in the callback once complete. This is useful for HTTP servers.
    ///
    /// If the socket is closed, does not attempt to send.
    ///
    /// If a send fails for any reason, this method ensures that the Socket is closed before returning.
    /// </summary>
    /// <param name="socket">The socket on which to send the data</param>
    /// <param name="data">The string to send</param>
    /// <returns>True if the send process was started, false if an error occurs or the socket is already closed</returns>
    public static bool SendAndClose(Socket socket, string data)
    {

        if (!socket.Connected)
        {
            socket.Close();
            return false;
        }

        else
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            try
            {
                //  Begin the asynchronous process of sending data via BeginSend
                //  using SendCallback to finalize the send process.
                socket.BeginSend(dataBytes, 0, dataBytes.Length, SocketFlags.None, SendAndCloseCallback, socket);
                return true;
            }
            catch
            {
                /// If the socket is closed, does not attempt to send.
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                return false;
            }
        }
    }

    /// <summary>
    /// To be used as the callback for finalizing a send operation that was initiated by SendAndClose.
    ///
    /// Uses EndSend to finalize the send, then closes the socket.
    ///
    /// This method must not throw, even if an error occurred during the Send operation.
    ///
    /// This method ensures that the socket is closed before returning.
    /// </summary>
    /// <param name="ar">
    /// This is the Socket (not SocketState) that is stored with the callback when
    /// the initial BeginSend is called.
    /// </param>
    private static void SendAndCloseCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState!;
            /// Uses EndSend to finalize the send.
            socket.EndSend(ar);
            socket.Close();
        }
        catch
        {
        }
    }
}