using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecQNet.SecQNetPackets
{
    [Serializable]
    abstract public class SecQNetPacket
    {
        public static readonly byte FLAG_COMPRESS = 0b100;

        public byte flags = 0b00000000;

        public enum PacketSpecifier
        {
            Command,
            TimeTags,
            Acknowlege
        }

        protected SecQNetPacket() { }

        abstract public PacketSpecifier packetSpecifier { get; }

        abstract public byte[] ToBytes();
    }
}
