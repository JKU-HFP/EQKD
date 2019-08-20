using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thorlabs.TLPM_64.Interop;
using System.Runtime.InteropServices;
using Extensions_Library;

namespace Entanglement_Library
{
    public class Stokes
    {


        private Action<string> _loggerCallback;
        private TLPM tlpm;

        public Stokes(Action<string> loggercallback)
        {

            _loggerCallback = loggercallback;

            //Test Thorlabs
                       
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

                tlpm = new TLPM(firstPowermeterFound, false, false);  //  For valid Ressource_Name see NI-Visa documentation.
                double powerValue;
                int err = tlpm.measPower(out powerValue);
                WriteLog("powerValue.ToString()");
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
            finally
            {
                if (tlpm != null)
                    tlpm.Dispose();
            }


            ///////
        }

        private void WriteLog(string message)
        {
            _loggerCallback?.Invoke("Stokes: " + message);
        }
    }
}
