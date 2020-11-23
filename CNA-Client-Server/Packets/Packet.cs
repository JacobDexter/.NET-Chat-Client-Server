using System;

namespace Packets
{
    public enum PacketType
    {
        EMPTY,
        CHATMESSAGE,
        PRIVATEMESSAGE,
        BROADCASTMESSAGE,
        SERVERMESSAGE,
        COMMANDMESSAGE,
        CLIENTNAME,
        DISCONNECTREQUEST
    }

    [Serializable]
    public abstract class Packet
    {
        public PacketType EPacketType { get; protected set; }

        public string GetCurrentTime()
        {
            //return DateTime.Now.ToShortTimeString(); //24 hour clock
            return DateTime.Now.ToString("h:mm:ss tt");
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
        public string Time { get; private set; }
        public string OriginClient { get; set; }
        public string Message { get; private set; }

        public ChatMessagePacket(string sentFrom, string message)
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
        public string OriginClient { get; private set; }
        public string PrivateMessage { get; private set; }

        public PrivateMessagePacket(string sentFrom, string pMessage)
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
        public string Prefix { get; private set; }
        public string Message { get; private set; }

        public ServerMessagePacket(string message)
        {
            EPacketType = PacketType.SERVERMESSAGE;
            Prefix = "[Server]: ";
            Message = message;
            Time = GetCurrentTime();
        }
    }

    [Serializable]
    public class CommandMessagePacket : Packet
    {
        public string Message { get; private set; }

        public CommandMessagePacket(string message)
        {
            EPacketType = PacketType.COMMANDMESSAGE;
            Message = message;
        }
    }

    ///////////////////
    ////Client Data////
    ///////////////////

    [Serializable]
    public class ClientNamePacket : Packet
    {
        public string ClientName { get; private set; }

        public ClientNamePacket(string name)
        {
            EPacketType = PacketType.CLIENTNAME;
            ClientName = name;
        }
    }

    [Serializable]
    public class DisconnectRequestPacket : Packet
    {
        public string Time { get; private set; }
        public string ClientName { get; private set; }

        public DisconnectRequestPacket(string name)
        {
            EPacketType = PacketType.DISCONNECTREQUEST;
            ClientName = name;
            Time = GetCurrentTime();
        }
    }
}
