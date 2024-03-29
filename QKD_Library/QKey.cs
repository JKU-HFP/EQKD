﻿using MathNet.Numerics;
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
    public class QKey
    {
        //-----------------------------------
        //----  P R O P E R T I E S
        //-----------------------------------
        public string FileName { get; set; } = "Key.txt";
        public ulong KeyTimebin { get; set; } = 1000;
        public int RectZeroChan { get; set; } = 0;
        public int DiagZeroChan { get; set; } = 2;

        public List<byte> SecureKey { get; private set; } = new List<byte>();
        public List<double> KeyRates { get; private set; } = new List<double>();

        //-----------------------------------
        //---- C O N S T R U C T O R
        //-----------------------------------
        public QKey()
        {

        }

        //--------------------------------------
        //----  M E T H O D S
        //--------------------------------------

        public void SaveToFile(string filename)
        {
            File.WriteAllLines(filename, SecureKey.Select(k => k.ToString()));
        }

        private void AppendToFile(string filename, List<byte> newKey)
        {
            File.AppendAllLines(filename, newKey.Select(k => k.ToString()));
        }

        public void ReadFromFile(string filename)
        {
            string[] lines = File.ReadAllLines(filename);
            SecureKey = lines.Select(l => byte.Parse(l)).ToList();
        }
        
        public static double GetQBER(List<byte> keyA, List<byte> keyB)
        {
            if (keyA.Count != keyB.Count) return -1;

            int num_incorrect = keyA.Zip(keyB, (kA, kB) => kA ^ kB).Where(res => res != 0).Count();
            return (double)num_incorrect / keyA.Count;
        }

        public double GetRate(TimeTags tt, List<KeyEntry> keyEntries)
        {
            double timespan = (tt.time.Last() - tt.time.First()) * 1E-12;
            double rate = keyEntries.Count / timespan;
            KeyRates.Add(rate);
            return rate;
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

        public static double GetBias(IEnumerable<byte> key)
        {
            int num_ones = key.Where(k => k == 1).Count();
            return (double)num_ones / (key.Count() - num_ones);
        }

        /// <summary>
        /// Filters timetags if Bias of 0 or 1 is persistent
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        public static List<KeyEntry> RemoveBias(List<KeyEntry> entries)
        {
            double bias = GetBias(entries.Select(ke=>ke.alice_key_value));

            //No bias
            if (bias.AlmostEqual(1.0, 1E-4)) return entries;

            double probability = Math.Abs(bias-1);

            //Bias towards 0 or 1?
            int key_to_cut = bias > 1.0 ? 1 : 0;

            Random ran = new Random();

            List<KeyEntry> resultEntries = new List<KeyEntry>();

           foreach (KeyEntry entry in entries)
            {
                double currRand = ran.NextDouble();

                if (entry.alice_key_value == key_to_cut && currRand < probability) continue;
                resultEntries.Add(entry);
            }

            return (resultEntries);
        }

        public static List<QKey> RemoveBias (QKey mainKey, QKey secondKey )
        {
            if (mainKey.SecureKey.Count != secondKey.SecureKey.Count) return null;

            double bias = GetBias(mainKey.SecureKey);

            //No bias
            if (bias.AlmostEqual(1.0, 1E-4))
            {
                return new List<QKey> { mainKey, secondKey };
            }

            QKey filteredMainKey = new QKey();
            QKey filteredSecondKey = new QKey();

            double probability = Math.Abs(bias - 1);
         
            //Bias towards 0 or 1?
            byte key_to_cut = bias > 1.0 ? (byte)1 : (byte)0;

            Random ran = new Random();
                
            for(int i=0; i<mainKey.SecureKey.Count; i++)
            {
                double currRand = ran.NextDouble();

                if (mainKey.SecureKey[i] == key_to_cut && currRand < probability) continue;
                
                filteredMainKey.SecureKey.Add(mainKey.SecureKey[i]);
                filteredSecondKey.SecureKey.Add(secondKey.SecureKey[i]);
                                
            }

            return new List<QKey> { filteredMainKey, filteredSecondKey };
        }


        /// <summary>
        /// Add Keys by timetag packet and list of indices
        /// </summary>
        /// <param name="tt"></param>
        /// <param name="keyIndices"></param>
        public void AddKey(TimeTags tt, List<int> keyIndices)
        {
            List<byte> newKey = new List<byte>();

            //Register key
            foreach (int i in keyIndices)
            {
                byte act_chan = tt.chan[i];
                newKey.Add(act_chan == RectZeroChan || act_chan == DiagZeroChan ? (byte)0 : (byte)1);
            };

            SecureKey.AddRange(newKey);
            if (!string.IsNullOrEmpty(FileName)) AppendToFile(FileName, newKey);
        }

        /// <summary>
        /// Add Keys by key entries
        /// </summary>
        /// <param name="keyentries"></param>
        public void AddKey(List<KeyEntry> keyentries)
        {
            List<byte> newKey = new List<byte>();

            newKey.AddRange(keyentries.Select(ke => ke.alice_key_value));

            SecureKey.AddRange(newKey);
            if (!string.IsNullOrEmpty(FileName)) AppendToFile(FileName, newKey);
        }

    }

    public struct KeyEntry
    {
        public int index_alice;
        public int index_bob;
        public byte alice_key_value;
    }
}
