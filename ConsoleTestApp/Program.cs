using QKD_Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Key alicekey = new Key();
            Key bobkey = new Key();

            alicekey.ReadFromFile(@"I:\public\NANOSCALE SEMICONDUCTOR GROUP\1. DATA\BIG-LAB\2019\10\10\QD25\SecureKey_Alice_2Tagger_long.txt");
            bobkey.ReadFromFile(@"I:\public\NANOSCALE SEMICONDUCTOR GROUP\1. DATA\BIG-LAB\2019\10\10\QD25\SecureKey_Bob_2Tagger_long.txt");

            double qber = Key.GetQBER(alicekey.SecureKey, bobkey.SecureKey);


            while (true) ;

        }

    }
}
