using QKD_Encryption;
using QKD_Library;
using System;
using System.Collections.Generic;
using System.Drawing;
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
            QKey alicekey = new QKey();
            QKey bobkey = new QKey();

            alicekey.ReadFromFile(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\SecureKey_Alice_2Tagger_long_unbiased_twiggle.txt");
            bobkey.ReadFromFile(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\SecureKey_Bob_2Tagger_long_unbiased_twiggle.txt");

            double qber = QKey.GetQBER(alicekey.SecureKey, bobkey.SecureKey);
            //double bias = QKey.GetBias(alicekey.SecureKey);

            //var filtered = QKey.RemoveBias(alicekey, bobkey);

            //double biasAfter = QKey.GetBias(filtered[0].SecureKey);
            //double biasBobAfter = QKey.GetBias(filtered[1].SecureKey);

            var filtered = new List<QKey> { alicekey, bobkey };



            ////Testcopy
            //using (Bitmap jku_logo = new Bitmap(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\Sq.bmp"))
            //{
            //    Bitmap testcopy = jku_logo.TestCopy();
            //    testcopy.Save(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU_TestCopy.bmp");
            //}

            //Encrypt
            using (Bitmap jku_logo = new Bitmap(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU.bmp"))
            {
                Bitmap encrypted_bmp = jku_logo.QKDEncryptFlipped(filtered[0].SecureKey);
                encrypted_bmp.Save(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU_encrypted.bmp");
            }

            //Decrypt
            using (Bitmap jku_logo = new Bitmap(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU_encrypted.bmp"))
            {
                Bitmap encrypted_bmp = jku_logo.QKDEncryptFlipped(filtered[1].SecureKey);
                encrypted_bmp.Save(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU_decrypted.bmp");
            }

            using (Bitmap decryptedLogo = new Bitmap(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU_decrypted.bmp"))
            {
                using (Bitmap jku_logo = new Bitmap(@"E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\JKU.bmp"))
                {
                    double err = decryptedLogo.CompareTo(jku_logo);
                }
            }


        }

    }
}
