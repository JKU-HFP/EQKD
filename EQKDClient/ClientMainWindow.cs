using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.IO;
using SecQNet;
using System.Collections.Concurrent;
using System.Threading;
using TimeTagger_Library;
using TimeTagger_Library.TimeTagger;
using AsyncAwaitBestPractices;

namespace EQKDClient
{
    public partial class ClientMainWindow : Form
    {
      
        EQKDClientModel _EQKDClient;

        public ClientMainWindow()
        {
            InitializeComponent();
        }

        private void ClientMainWindow_Load(object sender, EventArgs e)
        {
            _EQKDClient = new EQKDClientModel(WriteLog);
            _EQKDClient.secQNetClient.ConnectionStatusChanged += ConnectionStatusChangedHandler;
        }

        private void btn_OpenTestFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog of = new OpenFileDialog();
            if (of.ShowDialog() == DialogResult.OK) textBox_TestFile.Text = of.FileName;
        }
         
     
        private void btn_ConnectToServer_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(textBox_ServerPort.Text, out int server_socket)) return;
            if (!IPAddress.TryParse(textBox_ServerIP.Text, out IPAddress Ip)) return;

            _EQKDClient.CompressTimeTags = checkBox_Compress.Checked;
            //secQClient.TimeTagger.TimeTagsCollected += (s,ea) => syncContext.Post(o => TimeTagsCollected(s,ea), null);
            //_EQKDClient.TimeTagger.TimeTagsCollected += TimeTagsCollected;

            _EQKDClient.secQNetClient.ConnectAsync(Ip, server_socket);
        }

        private void TimeTagsCollected(object sender, TimeTagsCollectedEventArgs e)
        {
            textBox_CountrateTest.Text = e.Countrate[0].ToString();
        }

        private void ConnectionStatusChangedHandler(object sender, ClientConnStatChangedEventArgs e)
        {
            SecQClient.ConnectionStatus conn = e.connectionStatus;

            switch(conn)
            {
                case SecQClient.ConnectionStatus.Connecting:
                    WriteLog("Connecting to server");
                    break;
                case SecQClient.ConnectionStatus.ConnectedToServer:
                    WriteLog("Connected to server " + _EQKDClient.secQNetClient.ServerIPAdress.ToString());
                    if(_EQKDClient.TimeTagger is SimulatedTagger)
                    {
                        ((SimulatedTagger)_EQKDClient.TimeTagger).FileName = textBox_TestFile.Text;
                    }
                    _EQKDClient.StartListeningAsync().SafeFireAndForget();
                    break;
                case SecQClient.ConnectionStatus.ConnectionFailed:
                    WriteLog("Connection failed:" + e.error_msg);
                    break;
                case SecQClient.ConnectionStatus.ConnectionClosed:
                    WriteLog("Connection lost");
                    break;
                default:
                    break;
            }
        }
                   
        public void WriteLog(string entry)
        {
            Invoke((MethodInvoker)(() => textBox_Log.AppendText("\n" + DateTime.Now + ": " + entry + "\r\n")));
        }

        private void btn_StartCollecting_Click(object sender, EventArgs e)
        {
            
        }


    }
}
