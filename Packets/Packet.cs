using System;

namespace Packets
{
    public enum PacketType
    {
        EMPTY,
        CHATMESSAGE,
        PRIVATEMESSAGE,
        CLIENTNAME
    }

    [Serializable]
    public abstract class Packet
    {
        public PacketType EPacketType { get; protected set; }
    }

    [Serializable]
    public class EmptyPacket : Packet
    {
        public EmptyPacket()
        {
            EPacketType = PacketType.EMPTY;
        }
    }

    //////////////////
    ///Message data///
    //////////////////

    [Serializable]
    public class ChatMessagePacket : Packet
    {
        public string OriginClient { get; private set; }
        public string Message { get; private set; }

        public ChatMessagePacket(string sentFrom, string message)
        {
            EPacketType = PacketType.CHATMESSAGE;
            OriginClient = sentFrom;
            Message = message;
        }
    }

    [Serializable]
    public class PrivateMessagePacket : Packet
    {
        public string OriginClient { get; private set; }
        public string PrivateMessage { get; private set; }

        public PrivateMessagePacket(string sentFrom, string pMessage)
        {
            EPacketType = PacketType.PRIVATEMESSAGE;
            OriginClient = sentFrom;
            PrivateMessage = pMessage;
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
}
