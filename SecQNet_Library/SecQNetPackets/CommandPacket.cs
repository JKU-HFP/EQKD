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
    public class CommandPacket : SecQNetPacket
    {
        public override PacketSpecifier packetSpecifier
        { get { return PacketSpecifier.Command; } }

        //Command List
        public enum SecQNetCommands
        {
            SendTimeTags,
            SendTimeTagsSecure,
            ClearPhotonBuffer,
            StartCollecting,
            StopCollecting
        };

        private SecQNetCommands _command;

        //Optional values
        //          |   StartCollecting   |    
        //---------------------------------
        //  val0    |     Packet Size

        public int val0 = 0;

        public SecQNetCommands Command { get { return _command; } }
                 
        public CommandPacket(SecQNetCommands comm) : base()
        {
            _command = comm;
        }

        public CommandPacket(byte[] packetbytes)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(packetbytes);

            CommandPacket p = (CommandPacket)bf.Deserialize(ms);
            ms.Close();

            this._command = p.Command;
            this.val0 = p.val0;
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
