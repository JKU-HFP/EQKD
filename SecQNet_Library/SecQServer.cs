using Extensions_Library;
using SecQNet.SecQNetPackets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TimeTagger_Library;

namespace SecQNet
{
    public class SecQNetServer : SecQNetEndPoint, ITaggerNetwork
    {
        //-----------------------------------
        //----  P R I V A T E  F I E L D S
        //-----------------------------------

        //TCP Connection
        private TcpListener _server;
        private IPAddress _serverIP;
        private int _port;
        private ConnectionStatus _connStatus = ConnectionStatus.NotConnected;

        //Properties
        public bool IsConnected { get => connectionStatus == ConnectionStatus.ClientConnected; }
        public ConnectionStatus connectionStatus
        {
            get { return _connStatus; }
        }
        public IPAddress ServerIP
        {
            get { return _serverIP; }
        }
        public int Port
        {
            get { return _port; }
        }

        private bool _obscureClientTimeTags;

        public bool ObscureClientTimeTags
        {
            get { return _obscureClientTimeTags; }
            set
            {
                if (!(connectionStatus == ConnectionStatus.ClientConnected)) return;

                if (value == false) SendPacket(new CommandPacket(CommandPacket.SecQNetCommands.ObscureBasisOFF));
                else SendPacket(new CommandPacket(CommandPacket.SecQNetCommands.ObscureBasisON));

                _obscureClientTimeTags = value;
            }
        }



        //Events
        public event EventHandler<ServerConnStatChangedEventArgs> ConnectionStatusChanged;

        private void OnConnectionStatusChanged(ConnectionStatus conn_stat)
        {
            string clientIP = "";
            _connStatus = conn_stat;
            if (conn_stat != ConnectionStatus.ClientDisconnected)
            {
                clientIP = _client == null ? "" : (_client.Client == null ? "" : ((IPEndPoint)_client.Client.LocalEndPoint).Address.ToString());
            }
            ConnectionStatusChanged.Raise(this, new ServerConnStatChangedEventArgs() { ClientIPAddress = clientIP, connectionStatus = _connStatus });
        }

        public event EventHandler<TimeTagsReceivedEventArgs> TimeTagsReceived;

        private void OnTimeTagsReceived(TimeTagsReceivedEventArgs e)
        {
            TimeTagsReceived?.Raise(this, e);
        }


        //Enumerators
        public enum ConnectionStatus
        {
            NotConnected,
            Listening,
            ClientConnected,
            ClientDisconnected
        }


        //Constructor
        public SecQNetServer(Action<string> loggercallback) : base(loggercallback)
        {

        }

        public static IPAddress GetIP4Address()
        {
            IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName());

