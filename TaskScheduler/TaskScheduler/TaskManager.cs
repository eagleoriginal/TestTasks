using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TaskScheduler
{

    public class TaskManager : ITaskManager, IDisposable
    {
        public static readonly int CMinThreadCount = 1;
        private readonly Semaphore _semaphore = new Semaphore(0, int.MaxValue);
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly Stack<TaskThread> _threads = new Stack<TaskThread>();
        private int _threadCount = CMinThreadCount; // Default Thread cnt min
        private ReaderWriterLockSlim slimLock = new ReaderWriterLockSlim();
        private bool _disposed = false;


        /// <summary>
        /// Max processing thread count for this instance of TaskManager
        /// </summary>
        public int ThreadCount
        {
            get { return _threadCount; }
            set
            {
                if (value < CMinThreadCount)
                    value = CMinThreadCount;
                _threadCount = value;
                CheckForStopExtraThreads();
            }
        }

        /// <summary>
        /// Actually Created Unmanaged threads for current TaskManager
        /// </summary>
        public int ThreadCreated{get { return _threads.Count; }}


        ~TaskManager()
        {
            Dispose();
        }

        /// <summary>
        /// After Calling this Method:
        /// All waiting Thread Will be Closed 
        /// Already called Actions Would Play to End. 
        /// After thread will close.
        /// New actions will not Add.
        /// Settings of Thread Count not produce effect
        /// </summary>
        public void Dispose()
        {
            _disposed = true;

            GC.SuppressFinalize(this);
        }

        public void Add(Action action)
        {
            if (_disposed) return;

            if (action == null)
                return;

            // Part 1 . Synchronously Push To Container new Action
            slimLock.EnterWriteLock();
            try
            {
                _queue.Enqueue(action);
            }
            finally
            {
                slimLock.ExitWriteLock();
            }

            // Part 2. Notify Threads about new Work
            try
            {
                _semaphore.Release();
            }
            catch (SemaphoreFullException e)
            {
                // Not Do Anything 
            }

            // Part 3. Extend Threads pul if it necessary
            CheckForCreateNewThread();
        }


        private void CheckForCreateNewThread()
        {
            if (_threads.Count < _threadCount && _queue.Count > 0)
            {
                var thread = new TaskThread();

                slimLock.EnterWriteLock();
                try
                {
                    if (_threads.Count < _threadCount)
                    {
                        _threads.Push(thread);
                        thread.Start(DoThreadAction);
                    }
                }
                finally
                {
                    slimLock.ExitWriteLock();
                }
            }

        }

        private void CheckForStopExtraThreads()
        {
            if (_disposed) return;
            slimLock.EnterWriteLock();
            try
            {
                while (_threads.Count > ThreadCount)
                {
                    _threads.Pop().Dispose();
                }
            }
            finally
            {
                slimLock.ExitWriteLock();
            }
        }

        private void DoThreadAction(object obj)
        {
            TaskThread taskThread = (TaskThread) obj;
            try
            {
                while (true)
                {
                    // Check Before Waiting Does Thread Disposed  or Whole Class Disposed
                    if (taskThread.Disposed || _disposed)
                        return;

                    if (_semaphore.WaitOne(100) || _queue.Count > 0 )
                    {
                        // Check After Waiting Does Thread Disposed  or Whole Class Disposed
                        if (taskThread.Disposed || _disposed)
                            return;

                        Action a = null;
                        slimLock.EnterWriteLock();
                        try
                        {
                            if (_queue.Count > 0)
                            {
                                a = _queue.Dequeue();
                            }
                        }
                        finally
                        {
                            slimLock.ExitWriteLock();
                        }
                        
                        // Not Call Action if Whole Class Disposed
                        if (_disposed) return;

                        if (a != null)
                            a();
                    }
                }
            }
            catch (ThreadInterruptedException e)
            {
                // Not Do anything
            }
        }

    }
}
