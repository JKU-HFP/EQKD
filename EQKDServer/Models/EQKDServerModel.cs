using Extensions_Library;
using QKD_Library;
using SecQNet;
using Stage_Library;
using Stage_Library.NewPort;
using Stage_Library.Thorlabs;
using System;
using System.Collections.Generic;
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
        //----  P R I V A T E  F I E L D S
        //-----------------------------------

        private Action<string> _loggerCallback;
        private ServerSettings _currentServerSettings = new ServerSettings();
        string _serverSettings_XMLFilename = "ServerSettings.xml";
        CancellationTokenSource _cts;

        List<byte> _secureKeys = new List<byte>();
        List<byte> _bobKeys = new List<byte>();

        //-----------------------------------
        //----  P R O P E R T I E S
        //-----------------------------------


        //Synchronization and State correction
        public Synchronization TaggerSynchronization;
        public StateCorrection StateCorr;
        public DensityMatrixMeasurement densMeas;
        public bool IsSyncActive { get; private set; } = false;

        //SecQNet Connection
        public int PacketSize { get; set; } = 100000;
        public SecQNetServer SecQNetServer { get; private set; }

        //Time Tagger
        public ITimeTagger ServerTimeTagger { get; set; }
        public ITimeTagger ClientTimeTagger { get; set; }

        //Key generation
        public ulong Key_TimeBin { get; set; } = 1000;

        //Rotation Stages
        public SMC100Controller _smcController { get; private set; }
        public SMC100Stage _HWP_A { get; private set; }
        public KPRM1EStage _QWP_A { get; private set; }
        public SMC100Stage _HWP_B { get; private set; }
        public KPRM1EStage _HWP_C { get; private set; }
        public KPRM1EStage _QWP_B { get; private set; }
        public KPRM1EStage _QWP_C { get; private set; }
        public KPRM1EStage _QWP_D { get; private set; }


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
        public EQKDServerModel(Action<string> loggercallback)
        {
            _loggerCallback = loggercallback;

            SecQNetServer = new SecQNetServer(_loggerCallback);

            //Instanciate TimeTaggers
            ServerTimeTagger = new HydraHarp(_loggerCallback)
            {
                DiscriminatorLevel = 250,
                MeasurementMode = HydraHarp.Mode.MODE_T3,
                PacketSize = 500000
            };
            ServerTimeTagger.Connect(new List<long> { 0, -14464, -12160, -4736 }); //Normal
            //ServerTimeTagger.Connect(new List<long> { 0, 38616, 0, 0 });  //Density Matrix

            ClientTimeTagger = new SITimeTagger(_loggerCallback)
            {
                RefChan = 0
            };
            ClientTimeTagger.Connect(new List<long> { 0, 0, -2388, -2388, -6016, -256, -1152, 2176, 0, 0, 0, 0, 0, 0, 0, 0 });


            //ClientTimeTagger = new NetworkTagger(_loggerCallback,SecQNetServer);

            //Instanciate and connect rotation Stages
            _smcController = new SMC100Controller(_loggerCallback);
            _smcController.Connect("COM4");

            _HWP_A = _smcController[1];
            _HWP_B = _smcController[2];

            if (_HWP_A != null)
            {
                _HWP_A.Offset = 45.01;
            }

            if (_HWP_B != null)
            {
                _HWP_B.Offset = 100.06;
            }


            //_HWP_C = new KPRM1EStage(_loggerCallback);
            _QWP_A = new KPRM1EStage(_loggerCallback);
            _QWP_B = new KPRM1EStage(_loggerCallback);
            //_QWP_C = new KPRM1EStage(_loggerCallback);
            //_QWP_D = new KPRM1EStage(_loggerCallback);

            //_HWP_C.Connect("27254524");
            _QWP_A.Connect("27254310");
            _QWP_B.Connect("27504148");
            //_QWP_C.Connect("27003707");
            //_QWP_D.Connect("27254574");

            //_HWP_C.Offset = 58.5;
            _QWP_A.Offset = 35.15;
            _QWP_B.Offset = 63.84;
            //_QWP_C.Offset = 27.3;
            //_QWP_D.Offset = 33.15;
               

            TaggerSynchronization = new Synchronization(ServerTimeTagger, ClientTimeTagger, _loggerCallback);
            StateCorr = new StateCorrection(TaggerSynchronization, new List<IRotationStage> { _QWP_A, _HWP_B, _QWP_B }, _loggerCallback);
            densMeas = new DensityMatrixMeasurement(TaggerSynchronization, _HWP_A, _QWP_A, _HWP_B, _QWP_B, _loggerCallback);          
        }

        //--------------------------------------
        //----  M E T H O D S
        //--------------------------------------

        public async Task StartSynchronizeAsync()
        {
            if (IsSyncActive) return;

            _cts = new CancellationTokenSource();

            //Deactivate client side basis obscuring
            SecQNetServer.ObscureClientTimeTags = false;

            WriteLog("Synchronisation started");

            IsSyncActive = true;

            TaggerSynchronization.Reset();

                        

            await Task.Run(() =>
          {
              //while (!_cts.Token.IsCancellationRequested)
              //{
                 SyncClockResults syncClockRes = TaggerSynchronization.GetSyncedTimeTags(PacketSize);

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

        public void StopSynchronize()
        {
           if(IsSyncActive) _cts?.Cancel();
        }

        public async Task StartFiberCorrectionAsync()
        {
            //await densMeas.MeasurePeakAreasAsync();

            //await StateCorr.StartOptimizationAsync();

            await StartKeyGeneration();
        }

        public async Task StartKeyGeneration()
        {
            bool local = true;
            PacketSize = 1000000;

            SecQNetServer.ObscureClientTimeTags = true;

            await Task.Run(() =>
           {

               if (!local)
               {
                   byte bR = SecQNet.SecQNetPackets.TimeTagPacket.RectBasisCodedChan;
                   byte bD = SecQNet.SecQNetPackets.TimeTagPacket.DiagbasisCodedChan;
                   List<(byte cA, byte cB)> keyCorrConfig = new List<(byte cA, byte cB)>
                   {
                       //Rectilinear
                       (0,bR),(1,bR),
                       //Diagonal
                       (2,bD),(3,bD)
                   };

                   //Get Key Correlations
                   SyncClockResults syncRes = TaggerSynchronization.GetSyncedTimeTags(PacketSize);

                   Histogram key_hist = new Histogram(keyCorrConfig, Key_TimeBin);
                   Kurolator key_corr = new Kurolator(new List<CorrelationGroup> { key_hist }, Key_TimeBin);

                   key_corr.AddCorrelations(syncRes.TimeTags_Alice, syncRes.CompTimeTags_Bob);

                   //KEY SIFTING
                   List<long> aliceKeyTimes = key_hist.Correlations.Select(corr => corr.t1).ToList();
                   List<long> bobKeyTimes = key_hist.Correlations.Select(corr => corr.t2).ToList();

                   //Register key at Alice
                   List<int> aliceKeyIndices = syncRes.TimeTags_Alice.time.GetIndicesOf(aliceKeyTimes).ToList();
                   aliceKeyIndices.ForEach((i) =>
                   {
                       byte act_chan = syncRes.TimeTags_Alice.chan[i];
                       _secureKeys.Add(act_chan == 5 || act_chan == 7 ? (byte)0 : (byte)1);
                   });

                   //Register key at Bob
                   List<int> bobKeyIndices = syncRes.CompTimeTags_Bob.time.GetIndicesOf(bobKeyTimes).ToList();
                   TimeTags bobSiftedTimeTags = new TimeTags(new byte[] { }, bobKeyIndices.Select(bi => syncRes.TimeTags_Bob.time[bi]).ToArray());

                   //Send sifted tags to bob
                   SecQNetServer.SendSiftedTimeTags(bobSiftedTimeTags);
               }

               else
               {
                   while (true)
                   {
                       List<byte> newAliceKeys = new List<byte>();
                       List<byte> newBobKeys = new List<byte>();

                       List<(byte cA, byte cB)> keyCorrConfig = new List<(byte cA, byte cB)>
                       {
                           //Rectilinear
                           (1,5),(1,6),(2,5),(2,6),
                           //Diagonal
                           (3,7),(3,8),(4,7),(4,8)
                       };
                       List<(byte cA, byte cB)> bellTestConfig = new List<(byte cA, byte cB)>
                       {
                           //Mixed bases
                           (1,7),(1,8),(2,7),(2,8),
                           (3,5),(3,6),(4,5),(4,6)
                       };

                       //Get Key Correlations
                       TimeTags tt = TaggerSynchronization.GetSingleTimeTags(1, PacketSize);

                       long tspan = tt.time.Last() - tt.time.First();

                       Histogram key_hist = new Histogram(keyCorrConfig, Key_TimeBin);          
                       Kurolator key_corr = new Kurolator(new List<CorrelationGroup> { key_hist }, Key_TimeBin);

                       Histogram bell_hist = new Histogram(bellTestConfig, 100000);
                       Kurolator bell_corr = new Kurolator(new List<CorrelationGroup> { bell_hist }, 100000);

                       key_corr.AddCorrelations(tt, tt);
                       bell_corr.AddCorrelations(tt, tt);

                       List<Peak> peaks = bell_hist.GetPeaks();
                       (double val, double err) relmeanpeakarea = bell_hist.GetRelativeMiddlePeakArea();                                          

                       OnKeysGenerated(new KeysGeneratedEventArgs(bell_hist.Histogram_X, bell_hist.Histogram_Y));

                       //KEY SIFTING
                       List<long> aliceKeyTimes = key_hist.Correlations.Select(corr => corr.t1).ToList();
                       List<long> bobKeyTimes = key_hist.Correlations.Select(corr => corr.t2).ToList();

                       //Register key at Alice
                       List<int> aliceKeyIndices = tt.time.GetIndicesOf(aliceKeyTimes).ToList();
                       aliceKeyIndices.ForEach((i) =>
                       {
                           byte act_chan = tt.chan[i];
                           newAliceKeys.Add(act_chan == 1 || act_chan == 3 ? (byte)0 : (byte)1);
                       });

                       //Register key at Bob
                       List<int> bobKeyIndices = tt.time.GetIndicesOf(bobKeyTimes).ToList();
                       bobKeyIndices.ForEach((i) =>
                       {
                           byte act_chan = tt.chan[i];
                           newBobKeys.Add(act_chan == 5 || act_chan == 7 ? (byte)0 : (byte)1);
                       });

                       //Check QBER
                       _secureKeys.AddRange(newAliceKeys);
                       _bobKeys.AddRange(newBobKeys);

                       int sum_err = 0;
                       for (int i = 0; i < _secureKeys.Count; i++)
                       {
                           if (_secureKeys[i] != _bobKeys[i]) sum_err++;
                       }

                       //Write to file
                       File.AppendAllLines("AliceKey.txt", newAliceKeys.Select(k => k.ToString()));
                       File.AppendAllLines("BobKey.txt", newBobKeys.Select(k => k.ToString()));

                       double QBER = (double)sum_err / _secureKeys.Count;
                       double rate = aliceKeyIndices.Count / (tspan / 1E12);

                       WriteLog($"QBER: {QBER:F3} | rate: {rate:F3} | BellTest middlepeak: {relmeanpeakarea:F4}");
                   }
               }

           });
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

            _currentServerSettings.LinearDriftCoefficient = TaggerSynchronization.LinearDriftCoefficient;
            _currentServerSettings.LinearDriftCoeff_NumVar = TaggerSynchronization.LinearDriftCoeff_NumVar;
            _currentServerSettings.LinearDriftCoeff_Var = TaggerSynchronization.LinearDriftCoeff_Var;
            _currentServerSettings.TimeWindow = TaggerSynchronization.ClockSyncTimeWindow;
            _currentServerSettings.TimeBin = TaggerSynchronization.ClockTimeBin;

            _currentServerSettings.FiberOffset = TaggerSynchronization.FiberOffset;

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
