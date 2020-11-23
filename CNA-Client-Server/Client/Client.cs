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
using Packets;

namespace Client
{
    public class Client
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private BinaryWriter _writer;
        private BinaryReader _reader;
        private BinaryFormatter _binaryFormatter;

        //UI
        ClientForm _clientForm;

        public Client()
        {
            _tcpClient = new TcpClient();
        }

        public bool Connect(string ipAddress, int port)
        {
            try
            {
                _tcpClient.Connect(ipAddress, port);
                _stream = _tcpClient.GetStream();
                _writer = new BinaryWriter(_stream, Encoding.UTF8);
                _reader = new BinaryReader(_stream, Encoding.UTF8);
                _binaryFormatter = new BinaryFormatter();

                return true;
            }
            catch(Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
                return false;
            }
        }

        public void Disconnect()
        {
            if(_tcpClient.Connected)
            {
                try
                {
                    //send disconnect request packet
                    DisconnectRequestPacket disconnectRequestPacket = new DisconnectRequestPacket(null);//change
                    Serialize(disconnectRequestPacket);
                    _tcpClient.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine("[Error] " + e.Message);
                }
            }
        }

        public void Run()
        {
            _clientForm = new ClientForm(this);

            //start thread for UI
            Thread thread = new Thread(() => { ProcessServerResponse(); });
            thread.Start();

            _clientForm.ShowDialog(); //show window

            //_tcpClient.Close();
        }

        private void ProcessServerResponse()
        {
            Packet receivedPacket;

            while ((receivedPacket = Read()) != null)
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
                            Console.WriteLine(chatMessagePacket.Time + " " + chatMessagePacket.OriginClient + ": " + chatMessagePacket.Message);

                            //Write message to client form
                            _clientForm.UpdateChatWindow(chatMessagePacket.Time + " " + chatMessagePacket.OriginClient + ": " + chatMessagePacket.Message);
                            break;
                        case PacketType.PRIVATEMESSAGE:
                            PrivateMessagePacket privateMessagePacket = (PrivateMessagePacket)receivedPacket;
                            break;
                        case PacketType.SERVERMESSAGE:
                            ServerMessagePacket serverMessagePacket = (ServerMessagePacket)receivedPacket;

                            //Write server response to client console
                            Console.WriteLine(serverMessagePacket.Time + " " + serverMessagePacket.Prefix + serverMessagePacket.Message);

                            //Write server response to client form
                            _clientForm.UpdateChatWindow(serverMessagePacket.Time + " " + serverMessagePacket.Prefix + serverMessagePacket.Message);
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
        }

        public void SendMessage(string message)
        {
            if(_tcpClient.Connected)
            {
                //checks if message is a command
                if(!message.StartsWith("/"))
                {
                    //create message packet
                    ChatMessagePacket packet = new ChatMessagePacket(null, message);

                    //serialize and send packet to all clients
                    Serialize(packet);
                }
                else
                {
                    //creates command message packet
                    CommandMessagePacket packet = new CommandMessagePacket(message);

                    //serialize and send packet to all clients
                    Serialize(packet);
                }
            }
        }

        public Packet Read()
        {
            try
            {
                int numberOfBytes;
                if ((numberOfBytes = _reader.ReadInt32()) != -1)
                {
                    byte[] buffer = _reader.ReadBytes(numberOfBytes);
                    MemoryStream memoryStream = new MemoryStream(buffer);
                    return _binaryFormatter.Deserialize(memoryStream) as Packet;
                }
                else
                {
                    return null;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("[Error] " + e.Message);
                return null;
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
    }
}
