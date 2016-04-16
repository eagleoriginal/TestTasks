using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ApplyTwoArray;
namespace ApplyTwoArray.Tests
{
    [TestClass()]
    public class FastIntersect_Test
    {
        [TestMethod()]
        public void HandMadeIntersectTest_CheckSize_MustBeEqual()
        {
            var a = new int[] {1, 2, 3};
            var result = FastIntersect.HandMadeIntersect(a, a, Comparer<int>.Default);
            Assert.AreEqual(result.Count(), a.Length);
        }

        [TestMethod()]
        public void HandMadeIntersectTest_CheckSelfIntersect_MustBeEqual()
        {
            var a = new int[] { 1, 2, 3 };
            var result = FastIntersect.HandMadeIntersect(a, a, Comparer<int>.Default);

            Assert.IsTrue(a.SequenceEqual(result));
        }


        [TestMethod()]
        public void HandMadeIntersectTest_CheckDifferentArray_MustBeEmpty()
        {
            var a = new int[] { 1, 2, 3 };
            var b = new int[] { 4, 34, 5 };
            var result = FastIntersect.HandMadeIntersect(a, b, Comparer<int>.Default);

            Assert.IsFalse(result.Any());
        }

        [TestMethod()]
        public void HandMadeIntersectTest_MustBeLikeLinqIntersect()
        {
            var a = new int[] { 1, 2, 3, 3, 444, 5, 34};
            var b = new int[] { 1, 2, 38, 23, 23, 444, 5, 34 }.Reverse().ToList();
            
            var result = FastIntersect.HandMadeIntersect(a, b, Comparer<int>.Default);
            var c = a.Intersect(b).OrderBy(i => i);
            Assert.IsTrue(c.SequenceEqual(result));
        }

        [TestMethod()]
        [ExpectedException(typeof (ArgumentNullException))]
        public void HandMadeIntersectTest_CheckForNullArgument()
        {
            FastIntersect.HandMadeIntersect<int>(null, null, null);
        }
    }
}

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
        }
    }
}
