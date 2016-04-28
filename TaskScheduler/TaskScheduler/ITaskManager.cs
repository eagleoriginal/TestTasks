using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace TaskScheduler
{
    public interface ITaskManager
    {
        /// <summary>
        /// Push action for executing asynchronously
        /// </summary>
        /// <param name="action">delegate to execute asynchronously</param>
        void Add(System.Action action);
    }

}
