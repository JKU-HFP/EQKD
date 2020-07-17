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

        public static (int x, int y) Spiral(int s)
        {
            int s_temp = 0;
            int position_x = 0;
            int position_y = 0;
            int n = 0;

            while(true)
            {
                n += 1;
                int step = (int)Math.Pow(-1, n - 1) * n;
                int step_x = 1;
                int step_y = 1;

                int i = 1;
                while(i<=Math.Abs(step))
                {
                    while(s_temp<s && step_y<=Math.Abs(step))
                    {
                        position_y += Math.Sign(step);
                        s_temp += 1;
                        step_y += 1;
                    }
                    while(s_temp<s && step_x<=Math.Abs(step))
                    {
                        position_x += Math.Sign(step);
                        s_temp += 1;
                        step_x += 1;
                    }
                    i += 1;                
                }
                if (s_temp == s) break;
            }
            return (position_x, position_y);
        }
    }
}
