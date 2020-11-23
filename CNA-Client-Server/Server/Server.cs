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
        private ConcurrentDictionary<int, Client> _clients; //thread safe collection of values

        public Server(string ipAddress, int port)
        {
            IPAddress ip = IPAddress.Parse(ipAddress);
            _tcpListener = new TcpListener(ip, port);
        }

        public void Start()
        {
            _clients = new ConcurrentDictionary<int, Client>();
            _tcpListener.Start();
            //blocking function waiting for socket

            int clientIndex = 0;

            while(true)
            {
                try
                {
                    int index = clientIndex;
                    clientIndex++;

                    Socket socket = _tcpListener.AcceptSocket();

                    //create client instance and add to client list
                    Client clientInstance = new Client(socket, "Client " + (index + 1));
                    _clients.TryAdd(index, clientInstance);

                    Console.WriteLine("Connection Made!");
                    
                    //Welcome to server message
                    ServerMessagePacket WelcomeMessage = new ServerMessagePacket("You have connected to the server!");
                    clientInstance.Send(WelcomeMessage);

                    //send join message to all clients
                    for (int i = 0; i < _clients.Count; i++)
                    {
                        ServerMessagePacket serverMessagePacket = new ServerMessagePacket(clientInstance.clientData.clientNickname + " has joined the server!");
                        _clients[i].Send(serverMessagePacket);
                    }

                    //start packet interpreted
                    Thread thread = new Thread(() => { ClientMethod(index); });
                    thread.Start();
                }
                catch (Exception e)
                {
                    Console.WriteLine("[Error] " + e.Message);
                    break;
                }
            }
        }

        public void Stop()
        {
            _tcpListener.Stop();
        }

        private void ClientMethod(int index)
        {
            Packet receivedPacket;
            
            //loop to allow continuous communication
            while((receivedPacket = _clients[index].Read()) != null)
            {
                try
                {
                    switch (receivedPacket.EPacketType)
                    {
                        case PacketType.EMPTY:
                            EmptyPacket emptyPacket = (EmptyPacket)receivedPacket;
                            break;
                        case PacketType.CHATMESSAGE:
                            ChatMessagePacket chatMessagePacket = (ChatMessagePacket)receivedPacket;
                            try
                            {
                                chatMessagePacket.OriginClient = _clients[index].clientData.clientNickname;
                                //send messages to all clients
                                for (int i = 0; i < _clients.Count; i++)
                                {
                                    _clients[i].Send(chatMessagePacket);
                                }

                                //print client messages to server console
                                Console.WriteLine(chatMessagePacket.Time + " " + _clients[index].clientData.clientNickname + ": " + chatMessagePacket.Message);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("[Error] " + e.Message);
                                break;
                            }
                            break;
                        case PacketType.PRIVATEMESSAGE:
                            PrivateMessagePacket privateMessagePacket = (PrivateMessagePacket)receivedPacket;
                            break;
                        case PacketType.COMMANDMESSAGE:
                            CommandMessagePacket commandMessagePacket = (CommandMessagePacket)receivedPacket;

                            //Process command
                            ProcessCommand(_clients[index], commandMessagePacket.Message);
                            break;
                        case PacketType.CLIENTNAME:
                            ClientNamePacket clientNamePacket = (ClientNamePacket)receivedPacket;
                            break;
                        case PacketType.DISCONNECTREQUEST:
                            DisconnectRequestPacket disconnectRequestPacket = (DisconnectRequestPacket)receivedPacket;
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
                _clients[index].Close();

                //Take client out of client ConcurrentBag
                _clients.TryRemove(index, out Client c);
            }
            catch (Exception e)
            {
                Console.WriteLine("[Error] " + e.Message);
            }
        }

        private void ProcessCommand(Client client, string command)
        {
            try
            {
                ServerMessagePacket serverMessagePacket;
                ChatMessagePacket chatMessagePacket;

                switch (command)
                {
                    case "/hello":
                        //send client chat message
                        chatMessagePacket = new ChatMessagePacket(client.clientData.clientNickname + ": ", command);
                        client.Send(chatMessagePacket);

                        //send server reponse
                        serverMessagePacket = new ServerMessagePacket("Hello!");
                        client.Send(serverMessagePacket);
                        break;
                    default:
                        //send client chat message
                        chatMessagePacket = new ChatMessagePacket(client.clientData.clientNickname + ": ", command);
                        client.Send(chatMessagePacket);

                        //send server reponse
                        serverMessagePacket = new ServerMessagePacket("Sorry that is not a command!");
                        client.Send(serverMessagePacket);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[Error] " + e.Message);
            }
        }
    }
}
