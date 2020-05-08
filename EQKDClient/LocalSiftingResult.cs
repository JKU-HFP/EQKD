using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQKDClient
{
    public class LocalSiftingResult
    {
        public List<byte> newAliceKeys { get; set; }= new List<byte>();
        public List<byte> newBobKeys { get; set; } = new List<byte>();

        public double QBER { get; set; }
        public double rate { get; set; }

    }
}
