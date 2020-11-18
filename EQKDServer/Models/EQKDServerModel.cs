using Controller.XYStage;
using Extensions_Library;
using QKD_Library;
using QKD_Library.Characterization;
using QKD_Library.Synchronization;
using SecQNet;
using Stage_Library;
using Stage_Library.NewPort;
using Stage_Library.Thorlabs;
using Stage_Library.PI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;
using TimeTagger_Library.TimeTagger;

namespace EQKDServer.Models
{
    public class EQKDServerModel
    {
        //-----------------------------------
        //---- C O N S T A N T S 
        //-----------------------------------

        const double REMOVEDPOS = 50;
        const double INSERTEDPOS = 98;

        private bool EXTERNAL_CLOCK = false;

        //-----------------------------------
        //----  P R I V A T E  F I E L D S
        //-----------------------------------

        private Action<string> _loggerCallback;
        private Func<string, string, int> _userprompt;
        private ServerSettings _currentServerSettings = new ServerSettings();
        string _serverSettings_XMLFilename = "ServerSettings.xml";
        CancellationTokenSource _cts;

        private double _currQber;
        private int _currKeyNr = 0;

        List<byte> _secureKeys = new List<byte>();
        List<byte> _bobKeys = new List<byte>();

        System.Timers.Timer _stabTestTimer = new System.Timers.Timer();
        
        //-----------------------------------
        //----  P R O P E R T I E S
        //-----------------------------------

        //Synchronization and State correction
        public TaggerSync AliceBobSync { get; private set; }
        public StateCorrection FiberCorrection { get; private set; }
        public DensityMatrix AliceBobDensMatrix { get; private set; }
        public bool IsSyncActive { get; private set; } = false;

        //SecQNet Connection
        public int PacketSize { get; set; } = 100000;
        public long PacketTImeSpan { get; set; } = 2000000000000;
        public bool StabilizeCountrate { get; set; } = true;
        public SecQNetServer SecQNetServer { get; private set; }

        //Time Tagger
        public ITimeTagger ServerTimeTagger { get; set; }
        public ITimeTagger ClientTimeTagger { get; set; }

        //Stabilization
        public XYStabilizer XYStabilizer { get; private set; }
        public bool AutoStabilization { get; set; }

        //Key generation
        public ulong Key_TimeBin { get; set; } = 1000;
        public string KeyFolder { get; set; } = "Key";

        public QKey AliceKey { get; private set; } = new QKey()
        {
            RectZeroChan =0,
            DiagZeroChan=2
        };
        //Rotation Stages
        public SMC100Controller _smcController { get; private set; }
        public SMC100Stage _HWP_A { get; private set; }
        public KPRM1EStage _QWP_A { get; private set; }
        public SMC100Stage _HWP_B { get; private set; }
        public KPRM1EStage _HWP_C { get; private set; }
        public KPRM1EStage _QWP_B { get; private set; }
        public KPRM1EStage _QWP_C { get; private set; }
        public KPRM1EStage _QWP_D { get; private set; }

        //Linear stages
        public KBD101Stage PolarizerStage { get; private set; }
        public PI_C843_Controller XY_Controller { get; private set; }
        public PI_C843_Stage XStage { get; private set; }
        public PI_C843_Stage YStage { get; private set; }

        //-----------------------------------
        //----  E V E N T S
        //-----------------------------------

        public event EventHandler<ServerConfigReadEventArgs> ServerConfigRead;
        private void OnServerConfigRead(ServerConfigReadEventArgs e)
        {
            ServerConfigRead?.Raise(this, e);
        }

        public event EventHandler<KeysGeneratedEventArgs> KeysGenerated;
        private void OnKeysGenerated(KeysGeneratedEventArgs e)
        {
            KeysGenerated?.Raise(this, e);
        }

