using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;

namespace QKD_Library
{
    //#################################################
    //##  E N U M E R A T O R S
    //#################################################

    public enum StartSignalStatus
    {
        NotStarted,
        InitialSignalTooHigh,
        ThresholdNotFound,
        SignalFittingFailed,
        DerivativeFittingFailed,
        SlopeTooLow,
        SlopeOK
    }

    //#################################################
    //##  R E S U L T S
    //#################################################

    public class FindSignalStartResult
    {
        public long[] Times { get; set; }
        public double[] Rates { get; set; }
        public double Threshold { get; set; }
        public double[] Derivatives { get; set; }
        public double[] FittedRates { get; set; }
        public double[] FittedRateDervatives { get; set; }
        public long StartTime { get; set; }
        public double StartTimeFWHM { get; set; }
        public StartSignalStatus Status { get; set; } = StartSignalStatus.NotStarted;
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
        public long[] HistogramX { get; set; }
        public long[] HistogramY { get; set; }
        public List<Peak> Peaks { get; set; }
        public long CorrPeakPos { get; set; }
        public long NewFiberOffset { get; set; }
        public bool CorrPeakFound { get; set; }
        public bool IsCorrSync { get; set; }
        public TimeTags CompTimeTags_Bob { get; set; }
        public TimeSpan ProcessingTime { get; set; } = new TimeSpan();
    }

}
