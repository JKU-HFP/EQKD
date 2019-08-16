using Extensions_Library;
using SecQNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;
using TimeTagger_Library.TimeTagger;
using TimeTaggerWPF_Library;
using Stage_Library;
using Stage_Library.Thorlabs;
using Stage_Library.NewPort;
using Entanglement_Library;
using System.IO;

namespace EQKDServer.Models
{
    public class EQKDServerModel
    {

        //-----------------------------------
        //----  P R I V A T E  F I E L D S
        //-----------------------------------

        private Action<string> _loggerCallback;
        
        //Synchronization
        private CancellationTokenSource _sync_cts;
        private SyncStatus _sync_status;
        Kurolator testcorrs;
        private long _taggersOffset_Main;
        private long _taggersOffset_Latency;
        private long _taggersOffset_Drift;



        //-----------------------------------
        //----  P R O P E R T I E S
        //-----------------------------------

        public DensityMatrixMeasurement DensMeas;
        public StateCorrection StateCorr;
        public ITimeTagger StateCorrTimeTagger;

        //SecQNet Connection
        public SecQNetServer secQNetServer { get; private set; }

        //Time Tagger
        public ITimeTagger ServerTimeTagger { get; set; }
        public ITimeTagger ClientTimeTagger { get; set; }
        
        //Rotation Stages
        public SMC100Controller _smcController { get; private set; }
        public SMC100Stage _HWP_A { get; private set; }
        public KPRM1EStage _QWP_A { get; private set; }
        public SMC100Stage _HWP_B { get; private set; }
        public KPRM1EStage _QWP_B { get; private set; }

        public SyncStatus SynchronizationStatus
        {
            get { return _sync_status; }
        }

        public int NumSyncSteps { get; set; } = 20;

        //-----------------------------------
        //----  E N U M E R A T O R S
        //-----------------------------------

        public enum SyncStatus
        {
            Sync_Required,
            Mainsync_GetServerTimetags,
            Mainsync_GetClientTimetags,
            Corr_GetServerTimeTags,
            Corr_GetClientTimeTags,
            Correlate,
            Finalize,
            Sync_Finished
        };

        //-----------------------------------
        //----  E V E N T S
        //-----------------------------------
        
        public event EventHandler<SyncFinishedEventArgs> SyncFinished;

        private void OnSyncFinished(SyncFinishedEventArgs e)
        {
            SyncFinished?.Raise(this, e);
        }
        

        //Constructor
        public EQKDServerModel(Action<string> loggercallback)
        {
            _loggerCallback = loggercallback;
            secQNetServer = new SecQNetServer(_loggerCallback);

            //TimeTaggerFactory servertaggerFactory = new TimeTaggerFactory("ServerTagger", _loggerCallback) { SecQNetServer = secQNetServer};
            //ServerTimeTagger = servertaggerFactory.GetDefaultTimeTagger();
            //TimeTaggerFactory clienttaggerFactory = new TimeTaggerFactory("ClientTagger", _loggerCallback) { SecQNetServer = secQNetServer };
            //ClientTimeTagger = clienttaggerFactory.GetDefaultTimeTagger();

            //Instanciate TimeTaggers
            ServerTimeTagger = new HydraHarp(_loggerCallback);
            ClientTimeTagger = new NetworkTagger(_loggerCallback) { secQNetServer = secQNetServer };    

            _sync_status = SyncStatus.Sync_Required;

            //DENSITY MATRIX TEST

            //Instanciate and connect rotation Stages
            _QWP_A = new KPRM1EStage(_loggerCallback);
            _QWP_B = new KPRM1EStage(_loggerCallback);

            _QWP_A.Connect("27254310");

            _QWP_B.Connect("27504148");
            

            _smcController = new SMC100Controller(_loggerCallback);
            _smcController.Connect("COM4");

            _HWP_A = _smcController[1];
            _HWP_B = _smcController[2];


            //Define rotation sense and offset
            if (_HWP_A != null)
            {
                _HWP_A.Offset = 45.01; 
            }

            if (_HWP_B != null)
            {
                _HWP_B.Offset = 100.06;
            }

            _QWP_A.Offset = 35.15;
            _QWP_B.Offset = 63.84;

            //Connect timetagger
            ServerTimeTagger.Connect(new List<long> { 0,38016,0,0 });

            DensMeas = new DensityMatrixMeasurement(ServerTimeTagger, _HWP_A, _QWP_A, _HWP_B, _QWP_B, _loggerCallback);


            //STATE CORRECTION
            StateCorrTimeTagger = new SITimeTagger(loggercallback);
            //StateCorrTimeTagger.Connect(new List<long> { 0, 0, 0, 0, 9728, 16000, 14976, 18304 , 0, 0, 0, 0, 0, 0, 0, 0 });
            StateCorrTimeTagger.Connect(new List<long> { 0, 0, 0, 0, 9728-13972, 16000- 13972, 14976-13972, 18304-13972, 0, 0, 0, 0, 0, 0, 0, 0 });

            StateCorr = new StateCorrection(StateCorrTimeTagger, new List<IRotationStage> { _QWP_A, _HWP_B, _QWP_B }, loggercallback);
     
        }

        public void MeasureDensityMatrix()
        {
            if (!_HWP_A.StageReady || !_HWP_B.StageReady || !_QWP_A.StageReady || !_QWP_B.StageReady)
            {
                WriteLog("Rotation stages not ready.");
                return;
            }

            DensMeas.MeasurePeakAreasAsync();

            //if (!StateCorrTimeTagger.CanCollect)
            //{
            //    WriteLog("TimeTagger not ready");
            //    return;
            //}

            //StateCorr.StartOptimizationAsync();

        }


