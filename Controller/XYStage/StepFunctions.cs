using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Controller.XYStage
{
    public static class StepFunctions
    {
        public static (int x, int y) AlternatingZigZagYX(int s, int Ydist)
        {
            var range = Enumerable.Range(-Ydist, 2*Ydist+1);
            var yseq = range.Skip(Ydist).Take(Ydist + 1).Union(range.Take(Ydist).Reverse()).ToList();

            int sy = s % (2*Ydist+1); //Step in Y
            int y = yseq[sy];

            int alt= s / (2*Ydist + 1); //X Alternation Nr.               
            int xsign = alt % 2 == 0 ? 1 : -1;
            int x = alt == 0 ? 0 : xsign * (alt + 1) / 2;

            return (x, y);
        }
    }
}
