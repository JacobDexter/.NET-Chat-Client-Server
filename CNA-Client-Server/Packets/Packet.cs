using System;
using System.Net;
using System.Security.Cryptography;

namespace Packets
{
    public enum PacketType
    {
        EMPTY,
        CHATMESSAGE,
        PRIVATEMESSAGE,
        ANNOUNCEMESSAGE,
        SERVERMESSAGE,
        COMMANDMESSAGE,
        CLIENTNAME,
        DISCONNECTREQUEST,
        DISCONNECTACCEPTED,
        LOGIN,
        SERVERKEY,
        CLIENTLIST
    }

    [Serializable]
    public abstract class Packet
    {
        public PacketType EPacketType { get; protected set; }

        public string GetCurrentTime()
        {
            //return DateTime.Now.ToShortTimeString(); //24 hour clock
            return DateTime.Now.ToString("h:mm:ss");
        }
    }

    [Serializable]
    public class EmptyPacket : Packet
    {
        public string Time { get; private set; }
        public EmptyPacket()
        {
            EPacketType = PacketType.EMPTY;
            Time = GetCurrentTime();
        }
    }

    //////////////////
    ///Message data///
    //////////////////

    [Serializable]
    public class ChatMessagePacket : Packet
    {
        public string Time { get; set; }
        public byte[] OriginClient { get; set; }
        public byte[] Message { get; private set; }

        public ChatMessagePacket(byte[] sentFrom, byte[] message)
        {
            EPacketType = PacketType.CHATMESSAGE;
            OriginClient = sentFrom;
            Message = message;
            Time = GetCurrentTime();
        }
    }

    [Serializable]
    public class PrivateMessagePacket : Packet
    {
        public string Time { get; private set; }
        public byte[] OriginClient { get; private set; }
        public byte[] PrivateMessage { get; private set; }

        public PrivateMessagePacket(byte[] sentFrom, byte[] pMessage)
        {
            EPacketType = PacketType.PRIVATEMESSAGE;
            OriginClient = sentFrom;
            PrivateMessage = pMessage;
            Time = GetCurrentTime();
        }
    }

    [Serializable]
    public class ServerMessagePacket : Packet
    {
        public string Time { get; private set; }
        public byte[] Message { get; private set; }

        public ServerMessagePacket(byte[] message)
        {
            EPacketType = PacketType.SERVERMESSAGE;
            Message = message;
            Time = GetCurrentTime();
        }
    }

    [Serializable]
    public class CommandMessagePacket : Packet
    {
        public byte[] Message { get; private set; }

        public CommandMessagePacket(byte[] message)
        {
            EPacketType = PacketType.COMMANDMESSAGE;
            Message = message;
        }
    }

    [Serializable]
    public class AnnouncementMessagePacket : Packet
    {
        public string Time { get; private set; }
        public byte[] Message { get; private set; }
        public AnnouncementMessagePacket(byte[] announcement)
        {
            EPacketType = PacketType.ANNOUNCEMESSAGE;
            Message = announcement;
            Time = GetCurrentTime();
        }
    }

    ///////////////////
    ////Client Data////
    ///////////////////

    [Serializable]
    public class ClientNamePacket : Packet
    {
        public byte[] ClientName { get; private set; }

        public ClientNamePacket(byte[] name)
        {
            EPacketType = PacketType.CLIENTNAME;
            ClientName = name;
        }
    }

    [Serializable]
    public class DisconnectRequestPacket : Packet
    {
        public string Time { get; private set; }

        public DisconnectRequestPacket()
        {
            EPacketType = PacketType.DISCONNECTREQUEST;
            Time = GetCurrentTime();
        }
    }

    //Encryption

    [Serializable]
    public class LoginPacket : Packet
    {
        public IPEndPoint EndPoint { get; private set; }
        public RSAParameters PublicKey { get; private set; }

        public LoginPacket(IPEndPoint IPEndPoint, RSAParameters publicKey)
        {
            EPacketType = PacketType.LOGIN;
            EndPoint = IPEndPoint;
            PublicKey = publicKey;
        }
    }

    ///////////////////
    ////Server Data////
    ///////////////////

    [Serializable]
    public class ServerKeyPacket : Packet
    {
        public RSAParameters ServerKey { get; private set; }
        public bool Successful { get; private set; }

        public ServerKeyPacket(RSAParameters serverKey, bool success)
        {
            EPacketType = PacketType.SERVERKEY;
            ServerKey = serverKey;
            Successful = success;
        }
    }

    [Serializable]
    public class ClientListPacket : Packet
    {
        public string[] ClientList { get; private set; }
        public string[] ClientIPS { get; private set; }
        public ClientListPacket(string[] clients, string[] ips)
        {
            EPacketType = PacketType.CLIENTLIST;
            ClientList = clients;
            ClientIPS = ips;
        }
    }
}
