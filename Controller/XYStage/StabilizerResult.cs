using System;
using System.Collections.Generic;
using System.Text;

namespace Controller.XYStage
{
    public class StabilizerResult
    {
        public bool Success { get; set; } = false;
        public bool MaxStepsExceeded { get; set; } = false;
    }
}
