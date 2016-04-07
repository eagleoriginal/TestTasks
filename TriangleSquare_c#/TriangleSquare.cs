using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalcSquare
{
    public sealed class TriangleSquare
    {
        /// <summary>
        /// Calc Square of Triangle using three side
        /// </summary>
        /// <param name="a">Side A</param>
        /// <param name="b">Side B</param>
        /// <param name="c">Side C</param>
        /// <returns>Square of Rectangle</returns>
        public static double CalcTriangleSquare(double a, double b, double c)
        {
            if (a <= 0 || b <= 0 || c <= 0)
            {
                throw new ArgumentException("Sides Cannot be Zero or Less zero");
            }

            if (CheckDouble(a) || CheckDouble(b) || CheckDouble(c))
            {
                throw new ArgumentException("Sides Cannot be Nan or Infinity");
            }

            // Stuff check for triangle if Square(it is impossible to check it but...)
            double aSqr = a * a, bSqr = b * b, cSqr = c * c;

            if (!(HasMinDiff(aSqr + bSqr, cSqr, 5) || HasMinDiff(aSqr + cSqr, bSqr, 5) ||
                  HasMinDiff(bSqr + cSqr, aSqr, 5)))
            {
                throw new ArgumentException("Triangle not Rectangular");
            }

            // Heron Formula(couse it easier)
            var halfP = (a + b + c) / 2;
            var sqrSqr = halfP * (halfP - a) * (halfP - b) * (halfP - c);

            // Only Less than Zero. Because zero Square it is special case of triangle 
            if (sqrSqr < 0)
            {
                throw new ArgumentException("Side in couple not do not form a triangle");
            }

            return Math.Sqrt(sqrSqr);
        }


        private static bool CheckDouble(double arg)
        {
            return Double.IsInfinity(arg) || Double.IsNaN(arg);
        }

        // Copy Paste from MSDN.
        //https://msdn.microsoft.com/ru-ru/library/ya2zha7s(v=vs.110).aspx
        public static bool HasMinDiff(double value1, double value2, int units)
        {
            long lValue1 = BitConverter.DoubleToInt64Bits(value1);
            long lValue2 = BitConverter.DoubleToInt64Bits(value2);

            // If the signs are different, return false except for +0 and -0.
            if ((lValue1 >> 63) != (lValue2 >> 63))
            {
                if (value1 == value2)
                    return true;

                return false;
            }

            long diff = Math.Abs(lValue1 - lValue2);

            if (diff <= (long)units)
                return true;

            return false;
        }
    }
}
