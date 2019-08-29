using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;
using TimeTagger_Library.TimeTagger;

namespace Entanglement_Library
{
    public class Synchronization
    {
        //#################################################
        //##  P R O P E R T I E S
        //#################################################

        /// <summary>
        /// Frequency in kHz
        /// </summary>
        public int Frequency { get; set; } = 500;
        /// <summary>
        /// Integration time in milli seconds
        /// </summary>
        public int IntegrationTime { get; set; } = 2000;
        public byte Chan_Tagger1 { get; set; } = 2;
        public byte Chan_Tagger2 { get; set; } = 1;

        //#################################################
        //##  P R I V A T E S
        //#################################################
        private Action<string> _loggerCallback;
        private ITimeTagger _tagger1;
        private ITimeTagger _tagger2;
        private Kurolator _kurolator;
        

        //#################################################
        //##  C O N S T R U C T O R
        //#################################################

        public Synchronization(ITimeTagger tagger1, ITimeTagger tagger2, Action<string> loggerCallback)
        {
            _loggerCallback = loggerCallback;
            _tagger1 = tagger1;
            _tagger2 = tagger2;
        }

        //#################################################
        //##  M E T H O D S 
        //#################################################

        public void MeasureCorrelation()
        {

            //Get timetags

            _tagger1.PacketSize = 100000;
            _tagger2.PacketSize = 100000;

            _tagger1.ClearTimeTagBuffer();
            _tagger2.ClearTimeTagBuffer();

            _tagger1.StartCollectingTimeTagsAsync();
            _tagger2.StartCollectingTimeTagsAsync();

            Thread.Sleep(IntegrationTime);

            _tagger1.StopCollectingTimeTags();
            _tagger2.StopCollectingTimeTags();


            //Calculate correlations
            ulong timewindow = (ulong)(6*  10e6/ Frequency );
            long bin = (long)( timewindow / 1000 );

            Histogram hist = new Histogram(new List<(byte cA, byte cB)> { (Chan_Tagger1, Chan_Tagger2) }, timewindow, bin);
            _kurolator = new Kurolator(new List<CorrelationGroup> { hist }, timewindow);

            bool first = true;
            long offset = 0;


            _tagger1.GetNextTimeTags(out TimeTags tt1);
            _tagger2.GetNextTimeTags(out TimeTags tt2);

            if (first)
            {
                offset = tt1.time[0] - tt2.time[0];
                first = false;
            }

            _kurolator.AddCorrelations(tt1, tt2, offset);
            
                     
        }
     
    }
}
