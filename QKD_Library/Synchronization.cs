using Extensions_Library;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;
using TimeTagger_Library.TimeTagger;

namespace QKD_Library
{
    public class Synchronization
    {
        //#################################################
        //##  P R O P E R T I E S
        //#################################################

        public ulong TimeBin { get; set; } = 1000;
        public ulong ClockSyncTimeWindow { get; set; } = 100000;
        public long GlobalClockOffset { get; set; } = 0;
        public double LinearDriftCoefficient { get; set; } = 0;
        public double FWHM_Tolerance { get; set; } = 4000;
        public double PVal { get; set; } = 0;
        public ulong ExcitationPeriod { get; set; } = 12500;
        public byte Chan_Tagger1 { get; set; } = 0;
        public byte Chan_Tagger2 { get; set; } = 1;

        /// <summary>
        /// Offset by relative fiber distance of Alice and Bob
        /// </summary>
        public long FiberOffset { get; set; } = 0;
        public ulong CorrSyncTimeWindow { get; set; } = 1000000;
        public long CorrPeakOffset_Tolerance { get; set; } = 2000;

        //#################################################
        //##  P R I V A T E S
        //#################################################

        private Action<string> _loggerCallback;
        private Kurolator _kurolator;

        private bool _firstClockSyncIteration = true;
        private long _init_middlepeakpos = 0;

        //#################################################
        //##  E V E N T S 
        //#################################################

        public event EventHandler<SyncClocksCompleteEventArgs> SyncClocksComplete;
        
        private void OnSyncClocksComplete(SyncClocksCompleteEventArgs e)
        {
            SyncClocksComplete?.Raise(this, e);
        }

        public event EventHandler<SyncCorrResults> SyncCorrComplete;

        private void OnSyncCorrComplete(SyncCorrCompleteEventArgs e)
        {
            SyncCorrComplete?.Raise(this, e);
        }

        //#################################################
        //##  C O N S T R U C T O R
        //#################################################

        public Synchronization(Action<string> loggerCallback)
        {
            _loggerCallback = loggerCallback;
        }

        //#################################################
        //##  M E T H O D S 
        //#################################################
        public void Reset()
        {
            _firstClockSyncIteration = true;
        }

        public async Task<SyncClockResults> SyncClocksAsync(TimeTags ttAlice, TimeTags ttBob)
        {
            Stopwatch sw = new Stopwatch();

            SyncClockResults syncres = await Task<SyncClockResults>.Run(() =>
            {                           
                //Initialize
                WriteLog("Start synchronizing clocks");
                                
                sw.Start();

                Histogram hist = new Histogram(new List<(byte cA, byte cB)> { (Chan_Tagger1, Chan_Tagger2) }, ClockSyncTimeWindow, (long)TimeBin);
                _kurolator = new Kurolator(new List<CorrelationGroup> { hist }, ClockSyncTimeWindow);

                //Compensate Bobs tags for linear drift timerange
                long starttime = ttBob.time[0];
                long[] comp_times = ttBob.time.Select(t => (long)(t + (t - starttime) * LinearDriftCoefficient)).ToArray();
                TimeTags ttBob_comp = new TimeTags(ttBob.chan, comp_times);

                GlobalClockOffset = (ttAlice.time[0] - ttBob.time[0]);
                _kurolator.AddCorrelations(ttAlice, ttBob_comp, GlobalClockOffset + FiberOffset);


                //------------------------
                // Analyse peaks
                //------------------------

                bool peaksFound = false;
                bool clockInSync = false;
               
                List<Peak> peaks = hist.GetPeaks(peakBinning:2000);
                Peak MiddlePeak = peaks.Where(p => Math.Abs(p.MeanTime) == peaks.Select(a => Math.Abs(a.MeanTime)).Min()).FirstOrDefault();

                //Number of peaks plausible?
                int numExpectedPeaks = (int)(2*ClockSyncTimeWindow / ExcitationPeriod);    
                if (peaks.Count >= numExpectedPeaks && peaks.Count < numExpectedPeaks + 2)
                {
                    peaksFound = true;
                    if (_firstClockSyncIteration)
                    {
                        _init_middlepeakpos = MiddlePeak.MeanTime;
                        _firstClockSyncIteration = false;
                    }
                    else
                    {
                        //Calculate new linear drift coefficient
                        double optimum_FWHM = 3000;
                        double FWHM_P = MiddlePeak.FWHM - optimum_FWHM > 0 ? (MiddlePeak.FWHM - optimum_FWHM) : 0;
                        LinearDriftCoefficient = LinearDriftCoefficient + (PVal * Math.Sign(_init_middlepeakpos - MiddlePeak.MeanTime) * FWHM_P * 1E-12);

                        //Sync quality ok?   
                        if (peaks.Count >= numExpectedPeaks && peaks.Count < numExpectedPeaks + 2)
                        {
                            if (MiddlePeak.FWHM <= FWHM_Tolerance) clockInSync = true;
                        }
                    }
                }

                sw.Stop();

                if (MiddlePeak == null) MiddlePeak = new Peak() { }; //Define empty peak

                var alice_first = ttAlice.time[0];
                var alice_last = ttAlice.time[ttAlice.time.Length - 1];
                var bob_first = ttBob_comp.time[0];
                var bob_last = ttBob_comp.time[ttBob_comp.time.Length - 1];
                var alice_diff = alice_last - alice_first;
                var bob_diff = bob_last - bob_first;

                TimeSpan packettimespan = new TimeSpan(0, 0, 0, 0, (int)(Math.Min(alice_diff,bob_diff) * 1E-9));
                //TimeSpan packettimespan = new TimeSpan(0, 0, (int)((ttAlice.time[ttAlice.time.Length - 1] - ttAlice.time[0]) / 1E12));
                WriteLog($"Sync cycle complete in {sw.Elapsed} | TimeSpan: {packettimespan}| FWHM: {MiddlePeak.FWHM:F2} | Pos: {MiddlePeak.MeanTime:F2} | new DriftCoeff {LinearDriftCoefficient}");

                return new SyncClockResults()
                {
                    HistogramX = hist.Histogram_X,
                    HistogramY = hist.Histogram_Y,
                    CurrentLinearDriftCoeff = LinearDriftCoefficient,
                    Peaks = peaks,
                    MiddlePeak = MiddlePeak,
                    PeaksFound = peaksFound,
                    IsClocksSync = clockInSync,
                    CompTimeTags_Bob = ttBob_comp                  
                };

            });

            OnSyncClocksComplete(new SyncClocksCompleteEventArgs(syncres));

            return syncres;
        }