        //-----------------------------------
        //---- C O N S T R U C T O R
        //-----------------------------------
        public EQKDServerModel(Action<string> loggercallback, Func<string,string,int> userprompt)
        {
            _loggerCallback = loggercallback;
            _userprompt = userprompt;

            SecQNetServer = new SecQNetServer(_loggerCallback);

            //Instanciate TimeTaggers
            HydraHarp hydra = new HydraHarp(_loggerCallback)
            {
                DiscriminatorLevel = 200,
                SyncDivider = 8,
                SyncDiscriminatorLevel = 200,
                MeasurementMode = HydraHarp.Mode.MODE_T2,
                ClockMode = EXTERNAL_CLOCK ? HydraHarp.Clock.External : HydraHarp.Clock.Internal,
                PackageMode = TimeTaggerBase.PMode.ByEllapsedTime
            };
            hydra.Connect(new List<long> { 0, 0, 0, -5688+1100 });

            SITimeTagger sitagger = new SITimeTagger(_loggerCallback)
            {
                RefChan = EXTERNAL_CLOCK ? 1 : 0,
                SyncDiscriminatorVoltage = 0.2,
                RefChanDivider=100,
                SyncRate=10000000,
                PackageMode = TimeTaggerBase.PMode.ByEllapsedTime
            };

            long testoffs = 128;
            long OIC_Alice750m = 0;// -3493504;
            sitagger.Connect(new List<long> { 0+OIC_Alice750m, 0+OIC_Alice750m, -75648+OIC_Alice750m, -78208+OIC_Alice750m, 2176 + testoffs, 2176 + testoffs, 1164 + testoffs, 2176 + testoffs });


            NetworkTagger nwtagger = new NetworkTagger(_loggerCallback,SecQNetServer);

            ServerTimeTagger = hydra;
            ClientTimeTagger = nwtagger;

            //Instanciate and connect rotation Stages
            _smcController = new SMC100Controller(_loggerCallback);
            _smcController.Connect("COM4");
            List<SMC100Stage> _smcStages = _smcController.GetStages();

            _HWP_A = _smcStages[1];
            _HWP_B = _smcStages[2];

            if (_HWP_A != null)
            {
                _HWP_A.Offset = 45.01;
            }

            if (_HWP_B != null)
            {
                _HWP_B.Offset = 100.06;
            }


            //_HWP_C = new KPRM1EStage(_loggerCallback);
            //_HWP_C.Connect("27254524");
            //_HWP_C.Offset = 58.5 + 90;

            _QWP_A = new KPRM1EStage(_loggerCallback);
            _QWP_A.Connect("27254310");
            _QWP_A.Offset = 35.15;

            _QWP_B = new KPRM1EStage(_loggerCallback);
            _QWP_B.Connect("27504148");
            _QWP_B.Offset = 63.84;

            //_QWP_C = new KPRM1EStage(_loggerCallback);
            //_QWP_C.Connect("27003707");
            //_QWP_C.Offset = 27.3;

            //_QWP_D = new KPRM1EStage(_loggerCallback);
            //_QWP_D.Connect("27254574");
            //_QWP_D.Offset = 33.15 + 90; //FAST AXIS WRONG ON THORLABS PLATE --> +90°!

            //Instanciate and connect linear stages
            PolarizerStage = new KBD101Stage(_loggerCallback);
            PolarizerStage.Connect("28250918");

            XY_Controller = new PI_C843_Controller(_loggerCallback);
            XY_Controller.Connect("M-505.2DG\nM-505.2DG");
            XStage = XY_Controller.GetStages()[0];
            YStage = XY_Controller.GetStages()[1];


            //Instanciate XYStabilizer
            XYStabilizer = new XYStabilizer(XStage, YStage, () => ServerTimeTagger.GetCountrate().Sum(), loggerCallback: _loggerCallback)
            {
                StepSize = 5E-4
            };
                

            AliceBobSync = new TaggerSync(ServerTimeTagger, ClientTimeTagger, _loggerCallback, _userprompt, TriggerShutter, PolarizerControl);
            FiberCorrection = new StateCorrection(AliceBobSync, new List<IRotationStage> { _QWP_A, _HWP_A, _QWP_B }, _loggerCallback);
            //AliceBobDensMatrix = new DensityMatrix(AliceBobSync, _HWP_A, _QWP_A, _HWP_B, _QWP_B, _loggerCallback);//Before fiber
            AliceBobDensMatrix = new DensityMatrix(AliceBobSync, _HWP_A, _QWP_A, _HWP_B, _QWP_B, _loggerCallback, xystab: null)
            {
                ChannelA = 2,
                ChannelB = 3
            }; //in Alice/Bob Boxes


            //Create key folder
            if (!Directory.Exists(KeyFolder)) Directory.CreateDirectory(KeyFolder);

            //Set and start Stabilization Test Timer
            _stabTestTimer.Elapsed += _stabTestTimer_Elapsed;
            _stabTestTimer.Interval = 5000;
            _stabTestTimer.Start();

        }


