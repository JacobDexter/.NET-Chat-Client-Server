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
        public string _clientNickname = "Default";

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

        public void Run()
        {
            _clientForm = new ClientForm(this);

            //start thread for UI
            Thread thread = new Thread(() => { ProcessServerResponse(); });
            thread.Start();

            _clientForm.ShowDialog(); //show window

            _tcpClient.Close();
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
                            ChatMessagePacket chatPacket = (ChatMessagePacket)receivedPacket;
                            Console.WriteLine("[Server] " + chatPacket.Message); //Write server response to client console
                            _clientForm.UpdateChatWindow("[Server] " + chatPacket.Message); //Write server response to client form
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
        }

        public void SendMessage(string message)
        {
            //create message packet
            ChatMessagePacket packet = new ChatMessagePacket(_clientNickname, message);

            //send nickname and message to client console
            Console.WriteLine(packet.OriginClient + ": " + packet.Message);

            //serialize and send packet
            MemoryStream memoryStream = new MemoryStream();
            _binaryFormatter.Serialize(memoryStream, packet);
            byte[] buffer = memoryStream.GetBuffer();
            _writer.Write(buffer.Length);
            _writer.Write(buffer);
            _writer.Flush();

            //send nickname and message to client chat UI
            _clientForm.UpdateChatWindow(packet.OriginClient + ": " + packet.Message);
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
    }
}
