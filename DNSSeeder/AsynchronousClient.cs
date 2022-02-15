﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DNSSeeder
{
    public class AsynchronousClient
    {
        public string[] Addresses;
        // ManualResetEvent instances signal completion.  
        private static ManualResetEvent connectDone = 
            new ManualResetEvent(false), sendDone = 
            new ManualResetEvent(false), receiveDone = 
            new ManualResetEvent(false);

        string SeederHost;
        int SeederPort;

        // The response from the remote device.  
        private static String response = String.Empty;

        /// <summary>
        /// The constructor for the Seeder Client, host and port for the DNS Seeder are required
        /// Note: dnsseeder.ddns.net is a Dynamio DNS I created with NO-IP.
        /// NO-IP is a Free Dynamic DNS and Managed DNS Provider
        /// </summary>
        /// <param name="host">DNS Seeder host that the client will connect to</param>
        /// <param name="port">DNS Seeder port</param>
        public AsynchronousClient(string host = "dnsseeder.ddns.net", int port = 6942)
        {
            Addresses = new string[0];
            SeederHost = host;
            SeederPort = port;
        }

        /// <summary>
        /// Start the Seeder Client
        /// </summary>
        /// <param name="joinSeeder">Set this to true, to add the address to the list in the Seeder</param>
        public void StartClient(bool joinSeeder)
        {
            //Resolving the DNS Seeder host to get the acutal IP of our Seeder. 
            IPHostEntry ipHostInfo = Dns.GetHostEntry(SeederHost);
            IPAddress ipAddress = ipHostInfo.AddressList[0];

            // Establish the remote endpoint for the socket. IPAddress.Parse("127.0.0.1")
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), SeederPort);

            // Create a TCP/IP socket.  
            Socket client = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            // Connect to the remote endpoint.  
            client.BeginConnect(remoteEndPoint,
                new AsyncCallback(ConnectCallback), client);
            connectDone.WaitOne();

            // Send test data to the remote device.  
            Send(client, new byte[1] { joinSeeder ? (byte)1 : (byte)0 });
            sendDone.WaitOne();

            // Receive the response from the remote device.  
            Receive(client);
            receiveDone.WaitOne();

            Addresses = string.IsNullOrEmpty(response) ? new string[] { } : response.Split(Environment.NewLine);
            // Write the response to the console.  
            Console.WriteLine("Addresses received : {0}", Addresses.Length);

            // Release the socket.  
            client.Shutdown(SocketShutdown.Both);
            client.Close();
        }
        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.  
                client.EndConnect(ar);

                Console.WriteLine("Socket connected to {0}",
                    client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.  
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Receive(Socket client)
        {
            try
            {
                // Create the state object.  
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.  
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket
                // from the asynchronous state object.  
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.  
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.  
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // Get the rest of the data.  
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    // All the data has arrived; put it in response.  
                    if (state.sb.Length > 1)
                    {
                        response = state.sb.ToString();
                    }
                    // Signal that all bytes have been received.  
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            Send(client, byteData);
        }
        private static void Send(Socket client, byte[] byteData)
        {
            // Begin sending the data to the remote device.  
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                // Signal that all bytes have been sent.  
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