        public async Task<SyncCorrResults> SyncCorrelationAsync(TimeTags ttAlice, TimeTags ttBob)
        {

            SyncCorrResults syncRes = await Task<SyncCorrResults>.Run( () => 
            {
                Histogram hist = new Histogram(new List<(byte cA, byte cB)> { (Chan_Tagger1, Chan_Tagger2) }, CorrSyncTimeWindow, (long)TimeBin);
                _kurolator = new Kurolator(new List<CorrelationGroup> { hist }, CorrSyncTimeWindow);

                _kurolator.AddCorrelations(ttAlice, ttBob, GlobalClockOffset + FiberOffset);

                //Find correlated peak
                List<Peak> peaks = hist.GetPeaks(peakBinning:2000);
                double av_area = peaks.Select(p => p.Area).Average();
                double av_area_err = Math.Sqrt(av_area);

                //Find peak most outside outside the average
                Peak CorrPeak = peaks.Where(p => Math.Abs(p.Area-av_area) == peaks.Select(a => Math.Abs(a.Area-av_area)).Min()).FirstOrDefault();

                //Is peak area statistically significant?
                bool isCorrSync = false;
                bool corrPeakFound = false;

                if (Math.Abs(CorrPeak.Area - av_area) > 2*av_area_err)
                {
                    corrPeakFound = true;
                    if (Math.Abs(CorrPeak.MeanTime) < CorrPeakOffset_Tolerance) isCorrSync = true;

                    FiberOffset += CorrPeak.MeanTime;
                }

                return new SyncCorrResults()
                {
                    HistogramX = hist.Histogram_X,
                    HistogramY = hist.Histogram_Y,
                    CorrPeakPos = CorrPeak.MeanTime,
                    CorrPeakFound = corrPeakFound,
                    IsCorrSync = isCorrSync
                };
            });

            OnSyncCorrComplete(new SyncCorrCompleteEventArgs(syncRes));

            return syncRes;
        }

        private void WriteLog(string msg)
        {
            _loggerCallback?.Invoke("Sync: " + msg);
        }
    }

    public class SyncClocksCompleteEventArgs : EventArgs
    {
        public SyncClockResults SyncRes { get; private set;} 
        public SyncClocksCompleteEventArgs(SyncClockResults syncRes)
        {
            SyncRes = syncRes;
        }
    }
    
    public class SyncClockResults
    {
        public long[] HistogramX { get; set; }
        public long[] HistogramY { get; set; }
        public double CurrentLinearDriftCoeff { get; set; }
        public List<Peak> Peaks { get; set; }    
        public Peak MiddlePeak { get; set; }
        public bool PeaksFound { get; set; }
        public bool IsClocksSync { get; set; }
        public TimeTags CompTimeTags_Bob { get; set; }
    }

    public class SyncCorrCompleteEventArgs : EventArgs
    {
        public SyncCorrResults SyncRes { get; private set; }
        public SyncCorrCompleteEventArgs(SyncCorrResults syncRes)
        {
            SyncRes = syncRes;
        }
    }
    public class SyncCorrResults
    {
        public long[] HistogramX { get; set; }
        public long[] HistogramY { get; set; }
        public long CorrPeakPos { get; set; }
        public bool CorrPeakFound { get; set; }
        public bool IsCorrSync { get; set; }
    }
}
