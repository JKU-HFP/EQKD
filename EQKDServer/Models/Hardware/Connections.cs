using Controller.XYStage;
using SecQNet;
using Stage.NewPort;
using Stage.PI;
using Stage.Thorlabs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library.TimeTagger;

namespace EQKDServer.Models.Hardware
{
    public class Connections: Catalogue
    {
        public bool ConnectTimeTaggers(Action<string> loggerCallback, SecQNetServer secQNetServer, bool isExternalClock = false)
        {
            try
            {
                Hydra = new HydraHarp(loggerCallback)
                {
                    DiscriminatorLevel = 200,
                    SyncDivider = 8,
                    SyncDiscriminatorLevel = 200,
                    MeasurementMode = HydraHarp.Mode.MODE_T3,
                    ClockMode = isExternalClock ? HydraHarp.Clock.External : HydraHarp.Clock.Internal,
                    PackageMode = TimeTaggerBase.PMode.ByEllapsedTime
                };
                Hydra.Connect(new List<long> { 0, -3950, -11650 + 25000, -12300 + 25000 }); //DensMatrix --> Delay times of ch0, ch1, ch2, ch3 in [ps]
                ServerTimeTagger = Hydra;
                nwtagger = new NetworkTagger(loggerCallback, secQNetServer);
                ClientTimeTagger = nwtagger;

            }
            catch (Exception e)
            {
                loggerCallback?.Invoke("Time Tagger: " + e.Message);
                return false;
            }
            return true;
        }
        public bool ConnectLinearStages(Action<string> loggerCallback, string port = "M-505.2DG\nM-505.2DG")
        {
            try
            {
                //Connect linear stages for XY stabilization
                XY_Controller = new PI_C843_Controller(loggerCallback);
                XY_Controller.Connect(port);
                if (XY_Controller.GetStages().Count != 0)
                {
                    XStage = XY_Controller.GetStages()[0];
                    YStage = XY_Controller.GetStages()[1];
                }
                //Instanciate XYStabilizer
                string xystabdir = "XYStabilization";
                if (!Directory.Exists(xystabdir)) Directory.CreateDirectory(xystabdir);
                XYStabilizer = new XYStabilizer(XStage, YStage, () => ServerTimeTagger.GetCountrate().Sum(), loggerCallback: loggerCallback)
                {
                    StepSize = 5E-4,
                    Logfile = xystabdir + "//xystab_log.txt"
                };
            }
            catch (Exception e)
            {
                loggerCallback?.Invoke("Linear Stages: " + e.Message);
                return false;
            }
            return true;
        }
        public bool ConnectRotationStages(Action<string> loggerCallback, string port = "COM3")
        {
            try
            {
                _smcController = new SMC100Controller(loggerCallback);
                _smcController.Connect(port);
                _smcStages = _smcController.GetStages();
                _HWP_A = _smcStages[1];
                _HWP_B = _smcStages[2];
                if (_HWP_A != null)
                {
                    _HWP_A.Offset = 137.3; //old: 45.01;
                }
                if (_HWP_B != null)
                {
                    _HWP_B.Offset = 12.55; //old: 100.06;
                }
                //_HWP_C = new KPRM1EStage(_loggerCallback);
                //_HWP_C.Connect("27254524");
                //_HWP_C.Offset = 58.5 + 90;

                //_QWP_A = new KPRM1EStage(_loggerCallback);
                //_QWP_A.Connect("27254310");
                //_QWP_A.Offset = 35.92; //old: 35.15

                _QWP_B = new KPRM1EStage(loggerCallback);
                _QWP_B.Connect("27504148");
                _QWP_B.Offset = 156.8; //old: 63.84;

                //_QWP_C = new KPRM1EStage(_loggerCallback);
                //_QWP_C.Connect("27003707");
                //_QWP_C.Offset = 27.3;

                _QWP_D = new KPRM1EStage(loggerCallback);
                _QWP_D.Connect("27254574");
                _QWP_D.Offset = 35.9; //FAST AXIS WRONG ON THORLABS PLATE --> +90°!
            }
            catch (Exception e)
            {
                loggerCallback?.Invoke("Rotation Stages: " + e.Message);
                return false;
            }
            return true;
        }
        public Connections(Action<string> loggerCallback, SecQNetServer secQNetServer)
        {
            try
            {
                ConnectTimeTaggers(loggerCallback, secQNetServer, false);
                ConnectRotationStages(loggerCallback, "COM3");
                ConnectLinearStages(loggerCallback, "M-505.2DG\nM-505.2DG");
            }
            catch (Exception e)
            {
                loggerCallback?.Invoke("Hardware Connections: " + e.Message);
            }
        }
    }
}


//Instanciate and connect filter flippers
//PolarizerFlipper = new MFF101Flipper(_loggerCallback);
//PolarizerFlipper.Connect("37853189");
//PolarizerControl(false); //Open after homing
//ShutterFlipper = new MFF101Flipper(_loggerCallback);
//ShutterFlipper.Connect("37003303"); 
//TriggerShutter(false); //Open after Homing
//Instanciate and connect rotation Stages
//Connect linear stages for XY stabilization
