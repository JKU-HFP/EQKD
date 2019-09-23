using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extensions_Library
{
    public static class EnumeratorExtensions
    {
        public static T[] ConcatArray<T>(this T[] inarray, T[] addarray)
        {
            var outarr = new T[inarray.Length + addarray.Length];
            inarray.CopyTo(outarr, 0);
            addarray.CopyTo(outarr, inarray.Length);
            return outarr;
        }
        
        public static IEnumerable<int> GetIndicesOf<T>(this IEnumerable<T> inList, IEnumerable<T> filterlist)
        {
            T[] inListArr = inList.ToArray();
            T[] filterlistArr = filterlist.ToArray();

            for(int i=0; i<inListArr.Count(); i++)
            {
                for(int j=0; j<filterlistArr.Count(); j++)
                {
                    if (inListArr[i].Equals(filterlistArr[j])) yield return i;
                }
            }
        }

    }
}
