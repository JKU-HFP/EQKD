using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extensions_Library
{
    public static class ArrayExtensions
    {
        public static T[] ConcatArray<T>(this T[] inarray, T[] addarray)
        {
            var outarr = new T[inarray.Length + addarray.Length];
            inarray.CopyTo(outarr, 0);
            addarray.CopyTo(outarr, inarray.Length);
            return outarr;
        }
    }
}
