using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using Packets;

namespace Client
{
    public class Client
    {
        //threads
        private Thread _tcpThread;
        private Thread _udpThread;

        //data transfer protocols
        private TcpClient _tcpClient;
        private UdpClient _udpClient;

        //connection (read/write)
        private NetworkStream _stream;
        private BinaryWriter _writer;
        private BinaryReader _reader;
        private BinaryFormatter _binaryFormatter;

        //UI
        ClientForm _clientForm;

        //thread management
        private object _encryptLock;
        private object _decryptLock;

        //encryption
        Encoding UTF8 = Encoding.UTF8;
        private RSACryptoServiceProvider _RSAProvider;
        private RSAParameters _publicKey;
        private RSAParameters _privateKey;
        private RSAParameters _serverKey;
        private bool successfulLogin;

        public Client()
        {
            _encryptLock = new object();
            _decryptLock = new object();

            _RSAProvider = new RSACryptoServiceProvider(2048);
            _publicKey = _RSAProvider.ExportParameters(false);
            _privateKey = _RSAProvider.ExportParameters(true);
        }

        //CONNECT/DISCONNECT FUNCTIONALITY

        public bool Connect(string ipAddress, int port)
        {
            try
            {
                //initiate transfer protocols
                _tcpClient = new TcpClient();
                _udpClient = new UdpClient();
                _tcpClient.Connect(ipAddress, port);
                _udpClient.Connect(ipAddress, port);

                _stream = _tcpClient.GetStream();
                _writer = new BinaryWriter(_stream, Encoding.UTF8);
                _reader = new BinaryReader(_stream, Encoding.UTF8);
                _binaryFormatter = new BinaryFormatter();
                return true;
            }
            catch(Exception e)
            {
                Console.WriteLine("Exception: " + e.Message + e.StackTrace);
                return false;
            }
        }

        public void Login()
        {
            try
            {
                LoginPacket loginPacket = new LoginPacket((IPEndPoint)_udpClient.Client.LocalEndPoint, _publicKey);
                TCPSerialize(loginPacket);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message + e.StackTrace);
            }
        }

        //TCP FUNCTIONALITY

        public void Run()
        {
            _clientForm = new ClientForm(this);

            //start TCP thread for recieving packets
            _tcpThread = new Thread(() => { TCPProcessServerResponse(); });
            _tcpThread.Start();

            _udpThread = new Thread(() => { UDPProcessServerResponse(); });
            _udpThread.Start();

            Login();

            _clientForm.ShowDialog(); //show window
        }

