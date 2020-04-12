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

        public static void EncryptAndSaveBMPMono(string filein, string fileout, string keyfile)
        {
            QKey key = new QKey();

            key.ReadFromFile(keyfile);
           
            using (Bitmap jku_logo = new Bitmap(filein))
            {
                Bitmap encrypted_bmp = jku_logo.QKDEncryptFlipped(key.SecureKey);
                encrypted_bmp.Save(fileout);
            }
        }

        public static void EncryptAndSaveBMPColor(string filein, string fileout, string keyfile, bool repeat=false)
        {
            List<byte> keylist = File.ReadAllLines(keyfile).Select(k => byte.Parse(k)).ToList(); 

            using (Bitmap bmp = new Bitmap(filein))
            {
                int num_req_keys = bmp.Height * bmp.Width * 24; //8 bit (R,G,B) = 24 bit

                while(repeat==true && keylist.Count<num_req_keys)
                {
                    keylist.AddRange(keylist);
                }

                if (num_req_keys > keylist.Count) throw new Exception($"Insufficient keys for enconding bitmap. Required:{num_req_keys}, Available:{keylist.Count}");

                Bitmap rbmp = new Bitmap(bmp);

                byte[] key_bytes = keylist.Take(num_req_keys).ToArray();

                int i = 0;

                for (int h = 0; h < rbmp.Height; h++)
                {
                    for (int w = 0; w < rbmp.Width; w++)
                    {
                        var pixel = rbmp.GetPixel(w, h);
                        int r_byte = pixel.R;
                        int g_byte = pixel.G;
                        int b_byte = pixel.B;

                        byte[] range = key_bytes.Skip(3 * i).Take(8).ToArray();
                        //int enc_byte_r = _getKeyBits(key_bytes[new Range(3 * i, 3 * i + 8)], 8);
                        int enc_byte_r = _getKeyBits(range, 8);
                        range = key_bytes.Skip((3 * i) + 8).Take(8).ToArray();
                        int enc_byte_g = _getKeyBits(range, 8);
                        key_bytes.Skip((3 * i) + 16).Take(8).ToArray();
                        int enc_byte_b = _getKeyBits(range, 8);

                        rbmp.SetPixel(w, h, Color.FromArgb(0xff, r_byte ^ enc_byte_r, g_byte ^ enc_byte_g, b_byte ^ enc_byte_b));

                        i++;
                    }
                }

                rbmp.Save(fileout);
            }
        }

        private static int _getKeyBits(byte[] keybytes, int num_bits)
        {
            int keybits = 0x0000;
            for (int bit = 0; bit < num_bits; bit++)
            {
                int tmp_byte = keybytes[bit] << bit;
                keybits |= tmp_byte;
            }

            return keybits;
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
