using QKD_Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            QuantumKey k = new QuantumKey(null);
            //await k.TestEncryptionAsync();
            k.CreateBMPs();
        }

    }
}
