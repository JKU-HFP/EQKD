using Extensions_Library;
using SecQNet;
using SecQNet.SecQNetPackets;
using System;
using System.Collections.Generic;
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
        private List<byte> _secureKeys = new List<byte>();
        private List<byte> _bobKeys = new List<byte>();

        //Properties
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
                RefChan = 1
            };
            TimeTagger.Connect(new List<long> { 0, 0, -2388, -2388, -6016, -256, -1152, 2176, 0, 0, 0, 0, 0, 0, 0, 0 });

            //List<int> countrate = TimeTagger.GetCountrate();
            //for (int i = 0; i < 8; i++) WriteLog($"Chan {i + 1}: {countrate[i]}");

            secQNetClient = new SecQClient(_loggerCallback);
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
                                //Obscure basis if requested
                                if(_obscureBasis)
                                {
                                    byte act_chan = 0;
                                    for(int i=0; i<send_tt.chan.Length; i++)
                                    {
                                        act_chan = send_tt.chan[i];
                                        send_tt.chan[i] = act_chan == 5 || act_chan == 7 ? TimeTagPacket.RectBasisCodedChan : TimeTagPacket.DiagbasisCodedChan;
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
                            break;

                        case CommandPacket.SecQNetCommands.ObscureBasisON:
                            _obscureBasis = true;
                            break;

                        case CommandPacket.SecQNetCommands.ReceiveSiftedTags:

                            receive_tt = secQNetClient.ReceiveSiftedTimeTags();

                            if (receive_tt == null) break;

                            List<int> key_indices = send_tt.time.GetIndicesOf(receive_tt.time).ToList();
                            
                            key_indices.ForEach( (i) =>
                            {
                                byte act_chan = send_tt.chan[i];
                                _secureKeys.Add(act_chan == 5 || act_chan == 7 ? (byte)0 : (byte)1);
                            });                           
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
