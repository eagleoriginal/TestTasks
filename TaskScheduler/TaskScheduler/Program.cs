using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TaskScheduler
{
    class Program
    {
        static int ActNumber = 0;

        static void  LongAction()
        {
            int i = 0;
            while (i < 1000000)
            {

                i++;
            }
            Console.WriteLine("Stoped:" + (ActNumber++).ToString());
        }

        static void Main(string[] args)
        {
            // Sample Usage of TaskManager
            using (TaskManager newManager = new TaskManager {ThreadCount = 10})
            {

                // Add n Seconds New tasks
                TimeSpan ts = DateTime.Now.TimeOfDay.Add(TimeSpan.FromSeconds(1));
                while (ts > DateTime.Now.TimeOfDay)
                {
                    for (int i = 0; i < 100; i++)
                    {
                        newManager.Add(LongAction);
                    }

                }

                // Not waiting All Actions Called. 
                // Dispose Threads. 
            }
        }

      
    }
}