            return ips.Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).FirstOrDefault();
        }

        public async Task ConnectAsync(IPAddress ip, int port)
        {
            _serverIP = ip;
            _port = port;
            _server = new TcpListener(ip, port);

            //Await client connection
            _server.Start();
            OnConnectionStatusChanged(ConnectionStatus.Listening);
            _client = await _server.AcceptTcpClientAsync();
            _server.Stop();

            _client.Client.ReceiveBufferSize = _tcpBufferSize;
            _client.Client.SendBufferSize = _tcpBufferSize;
            _nws = _client.GetStream();

            OnConnectionStatusChanged(ConnectionStatus.ClientConnected);
        }

        public void Disconnect()
        {
            _nws.Close();
            _client.Close();
            OnConnectionStatusChanged(ConnectionStatus.ClientDisconnected);
        }

        #region Communication

        public bool RequestTimeTags(out TimeTags tt)
        {
            TimeTagPacket tt_packet = null;
            tt = null;

            try
            {
                //Request Timetags
                SendPacket(new CommandPacket(CommandPacket.SecQNetCommands.SendTimeTags));

                //Wait for timetags
                byte[] packet_buffer;
                byte flags;
                SecQNetPacket.PacketSpecifier packet_spec = ReceivePacket(out flags, out packet_buffer, _receive_timeout);

                if (packet_spec == SecQNetPacket.PacketSpecifier.TimeTags)
                {
                    tt_packet = new TimeTagPacket(flags, packet_buffer);
                    tt = tt_packet.timetags;
                    OnTimeTagsReceived(new TimeTagsReceivedEventArgs(tt_packet.timetags.Countrate, tt_packet.BufferStatus, tt_packet.BufferSize, tt.time.Length));
                    return true;
                }
            }
            //Read Timeout: IOException
            catch (IOException ex)
            {
                Disconnect();
                WriteLog("TCP Read error. Disconnecting from client.\n" + ex.Message);
            }

            return false;
        }

        public bool SendSiftedTimeTags(TimeTags tt)
        {
            try
            {
                //Request Timetags
                SendPacket(new CommandPacket(CommandPacket.SecQNetCommands.ReceiveSiftedTags));

                SendTimeTags(tt);
            }
            //Read Timeout: IOException
            catch (IOException ex)
            {
                Disconnect();
                WriteLog("TCP Read error. Disconnecting from client.\n" + ex.Message);
            }

            return false;
        }

        public bool RequestClearBuffer()
        {
            try
            {
                //Send request
                SendPacket(new CommandPacket(CommandPacket.SecQNetCommands.ClearPhotonBuffer));

                //Wait for acknowledge
                byte[] packet_buffer;
                byte flags;
                SecQNetPacket.PacketSpecifier packet_spec = ReceivePacket(out flags, out packet_buffer, _receive_timeout);

                if (packet_spec != SecQNetPacket.PacketSpecifier.Acknowlege) return false;

            }
            catch (Exception ex)
            {
                Disconnect();
                throw new Exception(ex.Message, ex.InnerException);
            }
            return true;
        }

        public bool RequestStartTimeTagger(int packetsize, double syncrate=0)
        {
            try
            {
                //Send request
                SendPacket(new CommandPacket(CommandPacket.SecQNetCommands.StartCollecting) { val0 = packetsize, val1=syncrate });

                //Wait for acknowledge
                byte[] packet_buffer;
                byte flags;
                SecQNetPacket.PacketSpecifier packet_spec = ReceivePacket(out flags, out packet_buffer, _receive_timeout);

                if (packet_spec != SecQNetPacket.PacketSpecifier.Acknowlege) return false;

            }
            catch (Exception ex)
            {
                Disconnect();
                throw new Exception(ex.Message, ex.InnerException);
            }
            return true;
        }

        public bool RequestStopTimeTagger()
        {
            try
            {
                //Send request
                SendPacket(new CommandPacket(CommandPacket.SecQNetCommands.StopCollecting));

                //Wait for acknowledge
                byte[] packet_buffer;
                byte flags;
                SecQNetPacket.PacketSpecifier packet_spec = ReceivePacket(out flags, out packet_buffer, _receive_timeout);

                if (packet_spec != SecQNetPacket.PacketSpecifier.Acknowlege) return false;

            }
            catch (Exception ex)
            {
                Disconnect();
                throw new Exception(ex.Message, ex.InnerException);
            }
            return true;
        }

        #endregion

        protected override void WriteLog(string message)
        {
            _loggerCallback?.Invoke("SecQNet Server: " + message);
        }
    }

    public class ServerConnStatChangedEventArgs : EventArgs
    {
        public string ClientIPAddress;
        public SecQNetServer.ConnectionStatus connectionStatus;

        public ServerConnStatChangedEventArgs() { }
    }

    public class TimeTagsReceivedEventArgs : EventArgs
    {
        public readonly List<int> Countrate;
        public readonly int BufferStatus;
        public readonly int BufferSize;
        public readonly int PacketSize;

        public TimeTagsReceivedEventArgs(List<int> countrate, int bufferstatus, int buffersize, int packetsize)
        {
            Countrate = countrate;
            BufferStatus = bufferstatus;
            BufferSize = buffersize;
            PacketSize = packetsize;
        }
    }



}
