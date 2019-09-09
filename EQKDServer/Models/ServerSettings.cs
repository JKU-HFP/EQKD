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
        public ulong TimeWindow { get; set} = 100000;
        public ulong TimeBin { get; set; } = 500;
        public double PVal { get; set; } = 0;
    }
}
