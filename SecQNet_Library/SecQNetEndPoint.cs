using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using TimeTagger_Library;
using SecQNet.SecQNetPackets;
using TimeTagger_Library.TimeTagger;

namespace SecQNet
{
    public abstract class SecQNetEndPoint
    {
        //Private fields
        private object _sendLock = new object();
        private object _receiveLock = new object();

        //Protected fields
        protected int _receive_timeout = 10000;
        protected int _send_timeout = 10000;
        protected int _tcpBufferSize = 65536;

        protected TcpClient _client;
        protected NetworkStream _nws;
        protected Action<string> _loggerCallback;

        //Properties
        public TcpClient tcpClient
        {
            get { return _client; }
        }
        
        //Contructor
        protected SecQNetEndPoint(Action<string> loggercallback)
        {
            _client = null;
            _loggerCallback = loggercallback;
        }
        
        // Network sequence:
        // Flags (1 byte) -- SIZE OF PACKET SPECIFIER (32 Bit) -- Packet Specifier -- SIZE OF PACKET (32 bit) -- Packet

        protected void SendPacket(SecQNetPacket packet)
        {
            lock(_sendLock)
            {           
                _client.SendTimeout = _send_timeout;

                //Flags
                _nws.Write(new byte[] { packet.flags }, 0, 1);

                //PACKET SPECIFIER

                SecQNetPacket.PacketSpecifier spec = packet.packetSpecifier;
                byte[] spec_bytes = BitConverter.GetBytes((int)spec);
                Int32 spec_length = spec_bytes.Length;
                byte[] spec_length_bytes = BitConverter.GetBytes(spec_length);

                //Write packet specifier length
                _nws.Write(spec_length_bytes, 0, spec_length_bytes.Length);
                //Write packet specifier
                _nws.Write(spec_bytes, 0, spec_length);

                //PACKET

                byte[] packet_bytes = packet.ToBytes();
                Int32 packet_length = packet_bytes.Length;
                byte[] packet_length_bytes = BitConverter.GetBytes(packet_length);

                //Write packet size
                _nws.Write(packet_length_bytes, 0, packet_length_bytes.Length);
                //Write packet
                _nws.Write(packet_bytes, 0, packet_length);
            }
        }

        public void SendTimeTags(TimeTags tt, int bufferfillstatus=0, int buffersize=0, bool compress = false)
        {
            SendPacket(new TimeTagPacket(tt, bufferfillstatus, buffersize)
            { flags = compress ? SecQNetPacket.FLAG_COMPRESS : (byte)0 });
        }

        protected SecQNetPacket.PacketSpecifier ReceivePacket(out byte flags, out byte[] received_bytes, int init_read_timeout)
        {
            lock (_receiveLock)
            {
                //Receive flags
                _client.ReceiveTimeout = init_read_timeout;
                byte[] flags_byte = new byte[1];
                int flags_num_cyc = nwRead(out flags_byte, 1);
                flags = flags_byte[0];

                //Receive Packet Specifier Length
                _client.ReceiveTimeout = _receive_timeout;
                byte[] spec_length_bytes = new byte[sizeof(Int32)];
                int spec_length_num_cyc = nwRead(out spec_length_bytes, spec_length_bytes.Length);

                Int32 spec_length = BitConverter.ToInt32(spec_length_bytes, 0);

                //Receive Packet specifier
                _client.ReceiveTimeout = _receive_timeout;
                byte[] spec_bytes = new byte[spec_length];
                int spec_num_cyc = nwRead(out spec_bytes, spec_bytes.Length);

                SecQNetPacket.PacketSpecifier spec = (SecQNetPacket.PacketSpecifier)BitConverter.ToInt32(spec_bytes, 0);

                //Receive Packet Length
                byte[] packet_length_bytes = new byte[sizeof(Int32)];
                int packet_length_num_cyc = nwRead(out packet_length_bytes, packet_length_bytes.Length);

                Int32 packet_length = BitConverter.ToInt32(packet_length_bytes, 0);

                //Receive Packet
                byte[] packet_bytes = new byte[packet_length];
                int packet_num_cyc = nwRead(out packet_bytes, packet_bytes.Length);

                received_bytes = packet_bytes;

                return spec;
            } 
        }

        private int nwRead(out byte[] read_bytes, int buffer_length)
        {
            int read_cycles = 0;
            int num_bytes_received = 0;
            byte[] buffer = new byte[buffer_length];

            do
            {
               num_bytes_received += _nws.Read(buffer, num_bytes_received, buffer_length - num_bytes_received);
               read_cycles++;

            } while (num_bytes_received < buffer_length);

            read_bytes = buffer;
            return read_cycles;
        }

        protected abstract void WriteLog(string message);
    }

}
