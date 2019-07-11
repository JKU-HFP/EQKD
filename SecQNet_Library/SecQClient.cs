using SecQNet.SecQNetPackets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.TimeTagger;

namespace SecQNet
{
    public class SecQClient : SecQNetEndPoint
    {
     
        //Private variables
        private ConnectionStatus _connStatus = ConnectionStatus.NotConnected;
        private IPAddress _serverIP;

        //Properties
        public IPAddress ServerIPAdress
        {
            get { return _serverIP; }
        }

        public ConnectionStatus connectionStatus
        {
            get { return _connStatus; }
        }
        
        //Events
        public event EventHandler<ClientConnStatChangedEventArgs> ConnectionStatusChanged;

        private void OnConnectionStatusChanged(ConnectionStatus conn_stat, string error_msg = "")
        {
            _connStatus = conn_stat;

            ClientConnStatChangedEventArgs e = new ClientConnStatChangedEventArgs();
            e.client = _client;
            e.connectionStatus = conn_stat;
            e.error_msg = error_msg;

            ConnectionStatusChanged(this, e);
        }

        //Enumerators
        public enum ConnectionStatus
        {
            NotConnected,
            Connecting,
            ConnectedToServer,
            ConnectionFailed,
            ConnectionClosed
        }

        public SecQClient(Action<string> loggercallback) : base(loggercallback)
        {
           
        }
        
        public async Task ConnectAsync(IPAddress ip, int socket)
        {
            OnConnectionStatusChanged(ConnectionStatus.Connecting);
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ip, socket);
            }
            catch(SocketException e)
            {
                OnConnectionStatusChanged(ConnectionStatus.ConnectionFailed, e.Message);
                return;
            }
       
            _serverIP = ip;
            _client.Client.ReceiveBufferSize = _tcpBufferSize;
            _client.Client.SendBufferSize = _tcpBufferSize;
            _nws = _client.GetStream();
            OnConnectionStatusChanged(ConnectionStatus.ConnectedToServer);
        } 

        public void Disconnect()
        {
            _nws.Close();
            _client.Close();
            OnConnectionStatusChanged(ConnectionStatus.ConnectionClosed);
        }
     
        #region Communication 
        public CommandPacket ReceiveCommand()
        {
            CommandPacket comm = null;
     
            byte[] command_buffer;
            byte flags;
            SecQNetPacket.PacketSpecifier spec = ReceivePacket(out flags, out command_buffer, 0);

            if (spec == SecQNetPacket.PacketSpecifier.Command) comm = new CommandPacket(command_buffer);
            else return null;
      
            return comm;
        }

        public void SendTimeTags(TimeTags tt, ITimeTagger tagger, bool compress)
        {
            SendPacket(new TimeTagPacket(tt, tagger.BufferFillStatus, tagger.BufferSize)
            { flags = compress ? SecQNetPacket.FLAG_COMPRESS : (byte)0 });
        }

        public void SendAcknowledge()
        {
            SendPacket(new AcknowledgePacket());
        }

        #endregion

        protected override void WriteLog(string message)
        {
            _loggerCallback?.Invoke("EQKD Server: " + message);
        }

    }

    public class ClientConnStatChangedEventArgs : EventArgs
    {
        public TcpClient client;
        public SecQClient.ConnectionStatus connectionStatus;
        public string error_msg;

        public ClientConnStatChangedEventArgs() { }

    }
}