        private void TCPProcessServerResponse()
        {
            Packet receivedPacket;

            while ((receivedPacket = TCPRead()) != null)
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

                            //Write message to client console
                            Console.WriteLine(chatMessagePacket.Time + " " + UTF8.GetString(chatMessagePacket.OriginClient) + ": " + UTF8.GetString(chatMessagePacket.Message));

                            //Write message to client form
                            _clientForm.UpdateChatWindow(chatMessagePacket.Time + " " + UTF8.GetString(chatMessagePacket.OriginClient) + ": " + UTF8.GetString(chatMessagePacket.Message));
                            break;
                        case PacketType.PRIVATEMESSAGE:
                            PrivateMessagePacket privateMessagePacket = (PrivateMessagePacket)receivedPacket;

                            //Write pm to client console
                            Console.WriteLine(privateMessagePacket.Time + " [PM] " + UTF8.GetString(privateMessagePacket.OriginClient) + " -> " + UTF8.GetString(privateMessagePacket.PrivateMessage));

                            //Write message to client form
                            _clientForm.UpdateChatWindow(privateMessagePacket.Time + " [PM] " + UTF8.GetString(privateMessagePacket.OriginClient) + " -> " + UTF8.GetString(privateMessagePacket.PrivateMessage));
                            break;
                        case PacketType.SERVERMESSAGE:
                            ServerMessagePacket serverMessagePacket = (ServerMessagePacket)receivedPacket;

                            //Write server response to client console
                            Console.WriteLine(serverMessagePacket.Time + " [Server] -> " + UTF8.GetString(serverMessagePacket.Message));

                            //Write server response to client form
                            _clientForm.UpdateChatWindow(serverMessagePacket.Time + " [Server] -> " + UTF8.GetString(serverMessagePacket.Message));
                            break;
                        case PacketType.ANNOUNCEMESSAGE:
                            AnnouncementMessagePacket announcementMessagePacket = (AnnouncementMessagePacket)receivedPacket;

                            //Write announcement to client console
                            Console.WriteLine(announcementMessagePacket.Time + " " + "[Announcement] -> " + UTF8.GetString(announcementMessagePacket.Message));

                            //Write announcement to client form
                            _clientForm.UpdateChatWindow(announcementMessagePacket.Time + " " + "[Announcement] -> " + UTF8.GetString(announcementMessagePacket.Message));
                            break;
                        case PacketType.SERVERKEY:
                            ServerKeyPacket serverKeyPacket = (ServerKeyPacket)receivedPacket;
                            _serverKey = serverKeyPacket.ServerKey;
                            successfulLogin = serverKeyPacket.Successful;
                            Console.WriteLine("Server Key Packet Received!");
                            break;
                        case PacketType.CLIENTLIST:
                            ClientListPacket clientListPacket = (ClientListPacket)receivedPacket;
                            _clientForm.RefreshClientList(clientListPacket.ClientList, clientListPacket.ClientIPS);
                            break;
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine("[Error] " + e.Message + e.StackTrace);
                    break;
                }
            }
        }

        public void TCPSendMessage(string message)
        {
            if (_tcpClient.Connected)
            {
                //checks if message is a command
                if (!message.StartsWith("/"))
                {
                    //create message packet
                    ChatMessagePacket packet = new ChatMessagePacket(EncryptString("Default"), EncryptString(message));

                    //serialize and send packet to all clients
                    TCPSerialize(packet);
                }
                else
                {
                    //creates command message packet
                    CommandMessagePacket packet = new CommandMessagePacket(EncryptString(message));

                    //send message to client console
                    Console.WriteLine(message);

                    //serialize and send packet to all clients
                    TCPSerialize(packet);
                }
            }
        }

        public Packet TCPRead()
        {
            try
            {
                int numberOfBytes;
                if ((numberOfBytes = _reader.ReadInt32()) != -1)
                {
                    byte[] buffer = _reader.ReadBytes(numberOfBytes);
                    MemoryStream memoryStream = new MemoryStream(buffer);
                    Packet packet = _binaryFormatter.Deserialize(memoryStream) as Packet;

                    switch(packet.EPacketType)
                    {
                        case PacketType.CHATMESSAGE:
                            ChatMessagePacket chatMessagePacket = (ChatMessagePacket)packet;
                            return new ChatMessagePacket(Decrypt(chatMessagePacket.OriginClient), Decrypt(chatMessagePacket.Message));
                        case PacketType.SERVERMESSAGE:
                            ServerMessagePacket serverMessagePacket = (ServerMessagePacket)packet;
                            return new ServerMessagePacket(Decrypt(serverMessagePacket.Message));
                        case PacketType.PRIVATEMESSAGE:
                            PrivateMessagePacket privateMessagePacket = (PrivateMessagePacket)packet;
                            return new PrivateMessagePacket(Decrypt(privateMessagePacket.OriginClient), Decrypt(privateMessagePacket.PrivateMessage));
                        case PacketType.ANNOUNCEMESSAGE:
                            AnnouncementMessagePacket announcementMessagePacket = (AnnouncementMessagePacket)packet;
                            return new AnnouncementMessagePacket(Decrypt(announcementMessagePacket.Message));
                    }

                    return packet;
                }
                else
                {
                    return null;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("[Error] " + e.Message + e.StackTrace);
                return null;
            }
        }

        private void TCPSerialize(Packet packet)
        {
            MemoryStream memoryStream = new MemoryStream();
            _binaryFormatter.Serialize(memoryStream, packet);
            byte[] buffer = memoryStream.GetBuffer();
            _writer.Write(buffer.Length);
            _writer.Write(buffer);
            _writer.Flush();
        }

        //UDP FUNCTIONALITY
        public void UDPSendMessage(Packet packet, bool encrypted)
        {
            //serialize and send packet
            UDPSerialize(packet, encrypted);
        }

        private void UDPProcessServerResponse()
        {
            try
            {
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 0);

                while(true)
                {
                    byte[] bytes = _udpClient.Receive(ref ipEndPoint);
                    MemoryStream memoryStream = new MemoryStream(bytes);
                    Packet receivedPacket = _binaryFormatter.Deserialize(memoryStream) as Packet;

                    try
                    {
                        switch (receivedPacket.EPacketType)
                        {
                            case PacketType.EMPTY:
                                break;
                            default:
                                break;
                        }
                    }
                    catch (SocketException e)
                    {
                        Console.WriteLine("[Error] " + e.Message + e.StackTrace);
                        break;
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("[Error] " + e.Message + e.StackTrace);
            }
        }

        private void UDPSerialize(Packet packet, bool encrypted)
        {
            MemoryStream memoryStream = new MemoryStream();
            _binaryFormatter.Serialize(memoryStream, packet);
            byte[] buffer = memoryStream.GetBuffer();
            _udpClient.Send(buffer, buffer.Length);
        }

        private void Close()
        {
            _stream.Close();
            _udpClient.Close();
            _tcpClient.Close();
        }

        public void DisconnectClient()
        {
            DisconnectRequestPacket disconnectRequestPacket = new DisconnectRequestPacket();
            TCPSerialize(disconnectRequestPacket);
            Close();
        }

        public void SetNickname(string nickname)
        {
            if(nickname.Length >= 3 &&  !nickname.Contains(" ") && nickname.Length <= 11)
            {
                ClientNamePacket clientNamePacket = new ClientNamePacket(EncryptString(nickname));
                TCPSerialize(clientNamePacket);
                return;
            }
            
            //check if it is too over 10 characters
            if(nickname.Length > 11)
            {
                _clientForm.UpdateChatWindow("[Error] Your nickname cannot be longer than 11 characters!");
                _clientForm.Nickname.Focus();
            }

            //check if it has spaces
            if(nickname.Contains(" "))
            {
                _clientForm.UpdateChatWindow("[Error] Your nickname cannot have spaces!");
                _clientForm.Nickname.Focus();
            }

            //check if nickname is too small or not inputted
            if(nickname.Length < 3 && nickname != "")
            {
                _clientForm.UpdateChatWindow("[Error] Your nickname must be atleast 3 characters long!");
                _clientForm.Nickname.Focus();
            }
            else if(nickname == "")
            {
                _clientForm.UpdateChatWindow("[Error] Please enter a nickname!");
                _clientForm.Nickname.Focus();
            }

            _clientForm.Nickname.Focus();
        }

        private byte[] Encrypt(byte[] data)
        {
            //encrypt data using the client public key
            _RSAProvider.ImportParameters(_serverKey);
            return _RSAProvider.Encrypt(data, true);
        }

        private byte[] Decrypt(byte[] data)
        {
            //decrypt data using the client private key
            _RSAProvider.ImportParameters(_privateKey);
            return _RSAProvider.Decrypt(data, true);
        }

        private byte[] EncryptString(string message)
        {
            byte[] buffer = UTF8.GetBytes(message);

            if (buffer.Length > 0)
            {
                return Encrypt(buffer);
            }

            return null;
        }

        private string DecryptString(byte[] message)
        {
            byte[] buffer = Decrypt(message);

            if (buffer.Length > 0)
            {
                return UTF8.GetString(buffer);
            }

            return null;
        }

        private byte[] EncryptInt(int integer)
        {
            byte[] buffer = BitConverter.GetBytes(integer);

            if (buffer.Length > 0)
            {
                return Encrypt(buffer);
            }

            return null;
        }

        private int DecryptInt(byte[] integer)
        {
            byte[] buffer = Decrypt(integer);

            if (buffer.Length > 0)
            {
                return BitConverter.ToInt32(buffer, 0); ;
            }

            return 0;
        }
    }
}
