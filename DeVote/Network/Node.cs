﻿using DeVote.Cryptography;
using DeVote.Extensions;
using DeVote.Network.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DeVote.Network
{
    public class Node : Client
    {
        public long ConsensusRN = long.MaxValue;
        public string MachineID = string.Empty;
        public bool FullNode = false;
        public Node(string ip, int port) : base(ip, port) { }
        public Node(string endPoint) : base(IPEndPoint.Parse(endPoint)) { }
        private Node(TcpClient client) : base(client) { }

        public async void Start()
        {
            while (!Connected)
                Console.WriteLine("Waiting to get connected to " + EndPoint);

            NetworkManager.AddNode(EndPoint, this);

            await Read();
        }
        async Task Read()
        {
            await Task.Yield();

            // Temp buffer
            byte[] buffer;

            try
            {
                while (true)
                {
                    if (Stream.CanRead)
                    {
                        int bytesRead = Stream.Read(Buffer, Index, BufferSize);
                        if (bytesRead > 0)
                        {
                            Index += bytesRead;

                            // Check for footer, to know wether we recieved the full package or not
                            if ((buffer = Buffer.Take(Index).ToArray()).EndsWith(Constants.EOTFlag))
                            {
                                // Add the packet to the handler to be handled
                                PacketsHandler.Packets.Enqueue(new KeyValuePair<Node, byte[]>(this, buffer.SkipLast(Constants.EOTFlag.Length).ToArray()));

                                // Clear buffer 
                                Array.Clear(Buffer, 0, Buffer.Length);

                                // Clear index
                                Index = 0;
                            }

                            // Keep recieving.  
                        }
                    }
                }
            }
            catch (System.IO.IOException e)
            {
                if (e.InnerException != null)
                {
                    if (e.InnerException is SocketException)
                    {
                        if ((e.InnerException as SocketException).ErrorCode == 10054)
                        {
                            NetworkManager.RemoveNode(EndPoint);
                            Console.WriteLine(EndPoint + " forcibly disconnected");
                            return;
                        }
                    }
                    else throw e.InnerException;
                }
                Console.WriteLine(e.ToString());
            }
            catch (SocketException e)
            {
                if (e.ErrorCode == 10054)
                {
                    NetworkManager.RemoveNode(EndPoint);
                    Console.WriteLine(EndPoint + " forcibly disconnected");
                }
                else throw e;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Send(string data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.  
            Send(byteData);
        }
        public void Send(byte[] byteData, bool encrypt = true)
        {
            try
            {
                // Encrypt data if possible
                if (AES.Key != null && encrypt)
                    byteData = AES.Encrypt(byteData);

                // Begin sending the data to the remote device.  
                Stream.WriteAsync(byteData.Concat(Constants.EOTFlag).ToArray());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        public static implicit operator Node(TcpClient client) => new Node(client);
    }
}
