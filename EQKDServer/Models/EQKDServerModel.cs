using QKD_Library;
using SecQNet;
using Stage_Library;
using Stage_Library.NewPort;
using Stage_Library.Thorlabs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TimeTagger_Library;
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

        //-----------------------------------
        //----  P R O P E R T I E S
        //-----------------------------------


        //Synchronization and State correction
        public Synchronization TaggerSynchronization;
        public StateCorrection StateCorr;

        //SecQNet Connection
        public SecQNetServer SecQNetServer { get; private set; }

        //Time Tagger
        public ITimeTagger ServerTimeTagger { get; set; }
        public ITimeTagger ClientTimeTagger { get; set; }

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


        //-----------------------------------
        //---- C O N S T R U C T O R
        //-----------------------------------
        public EQKDServerModel(Action<string> loggercallback)
        {
            _loggerCallback = loggercallback;

            SecQNetServer = new SecQNetServer(_loggerCallback);

            //Instanciate TimeTaggers
            ServerTimeTagger = new HydraHarp(_loggerCallback);
            ClientTimeTagger = new NetworkTagger(_loggerCallback) { secQNetServer = SecQNetServer };

            ////Instanciate and connect rotation Stages
            //_smcController = new SMC100Controller(_loggerCallback);
            //_smcController.Connect("COM4");

            //_HWP_A = _smcController[1];
            //_HWP_B = _smcController[2];

            //if (_HWP_A != null)
            //{
            //    _HWP_A.Offset = 45.01;
            //}

            //if (_HWP_B != null)
            //{
            //    _HWP_B.Offset = 100.06;
            //}


            //_HWP_C = new KPRM1EStage(_loggerCallback);
            //_QWP_A = new KPRM1EStage(_loggerCallback);
            //_QWP_B = new KPRM1EStage(_loggerCallback);
            //_QWP_C = new KPRM1EStage(_loggerCallback);
            //_QWP_D = new KPRM1EStage(_loggerCallback);

            //_HWP_C.Connect("27254524");
            //_QWP_A.Connect("27254310");
            //_QWP_B.Connect("27504148");
            //_QWP_C.Connect("27003707");
            //_QWP_D.Connect("27254574");

            //_HWP_C.Offset = 58.5;
            //_QWP_A.Offset = 35.15;
            //_QWP_B.Offset = 63.84;
            //_QWP_C.Offset = 27.3;
            //_QWP_D.Offset = 33.15;


            //Connect timetagger
            ServerTimeTagger.Connect(new List<long> { 0, 38016, 0, 0 });

            //StateCorrTimeTagger.Connect(new List<long> { 0, 0, -2388, -2388, -6016, -256, -1152, 2176, 0, 0, 0, 0, 0, 0, 0, 0 });

            TaggerSynchronization = new Synchronization(_loggerCallback);
            StateCorr = new StateCorrection(ClientTimeTagger, new List<IRotationStage> { _QWP_A, _HWP_B, _QWP_B }, loggercallback);

            ReadServerConfig();
        }

        //--------------------------------------
        //----  M E T H O D S
        //--------------------------------------

        public async Task StartSynchronizeAsync()
        {

            _cts = new CancellationTokenSource();

            //Configure Timetaggers
            int packetsize = 1000000;
            ServerTimeTagger.PacketSize = packetsize;
            ClientTimeTagger.PacketSize = packetsize;

            //Configure Synchronisation
            TaggerSynchronization.LinearDriftCoefficient = 5.52E-5;
            TaggerSynchronization.PVal = 0.0;

            WriteLog("Synchronisation started");

            await Task.Run(() =>
          {
              while (!_cts.Token.IsCancellationRequested)
              {
                  //Collect Timetags
                  ServerTimeTagger.ClearTimeTagBuffer();
                  ClientTimeTagger.ClearTimeTagBuffer();

                  ServerTimeTagger.StartCollectingTimeTagsAsync();
                  ClientTimeTagger.StartCollectingTimeTagsAsync();

                  TimeTags ttAlice;
                  TimeTags ttBob;


                  while (!ServerTimeTagger.GetNextTimeTags(out ttAlice)) Thread.Sleep(10);
                  while (!ClientTimeTagger.GetNextTimeTags(out ttBob)) Thread.Sleep(10);

                  ServerTimeTagger.StopCollectingTimeTags();
                  ClientTimeTagger.StopCollectingTimeTags();

                  SyncClockResults syncClockres = TaggerSynchronization.SyncClocksAsync(ttAlice, ttBob).GetAwaiter().GetResult();

                  var syncCorrres = TaggerSynchronization.SyncCorrelationAsync(ttAlice, syncClockres.CompTimeTags_Bob).GetAwaiter().GetResult();

                  if (syncClockres.IsClocksSync && syncCorrres.IsCorrSync) break;
              }

          });

            WriteLog("Synchronisation Stopped");
        }

        public void StopSynchronize()
        {
            _cts?.Cancel();
        }


        public void ReadServerConfig()
        {
            ServerSettings _readSettings = ReadConfigXMLFile(_serverSettings_XMLFilename);
            if (_readSettings == null)
            {
                WriteLog($"Could not read Configuration file '{_serverSettings_XMLFilename}'");
                return;
            }

            _currentServerSettings = _readSettings;

            //Set configs
            TaggerSynchronization.LinearDriftCoefficient = _currentServerSettings.LinearDriftCoefficient;
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
            _currentServerSettings.LinearDriftCoefficient = TaggerSynchronization.LinearDriftCoefficient;

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

}
