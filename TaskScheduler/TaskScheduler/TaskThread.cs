using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TaskScheduler
{

    /// <summary>
    /// Inner Class Only for "Disposed Flag" in TaskManager thread function
    /// It Allow Correct wait for Action Ending and then Exit From Thread. 
    /// </summary>
    class TaskThread: IDisposable
    {
        private Thread _thread = null;

        public TaskThread()
        {
            Disposed = false;
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public bool Disposed { get; private set; }

        public void Start(ParameterizedThreadStart start)
        {
            _thread = new Thread(start);
            _thread.Start(this);
        }
    }
}
