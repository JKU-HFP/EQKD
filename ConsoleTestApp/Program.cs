﻿using QKD_Encryption;
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

namespace ConsoleTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
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

            var controller = new PI_GCS2_Stage(s => Console.WriteLine(s));
            controller.Connect("");



            var contr = new PI_C843_Controller();
            contr.Connect("");
            var stages = contr.GetStages();
            stages[0].Move_Absolute(242.2);

            //QKey alicekey = new QKey();
            //QKey bobkey = new QKey();

            //alicekey.ReadFromFile(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\SecureKey_Alice_2Tagger_long_unbiased_twiggle.txt");
            //bobkey.ReadFromFile(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\SecureKey_Bob_2Tagger_long_unbiased_twiggle.txt");

            //double qber = QKey.GetQBER(alicekey.SecureKey, bobkey.SecureKey);

            string cmd = @"E:\Programs\Anaconda\testfile.py";
            string progargs = "argument1";

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = @"E:\Programs\Anaconda\python.exe";
            start.Arguments = string.Format("\"{0}\" \"{1}\"", cmd, progargs);
            start.UseShellExecute = false;// Do not use OS shell
            start.CreateNoWindow = true; // We don't need new window
            start.RedirectStandardOutput = true;// Any output, generated by application will be redirected back
            start.RedirectStandardError = true; // Any error in standard output will be redirected back (for example exceptions)
            using (Process process = Process.Start(start))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    string stderr = process.StandardError.ReadToEnd(); // Here are the exceptions from our Python script
                    string result = reader.ReadToEnd(); // Here is the result of StdOut(for example: print "test")
                    //return result;
                }
            }

            byte[] keys = File.ReadAllLines(@"E:\Programs\Anaconda\testkey.txt").Select(k => byte.Parse(k)).ToArray();
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
