using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using Packets;

namespace Server
{
    class Client
    {
        public struct ClientData
        {
            public string clientNickname;
            public IPEndPoint ipEndPoint;
        }

        //connection (read/write)
        private Socket _socket;
        private NetworkStream _stream;
        private BinaryWriter _writer;
        private BinaryReader _reader;
        private BinaryFormatter _binaryFormatter;

        //thread management
        private object _readLock;
        private object _writeLock;
        private object _encryptLock;
        private object _decryptLock;

        //client data
        public ClientData clientData;

        //encryption
        Encoding UTF8 = Encoding.UTF8;
        private RSACryptoServiceProvider _RSAProvider;
        public RSAParameters _publicKey;
        public RSAParameters _clientKey;
        private RSAParameters _privateKey;
        public bool successfulLogin;

        public Client(Socket socket, string nickname)
        {
            _socket = socket;
            _stream = new NetworkStream(socket);
            _writer = new BinaryWriter(_stream, Encoding.UTF8);
            _reader = new BinaryReader(_stream, Encoding.UTF8);
            _binaryFormatter = new BinaryFormatter();

            _readLock = new object();
            _writeLock = new object();
            _encryptLock = new object();
            _decryptLock = new object();

            clientData.clientNickname = nickname;

            _RSAProvider = new RSACryptoServiceProvider(2048);
            _publicKey = _RSAProvider.ExportParameters(false);
            _privateKey = _RSAProvider.ExportParameters(true);
            successfulLogin = false;
        }

        public void Close()
        {
            //close all connections
            _stream.Close();
            _reader.Close();
            _writer.Close();
        }

        public Packet TCPRead()
        {
            lock (_readLock)
            {
                try
                {
                    int numberOfBytes;
                    if ((numberOfBytes = _reader.ReadInt32()) != -1)
                    {
                        byte[] buffer = _reader.ReadBytes(numberOfBytes);

                        MemoryStream memoryStream = new MemoryStream(buffer);
                        Packet packet = _binaryFormatter.Deserialize(memoryStream) as Packet;

                        //decrypt relevant packets
                        switch (packet.EPacketType)
                        {
                            case PacketType.CHATMESSAGE:
                                ChatMessagePacket chatMessagePacket = (ChatMessagePacket)packet;
                                return new ChatMessagePacket(Decrypt(chatMessagePacket.OriginClient), Decrypt(chatMessagePacket.Message));
                            case PacketType.COMMANDMESSAGE:
                                CommandMessagePacket commandMessagePacket = (CommandMessagePacket)packet;
                                return new CommandMessagePacket(Decrypt(commandMessagePacket.Message));
                            case PacketType.CLIENTNAME:
                                ClientNamePacket clientNamePacket = (ClientNamePacket)packet;
                                return new ClientNamePacket(Decrypt(clientNamePacket.ClientName));
                        }

                        return packet;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("[Error] " + e.Message + e.StackTrace);
                    return null;
                }
            }
        }

        public void TCPSend(Packet packet)
        {
            lock (_writeLock)
            {
                try
                {
                    //encrypt relevant packets
                    switch (packet.EPacketType)
                    {
                        case PacketType.CHATMESSAGE:
                            ChatMessagePacket chatMessagePacket = (ChatMessagePacket)packet;
                            Serialize(new ChatMessagePacket(Encrypt(chatMessagePacket.OriginClient), Encrypt(chatMessagePacket.Message)));
                            break;
                        case PacketType.SERVERMESSAGE:
                            ServerMessagePacket serverMessagePacket = (ServerMessagePacket)packet;
                            Serialize(new ServerMessagePacket(Encrypt(serverMessagePacket.Message)));
                            break;
                        case PacketType.PRIVATEMESSAGE:
                            PrivateMessagePacket privateMessagePacket = (PrivateMessagePacket)packet;
                            Serialize(new PrivateMessagePacket(Encrypt(privateMessagePacket.OriginClient), Encrypt(privateMessagePacket.PrivateMessage)));
                            break;
                        case PacketType.ANNOUNCEMESSAGE:
                            AnnouncementMessagePacket announcementMessagePacket = (AnnouncementMessagePacket)packet;
                            Serialize(new AnnouncementMessagePacket(Encrypt(announcementMessagePacket.Message)));
                            break;
                        default:
                            Serialize(packet);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("[Error] " + e.Message + e.StackTrace);
                }
            }
        }

        private void Serialize(Packet packet)
        {
            MemoryStream memoryStream = new MemoryStream();
            _binaryFormatter.Serialize(memoryStream, packet);
            byte[] buffer = memoryStream.GetBuffer();
            _writer.Write(buffer.Length);
            _writer.Write(buffer);
            _writer.Flush();
        }

        private byte[] Encrypt(byte[] data)
        {
            lock(_encryptLock)
            {
                try
                {
                    //encrypt data using the client public key
                    _RSAProvider.ImportParameters(_clientKey);
                    return _RSAProvider.Encrypt(data, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine("[Error] " + e.Message + e.StackTrace);
                    return null;
                }
            }
        }

        public byte[] Decrypt(byte[] data)
        {
            lock (_decryptLock)
            {
                try
                {
                    //decrypt data using the client private key
                    _RSAProvider.ImportParameters(_privateKey);
                    return _RSAProvider.Decrypt(data, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine("[Error] " + e.Message + e.StackTrace);
                    return null;
                }
            }
        }

        public byte[] EncryptString(string message)
        {
            byte[] buffer = UTF8.GetBytes(message);

            if(buffer.Length > 0)
            {
                return Encrypt(buffer);
            }

            return null;
        }

        public string DecryptString(byte[] message)
        {
            byte[] buffer = Decrypt(message);

            if(buffer.Length > 0)
            {
                return UTF8.GetString(buffer);
            }

            return null;
        }

        public byte[] EncryptInt(int integer)
        {
            byte[] buffer = BitConverter.GetBytes(integer);

            if (buffer.Length > 0)
            {
                return Encrypt(buffer);
            }

            return null;
        }

        public int DecryptInt(byte[] integer)
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
