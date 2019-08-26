using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entanglement_Library
{
    static class BitmapEncryption
    {
        private static int white_arbg = Color.White.ToArgb();
        private static int black_arbg = Color.Black.ToArgb();

        public static Bitmap QKDEncrypt (this Bitmap orig_bmp, byte[] key, Action<string> loggercallback = null)
        {
            Bitmap encoded_bmp = new Bitmap(orig_bmp.Width, orig_bmp.Height);

            if (key.Length < orig_bmp.Width * orig_bmp.Height) loggercallback?.Invoke("Key too short to encrypt bitmap");

            //ENCODE / DECODE

            int index = 0;

            int err = 0;

            for (int x = 0; x < orig_bmp.Width; x++)
            {
                for (int y = 0; y < orig_bmp.Height; y++)
                {
                    Color c = orig_bmp.GetPixel(x, y);

                    if(!c.ToArgb().Equals(white_arbg) && !!c.ToArgb().Equals(black_arbg))
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
