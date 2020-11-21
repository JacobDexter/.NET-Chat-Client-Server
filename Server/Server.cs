using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Concurrent;
using System.Runtime.Serialization.Formatters.Binary;
using Packets;

namespace Server
{
    class Server
    {
        private TcpListener _tcpListener;
        private ConcurrentBag<Client> _clients; //thread safe collection of values

        public Server(string ipAddress, int port)
        {
            IPAddress ip = IPAddress.Parse(ipAddress);
            _tcpListener = new TcpListener(ip, port);
        }

        public void Start()
        {
            _clients = new ConcurrentBag<Client>();
            _tcpListener.Start();
            //blocking function waiting for socket

            while(true)
            {
                Socket socket = _tcpListener.AcceptSocket();

                //create client instance and add to client list
                Client clientInstance = new Client(socket);
                _clients.Add(clientInstance);

                Console.WriteLine("Connection Made!");

                Thread thread = new Thread(() => { ClientMethod(clientInstance); });
                thread.Start();
            }
        }

        public void Stop()
        {
            _tcpListener.Stop();
        }

        private void ClientMethod(Client client)
        {
            Packet receivedPacket;

            Packet WelcomeMessage = new ChatMessagePacket("[Server]", "You have connected to the server!");
            //welcome message
            client.Send(WelcomeMessage);
            
            //loop to allow continuous communication
            while((receivedPacket = client.Read()) != null)
            {
                try
                {
                    switch (receivedPacket.EPacketType)
                    {
                        case PacketType.EMPTY:
                            EmptyPacket emptyPacket = (EmptyPacket)receivedPacket;
                            break;
                        case PacketType.CHATMESSAGE:
                            ChatMessagePacket chatPacket = (ChatMessagePacket)receivedPacket;
                            //print client messages to server console
                            Console.WriteLine(chatPacket.OriginClient + ": " + chatPacket.Message);
                            //print server messages to client console
                            client.Send(chatPacket);
                            break;
                        case PacketType.PRIVATEMESSAGE:
                            PrivateMessagePacket privateMessagePacket = (PrivateMessagePacket)receivedPacket;
                            break;
                        case PacketType.CLIENTNAME:
                            ClientNamePacket clientNamePacket = (ClientNamePacket)receivedPacket;
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("[Error] " + e.Message);
                    break;
                }
            }

            try
            {
                client.Close();
                _clients.TryTake(out client);
            }
            catch (Exception e)
            {
                Console.WriteLine("[Error] " + e.Message);
            }
        }

        [System.Obsolete]
        private string GetReturnMessage(string code)
        {
            if(code == "Hi")
            {
                return "Hello!";
            }
            else
            {
                return code;
            }
        }
    }
}
