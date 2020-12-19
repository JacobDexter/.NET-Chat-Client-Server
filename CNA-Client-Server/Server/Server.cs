using Packets;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace Server
{
    class Server
    {
        private TcpListener _tcpListener;
        private UdpClient _udpListener;
        private ConcurrentDictionary<int, Client> _clients; //thread safe collection of values
        private BinaryFormatter _binaryFormatter;
        private int clientIndex = 0;
        private StreamWriter _logWriter;
        Encoding UTF8 = Encoding.UTF8;

        public Server(string ipAddress, int port)
        {
            //setup file log writing
            string filepath = "Server Log - " + DateTime.Now.ToString("dd-MM-yyyy hh-mm tt") + ".txt";
            _logWriter = new StreamWriter(new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite));
            OutputLog(DateTime.Now + " [Server] -> Server Started!");
            OutputLog(DateTime.Now + " [Server] -> Server File Logging!");
            OutputLog(DateTime.Now + " [Server] -> File Path: " + filepath);

            //setup connections
            IPAddress ip = IPAddress.Parse(ipAddress);
            _tcpListener = new TcpListener(ip, port);
            _udpListener = new UdpClient(port);
            OutputLog(DateTime.Now + " [Server] -> Connection Protocols Setup!");

            //encryptions
            _binaryFormatter = new BinaryFormatter();
            OutputLog(DateTime.Now + " [Server] -> Encryption Device Setup!");
        }

        public void Start()
        {
            _clients = new ConcurrentDictionary<int, Client>();
            _tcpListener.Start();
            OutputLog(DateTime.Now + " [Server] -> TCP Listener Started!");
            //blocking function waiting for socket

            OutputLog(DateTime.Now + " [Server] -> Main Server Loop Started!");
            while (true)
            {
                try
                {
                    int index = clientIndex;

                    Socket socket = _tcpListener.AcceptSocket();

                    //create client instance and add to client list
                    Client clientInstance = new Client(socket, "Client" + ++clientIndex);
                    _clients.TryAdd(index, clientInstance);

                    OutputLog(DateTime.Now + " [Server] -> " + clientInstance.clientData.clientNickname + " has joined the server!");
                    
                    //start tcp packet interpreter
                    Thread tcpThread = new Thread(() => { TCPClientMethod(index); });
                    tcpThread.Start();

                    //start udp packet interpreter
                    Thread udpThread = new Thread(() => { UDPListen(); });
                    udpThread.Start();
                }
                catch (Exception e)
                {
                    OutputLog("[Error] " + e.Message + e.StackTrace);
                    break;
                }
            }

            Stop();
        }

        public void Stop()
        {
            _tcpListener.Stop();
            _udpListener.Close();
            _logWriter.Dispose();
        }

        private void TCPClientMethod(int index)
        {
            Packet receivedPacket;
            
            //loop to allow continuous communication
            while((receivedPacket = _clients[index].TCPRead()) != null)
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
                                //reencrypt data into new packet so that all the clients are able to decrypt it with their private key
                                ChatMessagePacket sendPacket = new ChatMessagePacket(UTF8.GetBytes(_clients[index].clientData.clientNickname), chatMessagePacket.Message)
                                {
                                    Time = chatMessagePacket.Time
                                };

                                //send messages to all clients
                                TCPSendPacketToAll(sendPacket);

                                //print client messages to server console
                                OutputLog(chatMessagePacket.Time + " " + _clients[index].clientData.clientNickname + ": " + UTF8.GetString(chatMessagePacket.Message));
                            }
                            catch (Exception e)
                            {
                                OutputLog("[Error] " + e.Message + e.StackTrace);
                                break;
                            }
                            break;

                        case PacketType.COMMANDMESSAGE:
                            CommandMessagePacket commandMessagePacket = (CommandMessagePacket)receivedPacket;

                            //Process command
                            ProcessCommand(_clients[index], UTF8.GetString(commandMessagePacket.Message));
                            break;


                        case PacketType.CLIENTNAME:
                            ClientNamePacket clientNamePacket = (ClientNamePacket)receivedPacket;

                            bool uniqueName = true;

                            for(int i = 0; i < _clients.Count; i++)
                            {
                                if(UTF8.GetString(clientNamePacket.ClientName) == _clients[i].clientData.clientNickname)
                                {
                                    uniqueName = false;
                                }
                            }

                            if(uniqueName)
                            {
                                OutputLog("[Nickname] " + _clients[index].clientData.clientNickname + " has changed their nickname to " + UTF8.GetString(clientNamePacket.ClientName) + ".");
                                _clients[index].clientData.clientNickname = UTF8.GetString(clientNamePacket.ClientName);

                                //Refresh client list
                                ClientListPacket clientListPacket = new ClientListPacket(GetClientNames(), GetClientAddresses());
                                TCPSendPacketToAll(clientListPacket);

                                //Send name change confirmation
                                ServerMessagePacket serverMessagePacket = new ServerMessagePacket(UTF8.GetBytes("Your server nickname has been changed to " + UTF8.GetString(clientNamePacket.ClientName) + "!"));
                                _clients[index].TCPSend(serverMessagePacket);
                            }
                            else
                            {
                                //Send name change failed
                                ServerMessagePacket serverMessagePacket = new ServerMessagePacket(UTF8.GetBytes("Someone on this server already has the nickname '" + UTF8.GetString(clientNamePacket.ClientName) + "'. Please choose a different nickname!"));
                                _clients[index].TCPSend(serverMessagePacket);
                            }

                            break;


                        case PacketType.DISCONNECTREQUEST:
                            DisconnectRequestPacket disconnectRequestPacket = (DisconnectRequestPacket)receivedPacket;
                            //Announce client has joined to all clients
                            ServerMessagePacket serverMessagePacket3 = new ServerMessagePacket(UTF8.GetBytes(_clients[index].clientData.clientNickname + " has left the server!"));
                            TCPSendPacketToAll(serverMessagePacket3);
                            _clients[index].Close();
                            break;


                        case PacketType.LOGIN:
                            LoginPacket loginPacket = (LoginPacket)receivedPacket;
                            try
                            {
                                //set server client ip and client key
                                _clients[index].clientData.ipEndPoint = loginPacket.EndPoint;
                                _clients[index]._clientKey = loginPacket.PublicKey;

                                //send server public key back
                                ServerKeyPacket serverKeyPacket = new ServerKeyPacket(_clients[index]._publicKey, true);
                                _clients[index].TCPSend(serverKeyPacket);
                                _clients[index].successfulLogin = true;

                                //Announce client has joined to all clients
                                ServerMessagePacket serverMessagePacket2 = new ServerMessagePacket(UTF8.GetBytes(_clients[index].clientData.clientNickname + " has joined the server!"));
                                TCPSendPacketToAll(serverMessagePacket2);

                                //Refresh client list
                                ClientListPacket clientListPacket2 = new ClientListPacket(GetClientNames(), GetClientAddresses());
                                TCPSendPacketToAll(clientListPacket2);
                                break;
                            }
                            catch (Exception e)
                            {
                                OutputLog("[Error] " + e.Message + e.StackTrace);
                                break;
                            }
                    }
                }
                catch (Exception e)
                {
                    OutputLog("[Error] " + e.Message + e.StackTrace);
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
                OutputLog("[Error] " + e.Message + e.StackTrace);
            }
        }

        private void TCPSendPacketToAll(Packet packet)
        {
            foreach (Client c in _clients.Values)
            {
                c.TCPSend(packet);
            }
        }

        private string[] GetClientNames()
        {
            string[] clients = new string[_clients.Count];
            int loopCount = 0;

            foreach (Client c in _clients.Values)
            {
                clients[loopCount] = c.clientData.clientNickname;
                loopCount++;
            }

            return clients;
        }

        private string[] GetClientAddresses()
        {
            string[] ips = new string[_clients.Count];
            int loopCount = 0;

            foreach (Client c in _clients.Values)
            {
                ips[loopCount] = c.clientData.ipEndPoint.ToString();
            }

            return ips;
        }

        private void UDPListen()
        {
            try
            {
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 0);

                while (true)
                {
                    byte[] bytes = _udpListener.Receive(ref ipEndPoint);
                    MemoryStream memoryStream = new MemoryStream(bytes);
                    Packet receivedPacket = _binaryFormatter.Deserialize(memoryStream) as Packet;

                    try
                    {
                        switch (receivedPacket.EPacketType)
                        {
                            case PacketType.LOGIN:
                                break;
                            default:
                                break;
                        }
                    }
                    catch (SocketException e)
                    {
                        OutputLog("[Error] " + e.Message + e.StackTrace);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                OutputLog("[Error] " + e.Message + e.StackTrace);
            }
        }

        private void ProcessCommand(Client client, string command)
        {
            try
            {
                ServerMessagePacket serverMessagePacket;
                ChatMessagePacket chatMessagePacket;

                string[] parameters = SplitCommandIntoParameters(command);

                //////////////////////////////////

                switch ((parameters[0].ToUpper()).TrimStart('/'))
                {
                    case "HELLO":
                        //send server reponse
                        serverMessagePacket = new ServerMessagePacket(UTF8.GetBytes("Hello!"));
                        client.TCPSend(serverMessagePacket);
                        break;

                    case "PM":

                        int clientDestination = -1;

                        for(int i = 0; i < _clients.Count; i++)
                        {
                            if(parameters[1] == _clients[i].clientData.clientNickname)
                            {
                                //check if the destination is the same as the sender
                                if(_clients[i] == client)
                                {
                                    clientDestination = -2;
                                    break;
                                }
                                else
                                {
                                    clientDestination = i;
                                    break;
                                }
                            }
                        }

                        //check if there is a message
                        if(parameters[2] == null)
                        {
                            ServerMessagePacket serverMessagePacket1 = new ServerMessagePacket(UTF8.GetBytes("Please put a message after the nickname!"));
                            client.TCPSend(serverMessagePacket1);
                        }
                        //check if client was found with name
                        else if(clientDestination > -1)
                        {
                            //send private message to destination client
                            PrivateMessagePacket privateMessagePacket = new PrivateMessagePacket(UTF8.GetBytes(client.clientData.clientNickname), UTF8.GetBytes(parameters[2].ToString()));
                            _clients[clientDestination].TCPSend(privateMessagePacket);

                            //send confirmation to user that their private message has been sent and has arrived
                            PrivateMessagePacket privateMessagePacket1 = new PrivateMessagePacket(UTF8.GetBytes(parameters[2].ToString()), UTF8.GetBytes(_clients[clientDestination].clientData.clientNickname));
                            client.TCPSend(privateMessagePacket1);

                            //log pm to server console
                            OutputLog(privateMessagePacket.Time + " [PM] " + UTF8.GetString(privateMessagePacket.OriginClient) + " -> " + _clients[clientDestination].clientData.clientNickname + ": " + UTF8.GetString(privateMessagePacket.PrivateMessage));
                        }
                        else if (clientDestination == -1)
                        {
                            ServerMessagePacket serverMessagePacket2 = new ServerMessagePacket(UTF8.GetBytes("Sorry that client does not exist. Please try again with a real nickname!"));
                            client.TCPSend(serverMessagePacket2);
                        }
                        else if (clientDestination == -2)
                        {
                            ServerMessagePacket serverMessagePacket3 = new ServerMessagePacket(UTF8.GetBytes("You can't send private messages to yourself silly!"));
                            client.TCPSend(serverMessagePacket3);
                        }
                        break;

                    case "HELP":
                        ServerMessagePacket serverMessagePacket4 = new ServerMessagePacket(UTF8.GetBytes("\nCommands: \n/HELLO - Hello! \n/PM [NAME] [MSG] - Send private messages! \n/ROLLDICE [NumOfDices] [NumOfFaces] - Roll dice! \n/ANNOUNCE [MSG] - Send an announcement message to everyone on the server!"));
                        client.TCPSend(serverMessagePacket4);
                        break;

                    case "ROLLDICE":
                        ServerMessagePacket serverMessagePacket5;
                        if (!(String.IsNullOrEmpty(parameters[1]) || String.IsNullOrEmpty(parameters[2])))
                        {
                            serverMessagePacket5 = new ServerMessagePacket(UTF8.GetBytes(RollDice(Int32.Parse(parameters[1]), Int32.Parse(parameters[2]))));
                        }
                        else
                        {
                            serverMessagePacket5 = new ServerMessagePacket(UTF8.GetBytes("Please input a value for all parameters!"));
                            OutputLog(client.clientData.clientNickname + " has tried to roll dice without the correct number of parameters! (" + command + ")");
                        }
                        client.TCPSend(serverMessagePacket5);
                        break;

                    case "ANNOUNCE":
                        //get full announcement message
                        int start = command.IndexOf(" ");
                        string msg = command.Substring(start + 1, command.Length - (start + 1));

                        //send announcement message to all clients and server logs
                        AnnouncementMessagePacket announcementMessagePacket = new AnnouncementMessagePacket(UTF8.GetBytes(msg));
                        TCPSendPacketToAll(announcementMessagePacket);
                        OutputLog(msg);
                        break;

                    default:
                        //send client chat message
                        chatMessagePacket = new ChatMessagePacket(UTF8.GetBytes(client.clientData.clientNickname + ": "), UTF8.GetBytes(command));
                        client.TCPSend(chatMessagePacket);

                        //send server reponse
                        serverMessagePacket = new ServerMessagePacket(UTF8.GetBytes("Sorry that is not a command! For a list of commands type '/help'."));
                        client.TCPSend(serverMessagePacket);
                        break;
                }
            }
            catch (Exception e)
            {
                OutputLog("[Error] " + e.Message + e.StackTrace);
            }
        }

        //Commands

        private string[] SplitCommandIntoParameters(string command)
        {
            string[] parameters = new string[3];

            //Split up command and parameters
            if (command.Length > 0 && command.Contains(" "))
            {
                //get command
                int i = command.IndexOf(" ");
                parameters[0] = command.Substring(0, i);

                if (command.Length > parameters[0].Length)
                {
                    //remove command text from string to have only parameters
                    string temp = command.Trim((parameters[0] + " ").ToCharArray());

                    if (temp.Contains(" "))
                    {
                        //get 1st param
                        parameters[1] = temp.Substring(0, temp.IndexOf(" ")); //sets 1st param if there is the potential of a 2nd param

                        //remove param1 string from string to leave the 2nd param
                        temp = temp.TrimStart((parameters[1] + " ").ToCharArray());

                        //check if param2 was the same as param1 (if it is temp should contain nothing)
                        if (temp == "")
                        {
                            parameters[2] = parameters[1];
                        }
                        else
                        {
                            parameters[2] = temp.Trim();
                        }
                    }
                    else
                    {
                        parameters[1] = temp; //sets 1st param if there isnt a 2nd param
                    }
                }
            }
            else
            {
                parameters[0] = command;
            }

            return parameters;
        }
        
        private string RollDice(int numOfDice, int numOfSides)
        {
            try
            {
                if (numOfDice < 1)
                    return "There must be atleast 1 dice to roll!";
                else if (numOfDice > 10)
                    return "You can't roll more than 10 dice at the same time!";

                if (numOfSides < 6)
                    return "The dice must have atleast 6 sides!";
                else if (numOfSides > 120)
                    return "The dice can't have more sides then 120!";

                var rand = new Random();
                int[] nums = new int[numOfDice];
                int sum = 0;
                string diceNumString = "";

                for (int i = 0; i < numOfDice; i++)
                {
                    nums[i] = rand.Next(1, numOfSides);
                    sum += nums[i];
                    diceNumString += " [" + nums[i] + "]";
                }

                return "The numbers of your die were" + diceNumString + ". They have a sum of " + sum + ".";
            }
            catch(Exception)
            {
                return "Please input a value for all parameters!";
            }
        }

        private void OutputLog(string msg)
        {
            try
            {
                Console.WriteLine(msg);
                _logWriter.Write(msg + _logWriter.NewLine);
                _logWriter.Flush();
            }
            catch(Exception e)
            {
                Console.WriteLine("[Error] " + e.Message + e.StackTrace);
            }
        }
    }
}
