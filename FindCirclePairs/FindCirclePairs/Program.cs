using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace FindCirclePairs
{

    public struct TransformedCircle
    {
        public int Start;
        public int End;
        public int CircleX;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Solution sl = new Solution();
            //Console.WriteLine(sl.solution(A:new []{1, 5, 2, 1, 4, 0}));
            //Console.WriteLine(sl.solution(Enumerable.Range(12, 23).ToArray()));
            var c = Int32.MinValue;
            var c2 = 1 - Int32.MaxValue;
            Console.WriteLine(sl.solution(new []{1, 2147483647, 0}));

            
        }       
    }



    class Solution
    {

        public int solution(int[] A)
        {

            // 0. Initialize Return Intersect Pairs Count
            const int maxAllowedResultPairs = 10000000;
            int result = 0;


            if (A.Length == 0)
                return result;


            // 1. Sort By Start of Radius pos
            var orderedCircles =
                A.Select(
                    (radius, pos) => new TransformedCircle { Start = pos - radius, 
											End = (pos + radius) < pos? int.MaxValue : (pos + radius), CircleX = pos}
					)
                    .OrderBy(arg => arg.Start);
            
            // 2. This Pul We are using for Temporary collect Intersected Circles With Each Other 
            List<TransformedCircle> circlePul = new List<TransformedCircle>();


            // 3. Begin To Find Circles Which Will Be Interect with all circles in Pull.
            foreach (var nextCircle in orderedCircles)
            {
                // 3.1 Check Next. Whitch Circles in Pull Not Suitable Condition of Intersect With Each Other
                int i = 0;
                while (i < circlePul.Count)
                {
                    if (circlePul[i].End < nextCircle.Start)
                    {
                        // This Circle Must Be removed with intersecting all circles in current circlePul
                        circlePul.RemoveAt(i);
                        result += circlePul.Count;
                    }
                    else
                    {
                        i++;
                    }
                }
                // 3.2 In Result Circle Pul add new Circle. 
                circlePul.Add(nextCircle);

                //Once In The End of Loop Check If We Must Break Processing 
                if (result > maxAllowedResultPairs) return -1;
            }

            // Finally push to intresected pairs. It is Arithemical Progression
            result += (1 + (circlePul.Count - 1))*(circlePul.Count - 1)/2;
            
            

            // Also Check Last Time For exceeds maximum pairs
            return result > maxAllowedResultPairs ? -1 : result;
        }
    }
}
