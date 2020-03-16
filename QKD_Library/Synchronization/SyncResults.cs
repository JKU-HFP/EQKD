using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;

namespace QKD_Library.Synchronization
{
    //#################################################
    //##  E N U M E R A T O R
    //#################################################

    public enum CorrSyncStatus
    {
        SearchingCoarseRange,
        NoCorrelationFound,
        SearchingCorrPeak,
        TrackingPeak,
    }

    //#################################################
    //##  R E S U L T S
    //#################################################
    public class TaggerSyncResults
    {
        public bool IsSync { get; set; }
        public TimeTags TimeTags_Alice { get; set; }
        public TimeTags TimeTags_Bob { get; set; }
        public TimeTags CompTimeTags_Bob { get; set; }
    }


    public class SyncClockResult
    {
        public bool PeaksFound { get; set; }
        public bool IsClocksSync { get; set; }
        public long[] HistogramX { get; set; }
        public long[] HistogramY { get; set; }
        public double[] HistogramYFit { get; set; }
        public double NumIterations { get; set; }
        public (double val, double max) GroundLevel { get; set; }
        public double NewLinearDriftCoeff { get; set; }
        public (double val, double err) Sigma { get; set; } = (0, 0);
        public List<Peak> Peaks { get; set; }
        public Peak MiddlePeak { get; set; }
        public TimeTags TimeTags_Alice { get; set; }
        public TimeTags TimeTags_Bob { get; set; }
        public TimeTags CompTimeTags_Bob { get; set; }
        public TimeSpan ProcessingTime { get; set; } = new TimeSpan();
    }

    public class DriftCompResult
    {
        public int Index { get; private set; }
        public double LinearDriftCoeff { get; set; } = 0;
        public bool IsFitSuccessful { get; set; } = false;
        public long[] HistogramX { get; set; } = null;
        public long[] HistogramY { get; set; } = null;
        public List<Peak> Peaks { get; set; }
        public Peak MiddlePeak { get; set; }
        public double[] HistogramYFit { get; set; } = null;
        public double FittedMeanTime { get; set; }
        public double NumIterations { get; set; }
        public double GroundLevel { get; set; }
        public (double val, double err) Sigma { get; set; } = (0, 0);

        public DriftCompResult(int index)
        {
            Index = index;
        }
    }

    public class SyncCorrResults
    {
        public CorrSyncStatus Status { get; set; } = CorrSyncStatus.SearchingCoarseRange;
        public List<long> HistogramX { get; set; }
        public List<long> HistogramY { get; set; }
        public List<Peak> Peaks { get; set; }
        public long CorrPeakPos { get; set; }
        public bool IsCorrSync { get; set; }
    }

}
