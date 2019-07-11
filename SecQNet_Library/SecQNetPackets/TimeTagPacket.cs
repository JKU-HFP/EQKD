using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library;

namespace SecQNet.SecQNetPackets
{
    [Serializable]
    public class TimeTagPacket : SecQNetPacket
    {
        
        public override PacketSpecifier packetSpecifier
        { get { return PacketSpecifier.TimeTags; } }

        public TimeTags timetags;
        public int BufferStatus;
        public int BufferSize;

        public TimeTagPacket() : base()
        {

        }

        public TimeTagPacket(TimeTags tt, int bufferstatus, int buffersize) : base()
        {
            flags |= FLAG_COMPRESS; // Compress

            timetags = tt;
            BufferStatus = bufferstatus;
            BufferSize = buffersize;
        }

        public TimeTagPacket(byte inflags, byte[] packetBytes) : base()
        {      
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(packetBytes);
            TimeTagPacket p;

            if ((inflags & FLAG_COMPRESS) > 0)
            {
                using (GZipStream gzs = new GZipStream(ms, CompressionMode.Decompress))
                {
                    p = (TimeTagPacket)bf.Deserialize(gzs);
                }
            }
            else p = (TimeTagPacket)bf.Deserialize(ms);

            ms.Close();

            this.timetags = p.timetags;
            this.BufferStatus = p.BufferStatus;
            this.BufferSize = p.BufferSize;
            this.flags = p.flags;

        }

        public override byte[] ToBytes()
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
        
            byte[] buffer;

            if ((flags & FLAG_COMPRESS)>0)
            {
                using (GZipStream gzs = new GZipStream(ms, CompressionMode.Compress))
                {
                    bf.Serialize(gzs, this);
                }
            }
            else bf.Serialize(ms, this);
        
            buffer = ms.ToArray();
            ms.Close();
                      
            return buffer;
        }
    }
}
