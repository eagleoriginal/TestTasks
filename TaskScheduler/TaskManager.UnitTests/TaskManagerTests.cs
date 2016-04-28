using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaskScheduler;

namespace TaskManagerUnitTest
{
    [TestFixture]
    public class TaskManagerTests
    {
        [Test]
        [TestCase(1, 100)]
        [TestCase(100, 1)]
        [TestCase(100, 10)]
        public void Add_AllAddedTasks_WillBeExecuted_(int taskCount, int threadCount)
        {
            int tskCalled = 0;
            Action a = () => { Interlocked.Increment(ref tskCalled); };

            using (var taskManager = new TaskManager())
            {
                taskManager.ThreadCount = threadCount;
                for (int i = 0; i < taskCount; i++)
                {
                    taskManager.Add(a);
                }
                WaitForAllTAsksExecuted(() => tskCalled < taskCount , 1000);

            }
            Assert.AreEqual(taskCount, tskCalled);
        }


        [Test]
        [TestCase(1, 100)]
        [TestCase(100, 1)]
        [TestCase(10, 10)]
        [TestCase(8, 10)]
        public void Add_TestFor_CountCreatedThreads_FitToMinimums(int pushTaskCnt, int threadCount)
        {
            Action a = () =>
            {
                // Imitation long Work
                Thread.Sleep(1000);
            };

            
            using (var taskManager = new TaskManager())
            {
                taskManager.ThreadCount = threadCount;
                for (int i = 0; i < pushTaskCnt; i++)
                {
                    taskManager.Add(a);
                }

                Assert.LessOrEqual(taskManager.ThreadCreated, Math.Min(pushTaskCnt, threadCount));
            }
        }


        [Test]
        [TestCase(1, 100, 3)]
        [TestCase(100, 1, 4)]
        [TestCase(1000, 10, 5)]
        [TestCase(8, 10, 5)]
        public void Add_TestFor_ParralelTaskPush_AllCalled(int pushTaskCnt, int threadCount, int pushThreadCnt)
        {
            int tskCalled = 0;
            Action a = () => { Interlocked.Increment(ref tskCalled); Thread.Sleep(1); };

            using (var helpTasks = new TaskManager())
            {
                
                
                helpTasks.ThreadCount = pushThreadCnt;
                using (var taskManagerforTest = new TaskManager())
                {
                    for (int i = 0; i < pushThreadCnt; i++)
                    {
                        helpTasks.Add(() => { for (int j = 0; j < pushTaskCnt; j++) taskManagerforTest.Add(a); });
                    }
                    
                    WaitForAllTAsksExecuted(() => tskCalled < pushTaskCnt*pushThreadCnt, 10000);
                }
            }

            Assert.AreEqual(pushTaskCnt * pushThreadCnt, tskCalled);
        }


        [Test]
        [TestCase(100, 3)]
        [TestCase(1, 4)]
        [TestCase(10, 13)]
        [TestCase(8, 1)]
        public void ThreadCount_TestFor_ThreadsCount_ChangeCorrect(int initialThreadCnt, int newThreadCnt)
        {
            
            Action a = () => { Thread.Sleep(1); };

            using (var taskManager = new TaskManager())
            {

                taskManager.ThreadCount = initialThreadCnt;

                for (int i = 0; i < 10000; i++)
                {
                    taskManager.Add(a);
                }
                
                // Wait All Threads Starts to Executing
                Thread.Sleep(100);

                // Change Threads Count
                taskManager.ThreadCount = newThreadCnt;

                // Give A time For Threads Can Ending
                Thread.Sleep(100);

                // Check For New Thread Count
                Assert.LessOrEqual(taskManager.ThreadCreated, newThreadCnt);    
            }
        }



        public void WaitForAllTAsksExecuted(Func<bool> actToWait, int timeoutMs)
        {

            TimeSpan ts = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMilliseconds(timeoutMs));
            while (actToWait())
            {
                if (ts < DateTime.Now.TimeOfDay)
                    break;

                Thread.Sleep(25);
            }
        }
    }
}
