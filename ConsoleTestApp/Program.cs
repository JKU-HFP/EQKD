using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stage.Owis;

namespace ConsoleTestApp
{
    class Program
    {
        static void Main(string[] args)
        {

            PS10Controller c = new PS10Controller(Console.WriteLine);
            c.Connect("COM6");
            Console.ReadKey();


            //Console.WriteLine("Test");
            //Console.ReadKey();

            //var tta = TimeTags.FromFile(@"E:\temp\2021-07-01-00-48-18_0075_Alice.dat");
            //var ttb = TimeTags.FromFile(@"E:\temp\2021-07-01-00-48-18_0075_Bob.dat");

            //var globaloffset = 5073219462956;

            //byte oR = SecQNet.SecQNetPackets.TimeTagPacket.RectBasisCodedChan;
            //byte oD = SecQNet.SecQNetPackets.TimeTagPacket.DiagbasisCodedChan;
            //List<(byte cA, byte cB)> _clockChanConfig = new List<(byte cA, byte cB)>
            //{
            //    //Clear Basis
            //    (0,5),(0,6),(0,7),(0,8),
            //    (1,5),(1,6),(1,7),(1,8),
            //    (2,5),(2,6),(2,7),(2,8),
            //    (3,5),(3,6),(3,7),(3,8),

            //    //Obscured Basis
            //    (0,oR),(0,oD),(1,oR),(1,oD),(2,oR),(2,oD),(3,oR),(3,oD)
            //};

            //List<(byte cA, byte cB)> aliceconfig = new List<(byte cA, byte cB)>
            //{
            //    (0,2)
            //};

            //List<(byte cA, byte cB)> bobconfig = new List<(byte cA, byte cB)>
            //{
            //    (5,7),
            //    (oR,oD)
            //};


            //ulong twindow = 100_000;

            //Histogram hist = new Histogram(_clockChanConfig, twindow);
            //Kurolator kuro = new Kurolator(new List<CorrelationGroup> { hist }, twindow);

            //kuro.AddCorrelations(tta, ttb, 0);

            //File.WriteAllLines("hist_alice_bob.dat", hist.Histogram_X.Zip(hist.Histogram_Y, (x, y) => $"{x},{y}"));
        }

    }
}
