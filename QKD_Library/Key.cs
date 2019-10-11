using MathNet.Numerics;
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
        public ulong KeyTimebin { get; set; } = 1000;
        public int RectZeroChan { get; set; } = 0;
        public int DiagZeroChan { get; set; } = 2;

        //private
        List<byte> _key = new List<byte>();

        //constructor
        public Key()
        {

        }

        public void SaveToFile(string filename)
        {
            File.WriteAllLines(filename, _key.Select(k => k.ToString()));
        }

        public void ReadFromFile(string filename)
        {
            string[] lines = File.ReadAllLines(filename);
            _key = lines.Select(l => byte.Parse(l)).ToList();
        }
        
        public double GetQBER(List<byte> keyA, List<byte> keyB)
        {
            if (keyA.Count != keyB.Count) return -1;

            int num_incorrect = keyA.Zip(keyB, (kA, kB) => kA ^ kB).Where(res => res != 0).Count();
            return (double)num_incorrect / keyA.Count;
        }

        public double GetRate(TimeTags tt, List<byte> key)
        {
            double timespan = (tt.time.Last() - tt.time.First()) * 1E-12;
            return key.Count / timespan;
        }
                        
        public List<KeyEntry> GetKeyEntries(TimeTags ttAlice, TimeTags ttBob, ulong keytimebin=1000)
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

            key_corr.AddCorrelations(ttAlice, ttBob);

            //Alice Key Entries
            List<KeyEntry> keyEntries = key_hist.CorrelationIndices.Select(
                ci => new KeyEntry() {
                    index_alice = ci.i1,
                    index_bob = ci.i2,
                    alice_key_value = ttAlice.chan[ci.i1] ==  RectZeroChan|| ttAlice.chan[ci.i1] == DiagZeroChan ? (byte)0 : (byte)1 }
                ).ToList();        

            return keyEntries;
        }

        public static double GetBias(List<KeyEntry> keyEntries)
        {
            int num_ones = keyEntries.Where(ke => ke.alice_key_value == 1).Count();
            return (double)num_ones / (keyEntries.Count - num_ones);
        }

        public static List<KeyEntry> FilterKeyEntries(List<KeyEntry> entries)
        {
            double bias = GetBias(entries);

            int max_rand = 1000000; //Defines accuracy

            //No bias
            if (bias.AlmostEqual(1.0, 1.0 / max_rand)) return entries;

            double probability = Math.Abs(bias-1);
            int compval = (int)Math.Round(probability * max_rand);

            //Bias towards 0 or 1?
            int key_to_cut = bias > 1.0 ? 1 : 0;

            Random ran = new Random();

            List<KeyEntry> resultEntries = new List<KeyEntry>();

           foreach (KeyEntry entry in entries)
            {
                if (entry.alice_key_value == key_to_cut && ran.Next(max_rand) > compval)
                    resultEntries.Add(entry);
            }

            return (resultEntries);
        }

        public void AddKey(TimeTags tt, List<int> keyIndices, bool append = true)
        {
            if (!append) _key.Clear();

            //Register key
            foreach (int i in keyIndices)
            {
                byte act_chan = tt.chan[i];
                _key.Add(act_chan == RectZeroChan || act_chan == DiagZeroChan ? (byte)0 : (byte)1);
            };
        }

        public void AddKey(List<KeyEntry> keyentries)
        {
            _key.AddRange(keyentries.Select(ke => ke.alice_key_value));
        }

    }

    public struct KeyEntry
    {
        public int index_alice;
        public int index_bob;
        public byte alice_key_value;
    }
}
