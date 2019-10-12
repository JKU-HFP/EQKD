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

            alicekey.ReadFromFile(@"C:\Users\Christian\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\SecureKey_Alice_2Tagger_long.txt");
            bobkey.ReadFromFile(@"C:\Users\Christian\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\SecureKey_Bob_2Tagger_long.txt");

            double qber = QKey.GetQBER(alicekey.SecureKey, bobkey.SecureKey);
            double bias = QKey.GetBias(alicekey.SecureKey);

            var filtered = QKey.RemoveBias(alicekey, bobkey);

            double biasAfter = QKey.GetBias(filtered[0].SecureKey);
            double biasBobAfter = QKey.GetBias(filtered[1].SecureKey);

            //Enrypt
            using (Bitmap jku_logo = new Bitmap(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU.bmp"))
            {
                Bitmap encrypted_bmp = jku_logo.QKDEncrypt(filtered[0].SecureKey);
                encrypted_bmp.Save(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU_encrypted.bmp");
            }

            //Enrypt
            using (Bitmap jku_logo = new Bitmap(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU_encrypted.bmp"))
            {
                Bitmap encrypted_bmp = jku_logo.QKDEncrypt(filtered[1].SecureKey);
                encrypted_bmp.Save(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU_decrypted.bmp");
            }

            using(Bitmap decryptedLogo = new Bitmap(@"C:\Users\Christian\Dropbox\PhD\QKD\Encryption_Test_25_08_2019\JKU_decrypted.bmp"))
            {
                using (Bitmap jku_logo = new Bitmap(@"C:\Users\Christian\Dropbox\Coding\EQKD\icons\JKU.bmp"))
                {
                    double err = decryptedLogo.CompareTo(jku_logo);
                }
            }
    

        }

    }
}