        public async Task StartSynchronizeAsync()
        {
            System.Diagnostics.Stopwatch stw = new System.Diagnostics.Stopwatch();

            stw.Start();

            _sync_cts = new CancellationTokenSource();
            WriteLog("Synchronization started.");

            //var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            //bool synchronisation_finished = await Task.Factory.StartNew( () => { }).ContinueWith( r => DoSynchronize(_sync_cts.Token), scheduler);

            bool synchronisation_finished = await Task<bool>.Run(() => DoSynchronize(_sync_cts.Token));

            var peaks = testcorrs[0].GetPeaks(6250, 0.1, true);
            Peak MinPeak = peaks.Where(p => p.Height_Relative == peaks.Min(pp => pp.Height_Relative)).FirstOrDefault();
            _taggersOffset_Latency = MinPeak.MeanTime;

            OnSyncFinished(new SyncFinishedEventArgs(testcorrs[0].Histogram_X, testcorrs[0].Histogram_Y, peaks, MinPeak, _taggersOffset_Latency));

            stw.Stop();


            if (synchronisation_finished)
            {
                WriteLog($"Synchronization finished with {NumSyncSteps} steps, {ServerTimeTagger.PacketSize} packetsize, in {stw.Elapsed}");
            }
            else if (_sync_cts.Token.IsCancellationRequested) WriteLog("Synchronization cancelled by request.");
            else WriteLog("Synchronization cancelled due to error");

        }

        private bool DoSynchronize(CancellationToken _sync_ct)
        {
            int sync_steps = 0;
            bool retval = true;

            TimeTags server_tt = new TimeTags();
            TimeTags client_tt = new TimeTags();

            //Configure Correlation Channels
            List<CorrelationGroup> histograms = new List<CorrelationGroup>
                {
                    new Histogram(new List<(byte cA, byte cB)>{ (0, 1) },100000)                  
                };

            testcorrs = new Kurolator(histograms, 30000, 256);

            _sync_status = SyncStatus.Mainsync_GetServerTimetags;

            //Clear buffers and start Start Timetaggers
            ServerTimeTagger.ClearTimeTagBuffer();
            ClientTimeTagger.ClearTimeTagBuffer();
            ServerTimeTagger.StartCollectingTimeTagsAsync();
            ClientTimeTagger.StartCollectingTimeTagsAsync();
            
            //Main state machine
            while (_sync_status != SyncStatus.Sync_Finished)
            {
                if (_sync_ct.IsCancellationRequested || secQNetServer.connectionStatus != SecQNetServer.ConnectionStatus.ClientConnected)
                {
                    retval = false;
                    break;
                }

                switch (_sync_status)
                {
                    case SyncStatus.Mainsync_GetServerTimetags:
                        if (ServerTimeTagger.GetNextTimeTags(out server_tt)) _sync_status = SyncStatus.Mainsync_GetClientTimetags;
                        break;

                    case SyncStatus.Mainsync_GetClientTimetags:
                        if (ClientTimeTagger.GetNextTimeTags(out client_tt))
                        {
                            _taggersOffset_Main = server_tt.time[0] - client_tt.time[0];
                            _sync_status = SyncStatus.Correlate;
                        }
                        break;

                    case SyncStatus.Corr_GetServerTimeTags:
                        if (ServerTimeTagger.GetNextTimeTags(out server_tt)) _sync_status = SyncStatus.Correlate;
                        break;

                    case SyncStatus.Corr_GetClientTimeTags:
                        if (ClientTimeTagger.GetNextTimeTags(out client_tt)) _sync_status = SyncStatus.Correlate;
                        break;

                    case SyncStatus.Correlate:
                        long total_offset = _taggersOffset_Main + _taggersOffset_Latency + _taggersOffset_Drift;

                        Kurolator.CorrResult corrResult = testcorrs.AddCorrelations(server_tt, client_tt, total_offset);

                        if (corrResult == Kurolator.CorrResult.PartnerBehind)
                        {
                            _sync_status = SyncStatus.Corr_GetClientTimeTags;
                            sync_steps++;
                        }
                        else
                        {
                            _sync_status = SyncStatus.Corr_GetServerTimeTags;

                        }

                        if (sync_steps >= NumSyncSteps) _sync_status = SyncStatus.Finalize;

                        break;

                    case SyncStatus.Finalize:
                        _sync_status = SyncStatus.Sync_Finished;
                        break;
                    default:
                        break;
                }
            }

            //Stop collecting timetags
            StopAllTimeTaggers();

            return retval;
        }

        private void StopAllTimeTaggers()
        {
            ServerTimeTagger.StopCollectingTimeTags();
            ClientTimeTagger.StopCollectingTimeTags();
        }

        public void StopSynchronize()
        {
            _sync_cts.Cancel();
        }

       private void WriteLog(string message)
        {
            _loggerCallback?.Invoke("EQKD Server: " + message);
        }
    }

    public class SyncFinishedEventArgs : EventArgs
    {
        public readonly long[] HistogramX;
        public readonly long[] HistogramY;
        public readonly List<Peak> Peaks;
        public readonly Peak MinPeak;
        public readonly long TimeTaggers_LatencyOffset;

        public SyncFinishedEventArgs(long[] histx, long[] histy, List<Peak> peaks, Peak minpeak, long tt_latencyoffset)
        {
            HistogramX = histx;
            HistogramY = histy;
            Peaks = peaks;
            MinPeak = minpeak;
            TimeTaggers_LatencyOffset = tt_latencyoffset;
        }

    }
}
