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
    class Synchronization
    {
        //#################################################
        //##  P R O P E R T I E S
        //#################################################

        /// <summary>
        /// Frequency in kHz
        /// </summary>
        public int Frequency { get; set; } = 100;
        /// <summary>
        /// Integration time in milli seconds
        /// </summary>
        public int IntegrationTime { get; set; } = 100;
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

            _tagger1.ClearTimeTagBuffer();
            _tagger2.ClearTimeTagBuffer();

            _tagger1.StartCollectingTimeTagsAsync();
            _tagger2.StartCollectingTimeTagsAsync();

            Thread.Sleep(IntegrationTime);

            _tagger1.StopCollectingTimeTags();
            _tagger2.StopCollectingTimeTags();


            //Calculate correlations
            ulong timewindow = (ulong)(6*  10e9/ Frequency );
            long bin = (long)( timewindow / 100 );

            Histogram hist = new Histogram(new List<(byte cA, byte cB)> { (Chan_Tagger1, Chan_Tagger2) }, timewindow, bin);
            _kurolator = new Kurolator(new List<CorrelationGroup> { hist }, timewindow);

            while(_tagger1.BufferFillStatus>0 && _tagger2.BufferFillStatus>0)
            {
                if (!_tagger1.GetNextTimeTags(out TimeTags tt1)) continue;
                if (!_tagger1.GetNextTimeTags(out TimeTags tt2)) continue;

                _kurolator.AddCorrelations(tt1, tt2, 0);
            }
                      
        }
     
    }
}
