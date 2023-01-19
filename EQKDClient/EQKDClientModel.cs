using Extensions_Library;
using QKD_Library;
using SecQNet;
using SecQNet.SecQNetPackets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.TimeTagger;

namespace EQKDClient
{
    public class EQKDClientModel
    {
        //Private fields       
        private Action<string> _loggerCallback;
        private CancellationTokenSource _listening_cts;

        private bool _obscureBasis = false;

        //Properties
        public string KeyFolder { get; set; } = "Key";

        public QKey SecureKey { get; private set; } = new QKey()
        {
            RectZeroChan = 5,
            DiagZeroChan = 7,
            FileName= "Key_Bob.txt"
        };
        public SecQClient secQNetClient { get; private set; }

        public ITimeTagger TimeTagger { get; private set; }

        public bool CompressTimeTags { get; set; }

        public EQKDClientModel(Action<string> loggercallback)
        {
            _loggerCallback = loggercallback;
            //TimeTaggerFactory timeTaggerFactory = new TimeTaggerFactory("ClientTagger",WriteLog);
            //TimeTagger = timeTaggerFactory.GetDefaultTimeTagger();
            TimeTagger = new SITimeTagger(_loggerCallback)
            {
                RefChan = 1,
                SyncDiscriminatorVoltage = 0.2,
                SyncRate = 10000000,
                RefChanDivider = 100,
                PackageMode = TimeTaggerBase.PMode.ByEllapsedTime
            };
            try {
                TimeTagger.Connect(new List<long> { 0, 0, -75648, -78208, 2176, 2176, 1164, 2176 });
            }
            catch { }
            //List<int> countrate = TimeTagger.GetCountrate();
            //for (int i = 0; i < 8; i++) WriteLog($"Chan {i + 1}: {countrate[i]}");

            secQNetClient = new SecQClient(_loggerCallback);

            //Create key folder
            if (!Directory.Exists(KeyFolder)) Directory.CreateDirectory(KeyFolder);
        }

        public async Task StartListeningAsync()
        {
            _listening_cts = new CancellationTokenSource();

            WriteLog("Starting listening to EQKD Server...");
            await Task.Run(() => DoListening(_listening_cts.Token));
            WriteLog("Stopped listening to EKQD Server");
        }

        private void DoListening(CancellationToken _listening_ct)
        {
            try
            {
                TimeTags send_tt = null;
                TimeTags orgin_send_tt = null;
                TimeTags receive_tt = null;
    

                while (true)
                {
                    if (_listening_ct.IsCancellationRequested) return;

                    CommandPacket commandPacket = secQNetClient.ReceiveCommand();

                    switch (commandPacket.Command)
                    {
                        case CommandPacket.SecQNetCommands.SendTimeTags:
                            if (TimeTagger.GetNextTimeTags(out send_tt))
                            {
                                orgin_send_tt = new TimeTags(send_tt.chan.Take(send_tt.chan.Length).ToArray(), send_tt.time.Take(send_tt.time.Length).ToArray());
                                //Obscure basis if requested
                                if(_obscureBasis)
                                {
                                    byte act_chan = 0;
                                    for(int i=0; i<send_tt.chan.Length; i++)
                                    {
                                        act_chan = send_tt.chan[i];
                                        send_tt.chan[i] = act_chan == 5 || act_chan == 6 ? TimeTagPacket.RectBasisCodedChan : TimeTagPacket.DiagbasisCodedChan;
                                    }
                                }

                                secQNetClient.SendTimeTags(send_tt, TimeTagger.BufferFillStatus, TimeTagger.BufferSize, CompressTimeTags);
                            }
                            else
                            {
                                //Send acknowledge if no timetags available
                                secQNetClient.SendAcknowledge();
                            }
                            break;

                        case CommandPacket.SecQNetCommands.ObscureBasisOFF:
                            _obscureBasis = false;
                            WriteLog("Stealth mode OFF");
                            break;

                        case CommandPacket.SecQNetCommands.ObscureBasisON:
                            _obscureBasis = true;
                            WriteLog("Stealth mode ON");
                            break;

                        case CommandPacket.SecQNetCommands.ReceiveSiftedTags:

                            receive_tt = secQNetClient.ReceiveSiftedTimeTags();
                            int currKeyNr = commandPacket.val0;

                            if (receive_tt == null) break;

                            List<int> key_indices = receive_tt.time.Select(t => (int)t).ToList();

                            SecureKey.FileName = Path.Combine(KeyFolder, $"Key_Bob_{currKeyNr:D4}.txt");
                            SecureKey.AddKey(orgin_send_tt, key_indices);    
                    
                            break;

                        case CommandPacket.SecQNetCommands.SendTimeTagsSecure:
                            break;

                        case CommandPacket.SecQNetCommands.ClearPhotonBuffer:
                            TimeTagger.ClearTimeTagBuffer();
                            secQNetClient.SendAcknowledge();
                            break;

                        case CommandPacket.SecQNetCommands.StartCollecting:
                            TimeTagger.PacketSize = commandPacket.val0;
                            TimeTagger.SyncRate = commandPacket.val1;
                            TimeTagger.PacketTimeSpan = commandPacket.val2;
                            TimeTagger.PackageMode = commandPacket.val3 == 1 ? TimeTaggerBase.PMode.ByEllapsedTime : TimeTaggerBase.PMode.ByPackageSize;
                            TimeTagger.StartCollectingTimeTagsAsync();
                            secQNetClient.SendAcknowledge();
                            break;

                        case CommandPacket.SecQNetCommands.StopCollecting:
                            TimeTagger.StopCollectingTimeTags();
                            secQNetClient.SendAcknowledge();
                            break;
                        default:

                            break;
                    }

                }
            }
            catch (Exception)
            {
                TimeTagger?.StopCollectingTimeTags();
                secQNetClient.Disconnect();
                WriteLog("Disconnected from Server");
            }
        }

        public void StopListening()
        {
            _listening_cts.Cancel();
        }

        private void WriteLog(string message)
        {
            _loggerCallback?.Invoke("EQKD Client: " + message);
        }

    }



}
