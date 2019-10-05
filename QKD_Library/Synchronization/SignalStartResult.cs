using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QKD_Library.Synchronization
{
    //#################################################
    //##  E N U M E R A T O R S
    //#################################################
    public enum SignalStartStatus
    {
        NotStarted,
        InitialSignalTooHigh,
        ThresholdNotFound,
        SignalFittingFailed,
        SlopeTooLow,
        SlopeOK
    }
    public class SignalStartResult
    {
        public string ID { get; set; }
        public long[] Times { get; set; }
        public double[] Rates { get; set; }
        public double Threshold { get; set; }
        public double[] FittingTimes { get; set; }
        public double[] FittedRates { get; set; }
        public double Slope { get; set; }
        public double StartTime { get; set; }
        public long GlobalStartTime { get; set; }
        public SignalStartStatus Status { get; set; } = SignalStartStatus.NotStarted;
    }
}
