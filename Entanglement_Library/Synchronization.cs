using Extensions_Library;
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
        public ulong Bin { get; set; } = 1000;
        public ulong TimeWindow { get; set; } = 1000000;
        public int PacketSize { get; set; } = 100000;
        public ulong ShotTime { get; set; } = 10000;
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
        //##  E V E N T S 
        //#################################################

        public event EventHandler<SyncCompleteEventArgs> SyncComplete;
        
        private void OnSyncComplete(SyncCompleteEventArgs e)
        {
            SyncComplete?.Raise(this, e);
        }

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

            _tagger1.PacketSize = PacketSize;
            _tagger2.PacketSize = PacketSize;

            _tagger1.ClearTimeTagBuffer();
            _tagger2.ClearTimeTagBuffer();

            _tagger1.StartCollectingTimeTagsAsync();
            _tagger2.StartCollectingTimeTagsAsync();

            Thread.Sleep(IntegrationTime);

            _tagger1.StopCollectingTimeTags();
            _tagger2.StopCollectingTimeTags();
            

            //Calculate correlations
             Histogram hist = new Histogram(new List<(byte cA, byte cB)> { (Chan_Tagger1, Chan_Tagger2) }, TimeWindow, (long)Bin);
            _kurolator = new Kurolator(new List<CorrelationGroup> { hist }, TimeWindow);

            bool first = true;
            long offset = 0;


            _tagger1.GetNextTimeTags(out TimeTags tt1);
            _tagger2.GetNextTimeTags(out TimeTags tt2);

            long starttime = tt1.time[0];
            int index = tt1.time.TakeWhile(t => t - starttime < (long)ShotTime).Count();
            byte[] reduced_chans = tt1.chan.Take(index).ToArray();
            long[] reduced_times = tt1.time.Take(index).ToArray();

            TimeTags reduced_timetags = new TimeTags(reduced_chans, reduced_times);

            if (first)
            {
                offset = tt1.time[0] - tt2.time[0];
                first = false;
            }

            _kurolator.AddCorrelations(reduced_timetags, tt2, offset);

            OnSyncComplete(new SyncCompleteEventArgs(hist.Histogram_X, hist.Histogram_Y));         
        }
     
    }

    public class SyncCompleteEventArgs : EventArgs
    {
        public long[] HistogramX { get; private set; }
        public long[] HistogramY { get; private set; }

        public SyncCompleteEventArgs(long[] histX, long[] histY)
        {
            HistogramX = histX;
            HistogramY = histY;
        }
    }
}
