using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;

namespace QKD_Library
{
    public class SyncClockResults
    {
        public bool PeaksFound { get; set; }
        public bool IsClocksSync { get; set; }
        public long[] HistogramX { get; set; }
        public long[] HistogramY { get; set; }
        public double[] HistogramYFit { get; set; }
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
        public (double val, double err) Sigma { get; set; } = (0, 0);

        public DriftCompResult(int index)
        {
            Index = index;
        }
    }

    public class SyncCorrResults
    {
        public long[] HistogramX { get; set; }
        public long[] HistogramY { get; set; }
        public long CorrPeakPos { get; set; }
        public bool CorrPeakFound { get; set; }
        public bool IsCorrSync { get; set; }
        public TimeTags CompTimeTags_Bob { get; set; }
        public TimeSpan ProcessingTime { get; set; } = new TimeSpan();
    }

}
