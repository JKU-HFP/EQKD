using QKD_Library;
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

namespace QKD_Encryption
{
    public static class Encryption
    {

        private static int white_arbg = Color.White.ToArgb();
        private static int black_arbg = Color.Black.ToArgb();

        public static void EncryptAndSaveBMP(string filein, string fileout, string keyfile)
        {
            QKey key = new QKey();

            key.ReadFromFile(keyfile);
           
            using (Bitmap jku_logo = new Bitmap(filein))
            {
                Bitmap encrypted_bmp = jku_logo.QKDEncryptFlipped(key.SecureKey);
                encrypted_bmp.Save(fileout);
            }
        }

        public static Bitmap TestCopy(this Bitmap orig_bmp)
        {
            Bitmap encoded_bmp = new Bitmap(orig_bmp.Width, orig_bmp.Height);
                    
            int index = 0;

            int err = 0;

            for (int x = 0; x < orig_bmp.Width; x++)
            {
                for (int y = 0; y < orig_bmp.Height; y++)
                {
                    Color c = orig_bmp.GetPixel(x, y);

                    if (!c.ToArgb().Equals(white_arbg) && !c.ToArgb().Equals(black_arbg))
                    {
                        err++;
                    }

                    byte colorbyte = c.ToArgb().Equals(white_arbg) ? (byte)0 : (byte)1;
                    byte outbyte = (byte)(colorbyte);
                    Color outcolor = outbyte == 0 ? Color.White : Color.Black;

                    encoded_bmp.SetPixel(x, y, outcolor);

                    index++;
                }
            }

            return encoded_bmp;
        }

        public static double CompareTo(this Bitmap orig_bmp, Bitmap comparedBitmap)
        {
            if (orig_bmp.Height != comparedBitmap.Height || orig_bmp.Width != comparedBitmap.Width) return -1;

            int err = 0;

            for (int x = 0; x < orig_bmp.Width; x++)
            {
                for (int y = 0; y < orig_bmp.Height; y++)
                {
                    Color orig_color = orig_bmp.GetPixel(x, y);
                    Color comp_color = comparedBitmap.GetPixel(x, y);

                    if (orig_color!=comp_color) err++;
                }
            }
            return (double)err / (orig_bmp.Height * orig_bmp.Width);
        }

        public static Bitmap QKDEncrypt(this Bitmap orig_bmp, List<byte> key, Action<string> loggercallback = null)
        {
            Bitmap encoded_bmp = new Bitmap(orig_bmp.Width, orig_bmp.Height);

            if (key.Count < orig_bmp.Width * orig_bmp.Height) loggercallback?.Invoke("Key too short to encrypt bitmap");

            //ENCODE / DECODE

            int index = 0;

            int err = 0;

            for (int x = 0; x < orig_bmp.Width; x++)
            {
                for (int y = 0; y < orig_bmp.Height; y++)
                {
                    Color c = orig_bmp.GetPixel(x, y);

                    if (!c.ToArgb().Equals(white_arbg) && !c.ToArgb().Equals(black_arbg))
                    {
                        err++;
                    }

                    byte colorbyte = c.ToArgb().Equals(white_arbg) ? (byte)0 : (byte)1;
                    byte outbyte = (byte)(colorbyte ^ key[index]);
                    Color outcolor = outbyte == 0 ? Color.White : Color.Black;

                    encoded_bmp.SetPixel(x, y, outcolor);

                    index++;
                }
            }

            return encoded_bmp;
        }

        public static Bitmap QKDEncryptFlipped(this Bitmap orig_bmp, List<byte> key, Action<string> loggercallback = null)
        {
            Bitmap encoded_bmp = new Bitmap(orig_bmp.Width, orig_bmp.Height);

            if (key.Count < orig_bmp.Width * orig_bmp.Height) loggercallback?.Invoke("Key too short to encrypt bitmap");

            //ENCODE / DECODE

            int index = 0;

            int err = 0;

            for (int y = 0; y < orig_bmp.Height; y++)
            {
                for (int x = 0; x < orig_bmp.Width; x++)
                {
                    Color c = orig_bmp.GetPixel(x, y);

                    if (!c.ToArgb().Equals(white_arbg) && !c.ToArgb().Equals(black_arbg))
                    {
                        err++;
                    }

                    byte colorbyte = c.ToArgb().Equals(white_arbg) ? (byte)0 : (byte)1;
                    byte outbyte = (byte)(colorbyte ^ key[index]);
                    Color outcolor = outbyte == 0 ? Color.White : Color.Black;

                    encoded_bmp.SetPixel(x, y, outcolor);

                    index++;
                }
            }

            return encoded_bmp;
        }

    }
}
