using Controller.XYStage;
using Extensions_Library;
using QKD_Library;
using QKD_Library.Characterization;
using QKD_Library.Synchronization;
using SecQNet;
using Stage;
using Stage.NewPort;
using Stage.Thorlabs;
using Stage.PI;
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
using System.Windows;
using EQKDServer.Models.Hardware;

namespace EQKDServer.Models
{
    public class EQKDServerModel
    {
        //-----------------------------------
        //---- C O N S T A N T S 
        //-----------------------------------

        const uint REMOVEDPOS = 2;
        const uint INSERTEDPOS = 1;

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

        int syncFailedcounter = 0;

        //-----------------------------------
        //----  P R O P E R T I E S
        //-----------------------------------

        //Hardware Connection
        public HardwareInterface Hardware { get; set; }

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


        //Key generation
        public ulong Key_TimeBin { get; set; } = 1000;
        public string KeyFolder { get; set; } = "Key";

        public QKey AliceKey { get; private set; } = new QKey()
        {
            RectZeroChan = 0,
            DiagZeroChan = 2
        };

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
        public EQKDServerModel(Action<string> loggercallback, Func<string, string, int> userprompt)
        {
            _loggerCallback = loggercallback;
            _userprompt = userprompt;

            SecQNetServer = new SecQNetServer(_loggerCallback);
            Hardware = new HardwareInterface(_loggerCallback, SecQNetServer);

            AliceBobSync = new TaggerSync(
                Hardware.ServerTimeTagger,
                Hardware.ClientTimeTagger,
                _loggerCallback,
                _userprompt,
                Hardware.TriggerShutter,
                Hardware.PolarizerControl
            );

            FiberCorrection = new StateCorrection(
                AliceBobSync,
                new List<IRotationStage> {
                    Hardware._QWP_A,
                    Hardware._HWP_B,
                    Hardware._QWP_D
                },
                _loggerCallback
            );

            AliceBobDensMatrix = new DensityMatrix(Hardware.ServerTimeTagger, Hardware._HWP_B, Hardware._QWP_B, Hardware._HWP_A, Hardware._QWP_D, _loggerCallback, xystab: Hardware.XYStabilizer) //Order: HWP_A, QWP_A, HWP_B, QWP_B = same order as used in Basis definition
            {
                ChannelA = 0,
                ChannelB = 1
            };

            //Create key folder
            if (!Directory.Exists(KeyFolder)) Directory.CreateDirectory(KeyFolder);

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

        public async Task TestClock()
        {
            WriteLog("Start Testing Clocks...");
            SecQNetServer.ObscureClientTimeTags = false;
            await Task.Run(() => AliceBobSync.TestClock(PacketSize, PacketTImeSpan));
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


                if (syncClockRes.IsSync)
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
            await FiberCorrection.StartOptimizationAsync();
        }

        public Task StartDensityMatrixAsync()
        {
            //Read generated basis configuration
            var filestrings = File.ReadAllLines(@"I:\public\NANOSCALE SEMICONDUCTOR GROUP\1. DATA\BIG-LAB\2021\01\20\SA323_qd20\bases.txt");
            List<double[]> bases = filestrings.Select(line => line.Split(' ').Select(vals => double.Parse(vals)).ToArray()).ToList();

            AliceBobDensMatrix.PacketTimeSpan = PacketTImeSpan;
            AliceBobDensMatrix.BackupRawData = true;
            return AliceBobDensMatrix.MeasurePeakAreasAsync(userBasisConfigs: DensityMatrix.StdBasis9corr); //Def Basis here
        }

        public void Cancel()
        {
            AliceBobDensMatrix?.CancelMeasurement();
            _cts?.Cancel();
            Hardware.XYStabilizer?.Cancel();
        }

        public void ResetTaggers()
        {
            AliceBobSync.RequestReset();
            IsSyncActive = false;
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

                    if (Hardware.AutoStabilization &&
                       !Hardware.XYStabilizer.StabilizationActive &&
                        Hardware.XYStabilizer.PVBufferFilled &&
                        Hardware.XYStabilizer.IsBelowTriggerPoint)
                    {
                        Hardware.XYStabilizer.Correct();
                    }

                    switch (Hardware.ClientTimeTagger)
                    {
                        case NetworkTagger nwtag:

                            if (check_qber_counter >= check_qber_period)
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

            if (!File.Exists(stats_file)) File.WriteAllLines(stats_file, new string[] { "Time \t Rate \t Qber \t GlobalTimeOffset \t PacketOverlap \t TimeBin" });

            //Get Key Correlations
            TaggerSyncResults syncRes = AliceBobSync.GetSyncedTimeTags(packetSize: PacketSize, packetTimeSpan: PacketTImeSpan);


            //Log timetags and global offset if sync failed
            string failedTagsFolder = "SyncDebug";
            if (!Directory.Exists(failedTagsFolder)) Directory.CreateDirectory(failedTagsFolder);
            var date = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            File.AppendAllLines(Path.Combine(failedTagsFolder, "GlobalOffsets.txt"), new string[] { $"{date}\t{AliceBobSync.GlobalClockOffset}" });

            if (!syncRes.IsSync)
            {
                WriteLog("Not in sync, no keys generated");

                syncRes.TimeTags_Alice.ToFile(Path.Combine(failedTagsFolder, $"{date}_{syncFailedcounter:D4}_Alice.dat"));
                syncRes.TimeTags_Bob.ToFile(Path.Combine(failedTagsFolder, $"{date}_{syncFailedcounter:D4}_Bob.dat"));
                if (syncRes.CompTimeTags_Bob != null) syncRes.CompTimeTags_Bob.ToFile(Path.Combine(failedTagsFolder, $"{date}_{syncFailedcounter:D4}_BobComp.dat"));

                syncFailedcounter++;

                return;
            }

            var key_entries = AliceKey.GetKeyEntries(syncRes.TimeTags_Alice, syncRes.CompTimeTags_Bob, Key_TimeBin);
            AliceKey.AddKey(key_entries);
            //Register key at Bob                
            TimeTags bobSiftedTimeTags = new TimeTags(new byte[] { }, key_entries.Select(fe => (long)fe.index_bob).ToArray());
            //Send sifted tags to bob
            SecQNetServer.SendSiftedTimeTags(bobSiftedTimeTags, _currKeyNr);

            //Statistics
            double overlap = Kurolator.GetOverlapRatio(syncRes.TimeTags_Alice, syncRes.CompTimeTags_Bob);
            double rate = AliceKey.GetRate(syncRes.TimeTags_Alice, key_entries);
            WriteLog($"{key_entries.Count} keys generated with a raw rate of {rate:F3} keys/s");
            File.AppendAllLines(stats_file, new string[] { DateTime.Now.ToString()+"\t"+rate.ToString("F2")+"\t"+_currQber.ToString("F4")+"\t"+
                                                           AliceBobSync.GlobalClockOffset_Relative.ToString()+"\t"+overlap.ToString("F2")+"\t"+Key_TimeBin.ToString() });
        }

        private void _generateKeysLocal()
        {

            TimeTags ttA = new TimeTags();
            TimeTags ttB = new TimeTags();

            switch (EXTERNAL_CLOCK)
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

        private LocalSiftingResult _localKeySifting(TimeTags ttA, TimeTags ttB, bool two_taggers = true)
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
