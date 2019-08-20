using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thorlabs.TLPM_64.Interop;
using System.Runtime.InteropServices;
using Extensions_Library;
using Stage_Library;
using System.IO;

namespace Entanglement_Library
{
    public class Stokes
    {
        //#################################################
        //##  P R O P E R T I E S
        //#################################################

        public bool IsConnected { get; set; } = false;


        //#################################################
        //##  P R I V A T E S
        //#################################################

        private Action<string> _loggerCallback;
        private TLPM _powermeter;
        private IRotationStage _rotStage;

        public Stokes(Action<string> loggercallback, IRotationStage rotStage)
        {
            _loggerCallback = loggercallback;
            _rotStage = rotStage;
        }

        public void Connect()
        {
            double powerValue;

            //Connect to Thorlabs

            try
            {
                HandleRef Instrument_Handle = new HandleRef();

                TLPM searchDevice = new TLPM(Instrument_Handle.Handle);

                uint count = 0;

                string firstPowermeterFound = "";

                try
                {
                    int pInvokeResult = searchDevice.findRsrc(out count);

                    if (count > 0)
                    {
                        StringBuilder descr = new StringBuilder(1024);

                        searchDevice.getRsrcName(0, descr);

                        firstPowermeterFound = descr.ToString();
                    }
                }
                catch { }

                if (count == 0)
                {
                    searchDevice.Dispose();
                    WriteLog("No power meter could be found.");
                    return;
                }

                _powermeter = new TLPM(firstPowermeterFound, false, false);  //  For valid Ressource_Name see NI-Visa documentation.

                int err = _powermeter.measPower(out powerValue);
                WriteLog("powerValue.ToString()");

                IsConnected = true;
            }
            catch (BadImageFormatException bie)
            {
                WriteLog(bie.Message);
            }
            catch (NullReferenceException nre)
            {
                WriteLog(nre.Message);
            }
            catch (ExternalException ex)
            {
                WriteLog(ex.Message);
            }
        }

        public double GetPower()
        {
            if(!IsConnected)
            {
                WriteLog("Powermeter not connected");
                return -1.0;
            }

            double power;
            int err = _powermeter.measPower(out power);
            return power;
        }

        public void GetStokes(string filename = "Stokes.txt")
        {
            //Scan 360 degree
            for (int pos=0; pos<360; pos=pos+2)
            {
                _rotStage.Move_Absolute(pos);
                File.AppendAllLines(filename, new string[] { $"{pos:F2}\t{GetPower()}" });
            }
        }

        private void WriteLog(string message)
        {
            _loggerCallback?.Invoke("Stokes: " + message);
        }
    }
}