        private void PolarizerControl(bool status)
        {
            switch (status)
            {
                case true:
                    PolarizerStage.Move_Absolute(INSERTEDPOS);
                    break;
                case false:
                    PolarizerStage.Move_Absolute(REMOVEDPOS);
                    break;
                default:
                    break;
            }
        }
        private void TriggerShutter()
        {
            _QWP_A.SetOutput(true);
            Thread.Sleep(100);
            _QWP_A.SetOutput(false);
        }

        //--------------------------------------
        //----  M E T H O D S
        //--------------------------------------

        /// <summary>
        /// 
        /// </summary>
        /// <param name="direction"> 
        /// 0 ... Y+
        /// 1 ... Y-
        /// 2 ... X+
        /// 3 ... X-
        /// </param>
        /// <returns></returns>
        public Task MoveXYStage(int direction)
        {
            double step = 0.2E-3;
            
            return Task.Run( () =>
            {
                switch (direction)
                {
                    case 0:
                        YStage.Move_Relative(step);
                        break;
                    case 1:
                        YStage.Move_Relative(-step);
                        break;
                    case 2:
                        XStage.Move_Relative(step);
                        break;
                    case 3:
                        XStage.Move_Relative(-step);
                        break;
                }
            });
        }

        public Task XYStageOptimize()
        {
            return XYStabilizer.CorrectAsync();          
        }

        private void _stabTestTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //if (AutoStabilization &&
            //    !XYStabilizer.StabilizationActive && XYStabilizer.PVBufferFilled
            //    && XYStabilizer.ProcessValue < (XYStabilizer.SetPoint - 2*(XYStabilizer.SPTolerance)))
            //{
            //    XYStabilizer.CorrectAsync();
            //}
        }

        public async Task TestClock()
        {
            WriteLog("Start Testing Clocks...");
            SecQNetServer.ObscureClientTimeTags = false;
            await Task.Run(() => AliceBobSync.TestClock(PacketSize,PacketTImeSpan));
        }

        public async Task StartSynchronizeAsync()
        {
            if (IsSyncActive) return;

            _cts = new CancellationTokenSource();

            //Deactivate client side basis obscuring
            SecQNetServer.ObscureClientTimeTags = false;

            WriteLog("Synchronisation started");

            IsSyncActive = true;

                               
            await Task.Run(() =>
          {
              //while (!_cts.Token.IsCancellationRequested)
              //{
              TaggerSyncResults syncClockRes = AliceBobSync.GetSyncedTimeTags(packetSize: PacketSize, packetTimeSpan: PacketTImeSpan);


              if(syncClockRes.IsSync)
              {
                  List<(byte cA, byte cB)> _clockChanConfig = new List<(byte cA, byte cB)>
                {
                    //Clear Basis
                    (0,5),(0,6),(0,7),(0,8),
                    (1,5),(1,6),(1,7),(1,8),
                    (2,5),(2,6),(2,7),(2,8),
                    (3,5),(3,6),(3,7),(3,8),

                   // Obscured Basis
                    //(0,oR),(0,oD),(1,oR),(1,oD),(2,oR),(2,oD),(3,oR),(3,oD)
                };

                  Histogram trackingHist = new Histogram(_clockChanConfig, 200000, 512);

                  Kurolator trackingKurolator = new Kurolator(new List<CorrelationGroup> { trackingHist }, 200000);
                  trackingKurolator.AddCorrelations(syncClockRes.TimeTags_Alice, syncClockRes.CompTimeTags_Bob, 0);
              }

              //File.AppendAllLines("SyncTest.txt", new string[] { syncClockRes.NewLinearDriftCoeff + "\t" + syncClockRes.GroundLevel +"\t" + syncClockRes.Sigma });

              //    if (syncClockRes.IsClocksSync)
              //    {
              //        SyncCorrResults syncCorrres = TaggerSynchronization.SyncCorrelationAsync(syncClockRes.TimeTags_Alice, syncClockRes.CompTimeTags_Bob).GetAwaiter().GetResult();
              //    }
              //}

          });

            IsSyncActive = false;

            WriteLog("Synchronisation Stopped");
        }
        
