using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace SecQNet.SecQNetPackets
{
    [Serializable]
    public class AcknowledgePacket : SecQNetPacket
    {
        public override PacketSpecifier packetSpecifier
        { get { return PacketSpecifier.Acknowlege; } }

        public int Res { get; set; } = 1;

        public AcknowledgePacket()
        {

        }

        public AcknowledgePacket(byte[] packetBytes)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(packetBytes);

            AcknowledgePacket p = (AcknowledgePacket)bf.Deserialize(ms);
            ms.Close();

            this.Res = p.Res;
        }

        public override byte[] ToBytes()
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();

            bf.Serialize(ms, this);
            byte[] bytes = ms.ToArray();
            ms.Close();
            return bytes;
        }
    }
}
