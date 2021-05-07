using QKD_Encryption;
using QKD_Library;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stage_Library.PI;
using Controller.XYStage;

using TimeTagger_Library.TimeTagger;
using QKD_Library.Characterization;
using Stage_Library.Thorlabs;

namespace ConsoleTestApp
{
    class Program
    {
        static void Main(string[] args)
        {

            var contr = new Stage_Library.Owis.PS10Controller();
            contr.Connect("COM8");
            var stage = contr.GetStages()[0];

            Thread.Sleep(1000);

    
            stage.SetVelocity(2);

            for(int i=1; i<=5;i++)
            {
                Console.Write(stage.Position);
                Thread.Sleep(1000);
                stage.Move_Absolute(200);
                Thread.Sleep(1000);
                Console.Write(stage.Position);
                stage.Move_Absolute(150);
            }


            //MFF101Flipper flipper = new MFF101Flipper(Console.WriteLine);
            //var devs = flipper.GetDeviceList();
            //flipper.Connect("37853189");

            //while (true)
            //{
            //    Console.WriteLine($"Moving to Position 1");
            //    flipper.Move(1);
            //    Thread.Sleep(1000);
            //    Console.WriteLine($"On Position {flipper.Position}");
            //    Thread.Sleep(1000);
            //    Console.WriteLine($"Moving to Position 2");
            //    flipper.Move(2);
            //    Thread.Sleep(1000);
            //    Console.WriteLine($"On Position {flipper.Position}");
            //    Thread.Sleep(1000);
            //}


            //Instanciate TimeTaggers                        

            //HydraHarp hydra = new HydraHarp(Console.WriteLine)
            //{
            //    DiscriminatorLevel = 200,
            //    SyncDivider = 8,
            //    SyncDiscriminatorLevel = 200,
            //    MeasurementMode = HydraHarp.Mode.MODE_T3,
            //    ClockMode = HydraHarp.Clock.Internal,
            //    PackageMode = TimeTaggerBase.PMode.ByEllapsedTime
            //};
            //hydra.Connect();
            //hydra.PacketTimeSpan = (long)1E12;

            //hydra.BackupFilename = "testT3.ptu";
            //hydra.StartCollectingTimeTagsAsync();
            //Thread.Sleep(5000);
            //hydra.StopCollectingTimeTags();


            //File.WriteAllLines("stdBasis32.txt", DensityMatrix.StdBasis36.Select(a => string.Join(",",a)));

            //var filestrings = File.ReadAllLines(@"E:\Dropbox\Dropbox\Coding\Python-Scripts\JKULib\Entanglement\bases.txt");
            //List<double[]> bases = filestrings.Select(line => line.Split(' ').Select(vals => double.Parse(vals)).ToArray()).ToList();

            //ITimeTagger hydra = new HydraHarp((s) => Console.WriteLine(s))
            //{
            //    DiscriminatorLevel = 200,
            //    SyncDivider = 8,
            //    SyncDiscriminatorLevel = 200,
            //    MeasurementMode = HydraHarp.Mode.MODE_T2,
            //    ClockMode = HydraHarp.Clock.Internal,
            //    PackageMode = TimeTaggerBase.PMode.ByEllapsedTime,
            //    PacketTimeSpan=1000000000,
            //    BufferSize=10000000
            //};
            //hydra.Connect(new List<long> { 0, -3820, -31680, -31424 });

            //Task.Run(() =>
            //{ 
            //    while(true)
            //    {
            //        hydra.StartCollectingTimeTagsAsync();
            //        Thread.Sleep(2000);
            //        hydra.StopCollectingTimeTags();
            //        Thread.Sleep(1000);
            //        Console.WriteLine($"Num: {hydra.GetAllTimeTags().Count}");
            //        hydra.ClearTimeTagBuffer();
            //    }
            //});

            //while(true)
            //{
            //    Console.WriteLine("Press key to get countrate");
            //    Console.ReadKey();
            //    List<int> rate = hydra.GetCountrate();
            //    Console.WriteLine(string.Join(",",rate.Select(r => r.ToString()).ToList()));
            //}

            //int s = 0;
            //while(true)
            //{
            //    var xy = StepFunctions.AlternatingZigZagYX(s, 5);
            //    s++;
            //}

            //string filein = @"C:\Users\Christian\Dropbox\PhD\QKD\Publication\pics\corona.bmp";
            //string fileout = @"C:\Users\Christian\Dropbox\PhD\QKD\Publication\pics\corona_encr.bmp";
            //string keyfile = @"C:\Users\Christian\Dropbox\PhD\QKD\Publication\Data\QKD_2Taggers_10_10_2019\SecureKey_Bob_2Tagger_long.txt";

            //Encryption.EncryptAndSaveBMPColor(filein, fileout, keyfile, repeat:true);

            //var controller = new PI_GCS2_Stage(s => Console.WriteLine(s));
            //controller.Connect("");



            //var contr = new PI_C843_Controller();
            //contr.Connect("");
            //var stages = contr.GetStages();
            //stages[0].Move_Absolute(242.2);

            //QKey alicekey = new QKey();
            //QKey bobkey = new QKey();

            //alicekey.ReadFromFile(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\SecureKey_Alice_2Tagger_long_unbiased_twiggle.txt");
            //bobkey.ReadFromFile(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\SecureKey_Bob_2Tagger_long_unbiased_twiggle.txt");

            //double qber = QKey.GetQBER(alicekey.SecureKey, bobkey.SecureKey);

            //string cmd = @"E:\Programs\Anaconda\testfile.py";
            //string progargs = "argument1";

            //ProcessStartInfo start = new ProcessStartInfo();
            //start.FileName = @"E:\Programs\Anaconda\python.exe";
            //start.Arguments = string.Format("\"{0}\" \"{1}\"", cmd, progargs);
            //start.UseShellExecute = false;// Do not use OS shell
            //start.CreateNoWindow = true; // We don't need new window
            //start.RedirectStandardOutput = true;// Any output, generated by application will be redirected back
            //start.RedirectStandardError = true; // Any error in standard output will be redirected back (for example exceptions)
            //using (Process process = Process.Start(start))
            //{
            //    using (StreamReader reader = process.StandardOutput)
            //    {
            //        string stderr = process.StandardError.ReadToEnd(); // Here are the exceptions from our Python script
            //        string result = reader.ReadToEnd(); // Here is the result of StdOut(for example: print "test")
            //        //return result;
            //    }
            //}

            //byte[] keys = File.ReadAllLines(@"E:\Programs\Anaconda\testkey.txt").Select(k => byte.Parse(k)).ToArray();
            //double bias = QKey.GetBias(alicekey.SecureKey);

            //var filtered = QKey.RemoveBias(alicekey, bobkey);

            //double biasAfter = QKey.GetBias(filtered[0].SecureKey);
            //double biasBobAfter = QKey.GetBias(filtered[1].SecureKey);

            //var filtered = new List<QKey> { alicekey, bobkey };



            ////Testcopy
            //using (Bitmap jku_logo = new Bitmap(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\Sq.bmp"))
            //{
            //    Bitmap testcopy = jku_logo.TestCopy();
            //    testcopy.Save(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU_TestCopy.bmp");
            //}

            //Encrypt
            //using (Bitmap jku_logo = new Bitmap(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU.bmp"))
            //{
            //    Bitmap encrypted_bmp = jku_logo.QKDEncryptFlipped(filtered[0].SecureKey);
            //    encrypted_bmp.Save(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU_encrypted.bmp");
            //}

            ////Decrypt
            //using (Bitmap jku_logo = new Bitmap(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU_encrypted.bmp"))
            //{
            //    Bitmap encrypted_bmp = jku_logo.QKDEncryptFlipped(filtered[1].SecureKey);
            //    encrypted_bmp.Save(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU_decrypted.bmp");
            //}

            //using (Bitmap decryptedLogo = new Bitmap(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU_decrypted.bmp"))
            //{
            //    using (Bitmap jku_logo = new Bitmap(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU.bmp"))
            //    {
            //        double err = decryptedLogo.CompareTo(jku_logo);
            //    }
            //}


        }

    }
}
