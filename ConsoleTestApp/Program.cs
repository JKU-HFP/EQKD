using Entanglement_Library;
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
            encrypt();

            ;
        }

        public static async void encrypt()
        {
            QuantumKey k = new QuantumKey(null);
            //await k.TestEncryptionAsync();
            k.CreateBMPs();
        }
    }
}
