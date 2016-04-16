using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ApplyTwoArray
{
    class Program
    {
        static void Main(string[] args)
        {
            var arrays = GenerateTwoIntArrays();

            foreach (int i in FastIntersect.HandMadeIntersect(arrays.Item1, arrays.Item2, Comparer<int>.Default))
            {
                Console.WriteLine(i);
            }            
        }

        public static Tuple<ICollection<int>, ICollection<int>> GenerateTwoIntArrays()
        {
            int arrayCnt = 1000;
            int range = 700;
            Random rnd = new Random(DateTime.Now.Millisecond);
            var arr1 = Enumerable.Range(0, arrayCnt).Select(i => rnd.Next(range)).ToList() as ICollection<int>;
            var arr2 = Enumerable.Range(0, arrayCnt).Select(i => rnd.Next(range)).ToList() as ICollection<int>;
            return Tuple.Create(item1: arr1, item2: arr2);
        }

       
    }
}
