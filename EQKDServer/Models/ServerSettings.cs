using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQKDServer.Models
{
    public class ServerSettings
    {
        public int PacketSize { get; set; } = 100000;

        //-----------------------
        // Clock Synchronization
        //-----------------------
        public double LinearDriftCoefficient { get; set; } = 0.0;
        public double LinearDriftCoeff_Var { get; set; } = 1E-11;
        public int LinearDriftCoeff_NumVar { get; set; } = 0;
        public ulong TimeWindow { get; set; } = 100000;
        public ulong TimeBin { get; set; } = 256;


        public long FiberOffset { get; set; } = 0;

    }
}