        public async Task StartFiberCorrectionAsync()
        {
            SecQNetServer.ObscureClientTimeTags = false;

            AliceBobDensMatrix.PacketTimeSpan = PacketTImeSpan;
            //await AliceBobDensMatrix.MeasurePeakAreasAsync();

            await FiberCorrection.StartOptimizationAsync();
        }

        public Task StartDensityMatrixAsync()
        {
            //Read generated basis configuration
            //var filestrings = File.ReadAllLines(@"E:\Dropbox\Dropbox\Coding\Python-Scripts\JKULib\Entanglement\bases.txt");
            //List<double[]> bases = filestrings.Select(line => line.Split(' ').Select(vals => double.Parse(vals)).ToArray()).ToList();

            //Use 16 Basis
            AliceBobDensMatrix.PacketTimeSpan = PacketTImeSpan;
            return AliceBobDensMatrix.MeasurePeakAreasAsync(userBasisConfigs:  DensityMatrix.StdBasis36);
        }

        public void Cancel()
        {
            AliceBobDensMatrix?.CancelMeasurement();
            _cts?.Cancel();
            XYStabilizer?.Cancel();
        }

        public async Task StartKeyGenerationAsync()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            int check_qber_period = 10;
            int check_qber_counter = check_qber_period;

            while (File.Exists(Path.Combine(KeyFolder, $"Key_Alice_{_currKeyNr:D4}.txt"))) _currKeyNr++;

            SecQNetServer.ObscureClientTimeTags = true;

            WriteLog("Starting secure key generation");

