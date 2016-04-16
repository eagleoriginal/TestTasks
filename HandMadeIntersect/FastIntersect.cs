using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplyTwoArray
{
    public class FastIntersect
    {
        /// <summary>
        /// Intersect two Collections
        /// </summary>
        /// <typeparam name="T">Type of Collections</typeparam>
        /// <param name="arrFirst">First Collection</param>
        /// <param name="arrSecond">Second Collection</param>
        /// <param name="comparer">Comparer for Type T</param>
        /// <returns>Intersected Multiple. Not Null</returns>
        public static IEnumerable<T> HandMadeIntersect<T>(ICollection<T> arrFirst, ICollection<T> arrSecond, Comparer<T> comparer) where T : struct
        {
            if ((arrFirst == null) || (arrSecond == null) || (comparer == null))
                throw new ArgumentNullException();
            
            // 1. New Arrays in Memory
            T[] arr1 = new T[arrFirst.Count];
            T[] arr2 = new T[arrSecond.Count];

            // 2. Fill It
            arrFirst.CopyTo(arr1, 0);
            arrSecond.CopyTo(arr2, 0);

            // 3. Use QuickSort(ordering) for grouping values and fast select in future
            Array.Sort(arr1);
            Array.Sort(arr2);

            // 4. Prepare Result Array
            int minArrSize = Math.Min(arrFirst.Count, arrSecond.Count);
            T[] resultArrayInts = new T[minArrSize];

            // 5. Initialize Imperative Varaibles
            int pos1 = 0;
            int pos2 = 0;
            int pos3 = 0;

            // 6. Do Select Work
            while (pos1 < arr1.Length && pos2 < arr2.Length)
            {
                if (arr1[pos1].Equals(arr2[pos2]))
                {
                    resultArrayInts[pos3++] = arr1[pos1];
                    T lastval = arr1[pos1];
                    while (++pos1 < arr1.Length && arr1[pos1].Equals(lastval)) ;
                    while (++pos2 < arr2.Length && arr2[pos2].Equals(lastval)) ;

                }
                else if (comparer.Compare(arr1[pos1], arr2[pos2]) > 0)
                {
                    while (++pos2 < arr2.Length)
                    {
                        if (comparer.Compare(arr1[pos1], arr2[pos2]) <= 0)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    while (++pos1 < arr1.Length)
                    {
                        if (comparer.Compare(arr1[pos1], arr2[pos2]) >= 0)
                        {
                            break;
                        }
                    }
                }
            }

            return resultArrayInts.Take(pos3);
        } 
    }
}
