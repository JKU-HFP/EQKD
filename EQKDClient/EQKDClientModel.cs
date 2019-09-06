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

        //Properties
        public SecQClient secQNetClient { get; private set; }

        public ITimeTagger TimeTagger { get; private set; }

        public bool CompressTimeTags { get; set; }

        public EQKDClientModel(Action<string> loggercallback)
        {
            _loggerCallback = loggercallback;
            //TimeTaggerFactory timeTaggerFactory = new TimeTaggerFactory("ClientTagger",WriteLog);
            //TimeTagger = timeTaggerFactory.GetDefaultTimeTagger();
            TimeTagger = new SITimeTagger(_loggerCallback);
            TimeTagger.Connect(new List<long> { 0, 0, -2388, -2388, -6016, -256, -1152, 2176, 0, 0, 0, 0, 0, 0, 0, 0 });

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
                while (true)
                {
                    if (_listening_ct.IsCancellationRequested) return;

                    CommandPacket commandPacket = secQNetClient.ReceiveCommand();

                    switch (commandPacket.Command)
                    {
                        case CommandPacket.SecQNetCommands.SendTimeTags:
                            if (TimeTagger.GetNextTimeTags(out TimeTags tt))
                            {
                                secQNetClient.SendTimeTags(tt, TimeTagger, CompressTimeTags);
                            }
                            else
                            {
                                //Send acknowledge if no timetags available
                                secQNetClient.SendAcknowledge();
                            }
                            break;

                        case CommandPacket.SecQNetCommands.SendTimeTagsSecure:
                            break;

                        case CommandPacket.SecQNetCommands.ClearPhotonBuffer:
                            TimeTagger.ClearTimeTagBuffer();
                            secQNetClient.SendAcknowledge();
                            break;

                        case CommandPacket.SecQNetCommands.StartCollecting:
                            TimeTagger.PacketSize = commandPacket.val0;
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
