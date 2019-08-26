using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;
using TimeTagger_Library.TimeTagger;

namespace Entanglement_Library
{
    public class QuantumKey
    {
        private Action<string> _loggercallback;
        private ITimeTagger tagger;

        public QuantumKey(Action<string> loggercallback = null)
        {
            _loggercallback = loggercallback;
        }

        public async Task TestEncryptionAsync()
        {
            tagger = new SimulatedTagger(_loggercallback)
            {
                FileName = @"C:\Users\Christian\Desktop\rawdata.txt",
                PacketSize = 2000000           
            };


            List<byte> keyAlice = new List<byte>();
            List<byte> keyBob = new List<byte>();

            Bitmap jku_logo = new Bitmap(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU.bmp");
            int necc_key_length = jku_logo.Width * jku_logo.Height;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            //MAIN TASK

            await Task.Run(() =>

            {

               while (keyAlice.Count < necc_key_length)
               {

                   while (tagger.BufferFillStatus <= 0)
                   {
                       tagger.StartCollectingTimeTagsAsync();
                       Thread.Sleep(1000);
                   }

                   tagger.StopCollectingTimeTags();

                   tagger.GetNextTimeTags(out TimeTags tt);


                    //SPLIT TAGS

                    byte[] chans = tt.chan;
                   long[] times = tt.time;

                   List<byte> chansA = new List<byte>();
                   List<byte> chansB = new List<byte>();
                   List<long> timesA = new List<long>();
                   List<long> timesB = new List<long>();

                   for (long i = 0; i < chans.Length; i++)
                   {
                       if (chans[i] >= 1 && chans[i] <= 4)
                       {
                           chansA.Add(chans[i]);
                           timesA.Add(times[i]);
                       }
                       else
                       {
                           chansB.Add(chans[i]);
                           timesB.Add(times[i]);
                       }
                   }

                   TimeTags AliceTags = new TimeTags(chansA.ToArray(), timesA.ToArray());

                    //OBSCURE BOBS TAGS

                    List<byte> chans_obsc = chansB.Select(c => c == 5 || c == 6 ? (byte)10 : (byte)11).ToList();

                   TimeTags BobTags = new TimeTags(chans_obsc.ToArray(), timesB.ToArray());


                    //Find correlations between Alice and Bob
                    List<(byte chA, byte chB)> corrConfigs = new List<(byte chA, byte chB)> { (1, 10), (2, 10), (3, 11), (4, 11) };
                   Histogram key_correlations = new Histogram(corrConfigs, 1000);

                   Kurolator kuro = new Kurolator(new List<CorrelationGroup> { key_correlations }, 1000);

                   kuro.AddCorrelations(AliceTags, BobTags, 0);

                    //Get keys from correlations


                    foreach (var key in key_correlations.Correlations)
                   {
                       byte cA = chansA[timesA.IndexOf(key.t1)];
                       keyAlice.Add(cA == 1 || cA == 3 ? (byte)0 : (byte)1);

                       byte cB = chansB[timesB.IndexOf(key.t2)];
                       keyBob.Add(cB == 5 || cB == 7 ? (byte)0 : (byte)1);
                   }
               }

           });

            stopwatch.Stop();

            File.WriteAllLines(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\Key_Alice.txt", keyAlice.Select(k => k.ToString()).ToArray());
            File.WriteAllLines(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\Key_Bob.txt", keyBob.Select(k => k.ToString()).ToArray());
        }

        public void CreateBMPs()
        {
            Bitmap jku_logo = new Bitmap(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU_encrypted.bmp");

            byte[] AliceKeys = File.ReadAllLines(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\Key_Alice.txt").Select(s => (byte)(int.Parse(s))).ToArray();
            byte[] BobKeys = File.ReadAllLines(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\Key_Bob.txt").Select(s => (byte)(int.Parse(s))).ToArray();

            Bitmap encrypted_bmp = jku_logo.QKDEncrypt(AliceKeys);
            encrypted_bmp.Save(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU_encrypted3.bmp");
            //Bitmap decrypted_bmp_alice = new Bitmap(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU_encrypted.bmp").QKDEncrypt(AliceKeys);
            //decrypted_bmp_alice.Save(@"C: \Users\Christian\Dropbox\Coding\EQKD\icons\JKU_decrypted_Alice.bmp");

            // Bitmap decrypted_bmp_bob = new Bitmap(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU_encrypted.bmp").QKDEncrypt(BobKeys);
            //Bitmap encrypted_bmp = new Bitmap(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU_encrypted2.bmp");
            //Bitmap decrypted_bmp_bob = jku_logo.QKDEncrypt(AliceKeys);
            //decrypted_bmp_bob.Save(@"C: \Users\Christian\Dropbox\Coding\EQKD\icons\JKU_decrypted_Bob.bmp");
        }

    }
}
