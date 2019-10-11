using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;

namespace QKD_Library
{
    public class Key
    {
        //public
        public ulong KeyTimebin = 1000;

        //private
        List<byte> _key = new List<byte>();


        public void SaveToFile(string filename)
        {
            File.WriteAllLines(filename, _key.Select(k => k.ToString()));
        }

        public void ReadFromFile(string filename)
        {
            string[] lines = File.ReadAllLines(filename);
            _key = lines.Select(l => byte.Parse(l)).ToList();
        }

        public static KeyIndices GetKeyIndices(TimeTags ttA, TimeTags ttB, ulong keytimebin=1000)
        {
            byte bR = SecQNet.SecQNetPackets.TimeTagPacket.RectBasisCodedChan;
            byte bD = SecQNet.SecQNetPackets.TimeTagPacket.DiagbasisCodedChan;

            List<(byte cA, byte cB)> keyCorrConfig = new List<(byte cA, byte cB)>
               {
                   //Rectilinear
                   (0, bR), (1, bR),
                   //Diagonal
                   (2, bD), (3, bD)
               };

            Histogram key_hist = new Histogram(keyCorrConfig, keytimebin);
            Kurolator key_corr = new Kurolator(new List<CorrelationGroup> { key_hist }, keytimebin);

            key_corr.AddCorrelations(ttA, ttB);

            KeyIndices ki = new KeyIndices()
            {
                AliceKeyIndices = key_hist.CorrelationIndices.Select(c => c.i1).ToList(),
                BobKeyIndices = key_hist.CorrelationIndices.Select(c => c.i2).ToList()
            };


            //!!!FILTER KEY IF BIASED!!!!
            return ki;

        }

        public void GenerateKeyFromIndices(TimeTags tt, List<byte> keyIndices, bool append = true)
        {
            if (!append) _key.Clear();

            //Register key
            foreach (int i in keyIndices)
            {
                byte act_chan = tt.chan[i];
                _key.Add(act_chan == 5 || act_chan == 7 ? (byte)0 : (byte)1);
            };
        }
    }

    public struct KeyIndices
    {
        public List<int> AliceKeyIndices;
        public List<int> BobKeyIndices;
    }
}