            await Task.Run(() =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {

                    if (AutoStabilization &&
                       !XYStabilizer.StabilizationActive && XYStabilizer.PVBufferFilled
                       && XYStabilizer.IsBelowTriggerPoint)
                    {
                        XYStabilizer.Correct();
                    }

                    switch (ClientTimeTagger)
                    {
                        case NetworkTagger nwtag:

                            if(check_qber_counter>=check_qber_period)
                            {
                                SecQNetServer.ObscureClientTimeTags = false;
                                _checkQberNetwork();
                                SecQNetServer.ObscureClientTimeTags = true;
                                check_qber_counter = 0;
                            }
                            else _generateKeysNetwork();

                            check_qber_counter++;
                            break;

                        default:

                            _generateKeysLocal();
                            break;
                    }
                }
            });

            WriteLog("Secure key generation stopped.");
        }

        private void _checkQberNetwork()
        {
            
            TaggerSyncResults syncRes = AliceBobSync.GetSyncedTimeTags(packetSize: PacketSize, packetTimeSpan: PacketTImeSpan);
            if (!syncRes.IsSync)
            {
                WriteLog("Not in sync, QBER not checked");
                return;
            }

            LocalSiftingResult sr = _localKeySifting(syncRes.TimeTags_Alice, syncRes.CompTimeTags_Bob);

            WriteLog($"Current QBER: {sr.QBER:F4}");
            _currQber = sr.QBER; 

        }

        private void _generateKeysNetwork()
        {
            AliceKey.FileName = Path.Combine(KeyFolder, $"Key_Alice_{_currKeyNr:D4}.txt");
            string stats_file = Path.Combine(KeyFolder, $"Stats_{_currKeyNr:D4}.txt");

            if (!File.Exists(stats_file)) File.WriteAllLines(stats_file, new string[] { "Time \t Rate \t Qber \t GlobalTimeOffset \t PacketOverlap" });

            //Get Key Correlations
            TaggerSyncResults syncRes = AliceBobSync.GetSyncedTimeTags(packetSize: PacketSize, packetTimeSpan: PacketTImeSpan);

            if (!syncRes.IsSync)
            {
                WriteLog("Not in sync, no keys generated");
                return;
            }
                 
            var key_entries = AliceKey.GetKeyEntries(syncRes.TimeTags_Alice, syncRes.CompTimeTags_Bob);
            AliceKey.AddKey(key_entries);
            //Register key at Bob                
            TimeTags bobSiftedTimeTags = new TimeTags(new byte[] { }, key_entries.Select(fe => (long)fe.index_bob).ToArray());
            //Send sifted tags to bob
            SecQNetServer.SendSiftedTimeTags(bobSiftedTimeTags,_currKeyNr);

            //Statistics
            double overlap = Kurolator.GetOverlapRatio(syncRes.TimeTags_Alice, syncRes.CompTimeTags_Bob);
            double rate = AliceKey.GetRate(syncRes.TimeTags_Alice, key_entries);
            WriteLog($"{key_entries.Count} keys generated with a raw rate of {rate:F3} keys/s");
            File.AppendAllLines(stats_file, new string[] { DateTime.Now.ToString()+"\t"+rate.ToString("F2")+"\t"+_currQber.ToString("F4")+"\t"+
                                                           AliceBobSync.GlobalClockOffset_Relative.ToString()+"\t"+overlap.ToString("F2") });
        }

        private void _generateKeysLocal()
        { 

            TimeTags ttA = new TimeTags();
            TimeTags ttB = new TimeTags();                     

            switch(EXTERNAL_CLOCK)
            {
                //Two timertaggers (Hydra + SI)
                case true:
                    TaggerSyncResults syncRes = AliceBobSync.GetSyncedTimeTags(packetSize: PacketSize, packetTimeSpan: PacketTImeSpan);

                    if (!syncRes.IsSync)
                    {
                        WriteLog("Not in sync, no keys generated");
                        return;
                    }

                    ttA = syncRes.TimeTags_Alice;
                    ttB = syncRes.CompTimeTags_Bob;               
                    break;

                //One Timetagger (SI)
                case false:
                    ttA = AliceBobSync.GetSingleTimeTags(0, packetSize: PacketSize, packetTimeSpan: PacketTImeSpan);
                    ttB = ttA;               
                    break;
            }

            LocalSiftingResult sr = _localKeySifting(ttA, ttB, EXTERNAL_CLOCK);
 
            //Write to file
            File.AppendAllLines("AliceKey.txt", sr.newAliceKeys.Select(k => k.ToString()));
            File.AppendAllLines("BobKey.txt", sr.newBobKeys.Select(k => k.ToString()));

            File.AppendAllLines("KeyStats.txt", new string[] { DateTime.Now.ToString() + "," + sr.rate.ToString() + "," + sr.QBER.ToString() });
            WriteLog($"QBER: {sr.QBER:F3} | rate: {sr.rate:F3}");

        }

        private LocalSiftingResult _localKeySifting(TimeTags ttA, TimeTags ttB, bool two_taggers=true)
        {

            List<byte> newAliceKeys = new List<byte>();
            List<byte> newBobKeys = new List<byte>();

            List<(byte cA, byte cB)> keyCorrConfig = two_taggers
            ? new List<(byte cA, byte cB)>
                {
                                //Rectilinear
                                (0,5),(0,6),(1,5),(1,6),
                                //Diagonal
                                (2,7),(2,8),(3,7),(3,8)
                }
            : new List<(byte cA, byte cB)>
                {
                                //Rectilinear
                                (1,5),(1,6),(2,5),(2,6),
                                //Diagonal
                                (3,7),(3,8),(4,7),(4,8)
                };

            Histogram key_hist = new Histogram(keyCorrConfig, Key_TimeBin);
            Kurolator key_corr = new Kurolator(new List<CorrelationGroup> { key_hist }, Key_TimeBin);
            key_corr.AddCorrelations(ttA, ttB);

            //Register key at Alice
            foreach (int i in key_hist.CorrelationIndices.Select(i => i.i1))
            {
                byte act_chan = ttA.chan[i];
                newAliceKeys.Add(act_chan == (two_taggers ? 0 : 1) || act_chan == (two_taggers ? 2 : 3) ? (byte)0 : (byte)1);
            };

            //Register key at Bob
            foreach (int i in key_hist.CorrelationIndices.Select(i => i.i2))
            {
                byte act_chan = ttB.chan[i];
                newBobKeys.Add(act_chan == 5 || act_chan == 7 ? (byte)0 : (byte)1);
            };

            //Check QBER
            _secureKeys.AddRange(newAliceKeys);
            _bobKeys.AddRange(newBobKeys);

            int sum_err = 0;
            for (int i = 0; i < _secureKeys.Count; i++)
            {
                if (_secureKeys[i] != _bobKeys[i]) sum_err++;
            }

            long tspan = Math.Max(ttA.time.Last(), ttB.time.Last()) - Math.Min(ttA.time.First(), ttB.time.First());
            double QBER = (double)sum_err / _secureKeys.Count;
            double rate = key_hist.CorrelationIndices.Count / (tspan / 1E12);


            OnKeysGenerated(new KeysGeneratedEventArgs(key_hist.Histogram_X, key_hist.Histogram_Y));

            return new LocalSiftingResult()
            {
                newAliceKeys = newAliceKeys,
                newBobKeys = newBobKeys,
                QBER = QBER,
                rate = rate,
                tspan = tspan
            };
        }

        public void ReadServerConfig()
        {
            ServerSettings _readSettings = ReadConfigXMLFile(_serverSettings_XMLFilename);
            if (_readSettings == null)
            {
                WriteLog($"Could not read Configuration file '{_serverSettings_XMLFilename}', using default settings");
            }

            _currentServerSettings = _readSettings ?? new ServerSettings();

            //Set configs
            OnServerConfigRead(new ServerConfigReadEventArgs(_currentServerSettings));

        }

        private ServerSettings ReadConfigXMLFile(string filename)
        {
            ServerSettings tmp_settings = null;

            if (!File.Exists(filename)) return tmp_settings;

            try
            {
                FileStream fs = new FileStream(filename, FileMode.OpenOrCreate);
                TextReader tr = new StreamReader(fs);
                XmlSerializer xmls = new XmlSerializer(typeof(ServerSettings));

                tmp_settings = (ServerSettings)xmls.Deserialize(tr);

                tr.Close();
            }
            catch (InvalidOperationException ex) //Thrown by Serialize
            {
                throw new InvalidOperationException("Catched InvalidOperationException: " + ex.Message, ex.InnerException);
            }
            catch (IOException ex) //Thrown by FileStream
            {
                throw new InvalidOperationException("Catched IOException: " + ex.Message, ex.InnerException);
            }

            return tmp_settings;
        }

        public void SaveServerConfig()
        {
            //Get Config Data
            _currentServerSettings.PacketSize = PacketSize;

            _currentServerSettings.LinearDriftCoefficient = AliceBobSync.LinearDriftCoefficient;
            _currentServerSettings.LinearDriftCoeff_NumVar = AliceBobSync.LinearDriftCoeff_NumVar;
            _currentServerSettings.LinearDriftCoeff_Var = AliceBobSync.LinearDriftCoeff_Var;
            _currentServerSettings.TimeWindow = AliceBobSync.ClockSyncTimeWindow;
            _currentServerSettings.TimeBin = AliceBobSync.ClockTimeBin;

            //Write Config file
            SaveConfigXMLFile(_currentServerSettings, _serverSettings_XMLFilename);
        }

        private bool SaveConfigXMLFile(ServerSettings settings, string filename)
        {
            try
            {
                TextWriter tw = new StreamWriter(filename);
                XmlSerializer xmls = new XmlSerializer(settings.GetType());

                xmls.Serialize(tw, _currentServerSettings);

                tw.Close();
            }
            catch (InvalidOperationException ex) //Thrown by Serialize
            {
                throw new InvalidOperationException("Catched InvalidOperationException: " + ex.Message, ex.InnerException);
            }
            catch (IOException ex) //Thrown by FileStream
            {
                throw new InvalidOperationException("Catched IOException: " + ex.Message, ex.InnerException);
            }

            WriteLog("TimeTagger factory options saved in '" + filename + "'.");

            return true;
        }

        private void WriteLog(string message)
        {
            _loggerCallback?.Invoke("EQKD Server: " + message);
        }
    }

//##############################
// E V E N T   A R G U M E N T S
//##############################
    public class ServerConfigReadEventArgs : EventArgs
    {
        public ServerSettings StartConfig { get; private set; }

        public ServerConfigReadEventArgs(ServerSettings _config)
        {
            StartConfig = _config;
        }
    }

    public class KeysGeneratedEventArgs : EventArgs
    {
        public long[] HistogramX { get; private set; }
        public long[] HistogramY { get; private set; }

        public KeysGeneratedEventArgs(long[] histX, long[] histY)
        {
            HistogramX = histX;
            HistogramY = histY;
        }
    }

}
