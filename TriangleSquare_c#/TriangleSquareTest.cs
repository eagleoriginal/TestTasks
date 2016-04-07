using CalcSquare;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CalcSquareTest
{
    
    
    /// <summary>
    ///Это класс теста для TriangleSquareTest, в котором должны
    ///находиться все модульные тесты TriangleSquareTest
    ///</summary>
    [TestClass()]
    public class TriangleSquareTest
    {


        private TestContext testContextInstance;

        /// <summary>
        ///Получает или устанавливает контекст теста, в котором предоставляются
        ///сведения о текущем тестовом запуске и обеспечивается его функциональность.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Дополнительные атрибуты теста
        // 
        //При написании тестов можно использовать следующие дополнительные атрибуты:
        //
        //ClassInitialize используется для выполнения кода до запуска первого теста в классе
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //ClassCleanup используется для выполнения кода после завершения работы всех тестов в классе
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //TestInitialize используется для выполнения кода перед запуском каждого теста
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //TestCleanup используется для выполнения кода после завершения каждого теста
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion


    
        /// <summary>
        ///Тест для CalcTriangleSquare
        ///</summary>
        [TestMethod()]
        public void CalcTriangleSquareTest()
        {
            double a = 1D;  
            double b = 1D;  
            double c = Math.Sqrt(2D);  
            double expected = 0.5D;  
            double actual;
            actual = TriangleSquare.CalcTriangleSquare(a, b, c);
            Assert.IsTrue(TriangleSquare.HasMinDiff(expected, actual, 10),
                String.Format("Ошибка Ожидается: <{0}>. Фактически: <{1}>.", expected, actual));

        }

        [TestMethod()]
        [ExpectedException(typeof(System.ArgumentException))]
        public void CalcTriangleSquareTestNotTriangleParams()
        {
            double a = 1D;  
            double b = 2D;  
            double c = Math.Sqrt(2D);  
            double expected = 0.5D;  
            double actual;
            actual = TriangleSquare.CalcTriangleSquare(a, b, c);
            Assert.IsTrue(TriangleSquare.HasMinDiff(expected, actual, 10),
                String.Format("Ошибка Ожидается: <{0}>. Фактически: <{1}>.", expected, actual));

        }

        [TestMethod()]
        [ExpectedException(typeof(System.ArgumentException))]
        public void CalcTriangleSquareTestNanParams()
        {
            double a = double.NaN; 
            double b = 1D; 
            double c = Math.Sqrt(2D);  
            double expected = 0.5D;  
            double actual;
            actual = TriangleSquare.CalcTriangleSquare(a, b, c);
        }

        [TestMethod()]
        [ExpectedException(typeof(System.ArgumentException))]
        public void CalcTriangleSquareTestLessZeroParams()
        {
            double a = -10;
            double b = 1;
            double c = Math.Sqrt(2D);
            double expected = 0.5D;
            double actual;
            actual = TriangleSquare.CalcTriangleSquare(a, b, c);
        }

        [TestMethod]
        public void HasMinDiffTest()
        {
            double expected = 23/100000D;
            double actual = 0.00023D;
            Assert.IsTrue(TriangleSquare.HasMinDiff(expected, actual, 1), String.Format("Ошибка Ожидается: <{0}>. Фактически: <{1}>.", expected, actual));
            
            expected = -0D;
            actual = +0D;
            Assert.IsTrue(TriangleSquare.HasMinDiff(expected, actual, 1), String.Format("Ошибка Ожидается: <{0}>. Фактически: <{1}>.", expected, actual));

            expected = -10D;
            actual = +10D;
            Assert.IsFalse(TriangleSquare.HasMinDiff(expected, actual, 1), String.Format("Ошибка Ожидается: <{0}>. Фактически: <{1}>.", expected, actual));
        }
    }
}
