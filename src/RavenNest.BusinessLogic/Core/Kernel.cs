using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RavenNest.BusinessLogic
{
    public interface ITimeoutHandle { }
    public interface IKernel
    {
        ITimeoutHandle SetTimeout(Action action, int timeoutMilliseconds);
        void ClearTimeout(ITimeoutHandle discordBroadcast);

        void Start();
        void Stop();
        bool Started { get; }
    }

    public class Kernel : IKernel
    {
        private readonly object mutex = new object();
        private readonly object timeoutMutex = new object();
        private readonly List<TimeoutCallbackHandle> timeouts = new List<TimeoutCallbackHandle>();

        private Thread kernelThread;
        private bool started;

        public ITimeoutHandle SetTimeout(Action action, int timeoutMilliseconds)
        {
            this.Start(); // ensure kernel is started if it isnt.
            lock (timeoutMutex)
            {
                var timeout = new TimeoutCallbackHandle(action, timeoutMilliseconds);
                this.timeouts.Add(timeout);
                return timeout;
            }
        }

        public void ClearTimeout(ITimeoutHandle handle)
        {
            lock (timeoutMutex)
            {
                this.timeouts.Remove((TimeoutCallbackHandle)handle);
            }
        }

        public void Start()
        {
            lock (mutex)
            {
                if (this.started)
                {
                    return;
                }

                this.started = true;
                this.kernelThread = new Thread(KernelProcess)
                {
                    IsBackground = true
                };
                this.kernelThread.Start();
            }
        }

        public void Stop()
        {
            lock (mutex)
            {
                this.started = false;
                this.kernelThread.Join();
            }
        }

        public bool Started => started;

        private void KernelProcess()
        {
            do
            {
                lock (mutex)
                {
                    if (!this.started)
                    {
                        return;
                    }
                }
                try
                {
                    // Take the due timeout under the lock, run it outside. It used to run inside, so SetTimeout waited
                    // for whatever the action did: the database save is one, and every GameData.Add during a save
                    // schedules the next one, so a level up (a new skill record) stalled its caller for the whole
                    // database write, seconds at a time under load.
                    TimeoutCallbackHandle due = null;
                    lock (timeoutMutex)
                    {
                        var item = this.timeouts.OrderBy(x => x.Timeout).FirstOrDefault();
                        if (item != null && DateTime.Now >= item.Timeout)
                        {
                            this.timeouts.Remove(item);
                            due = item;
                        }
                    }

                    if (due != null)
                    {
                        due.Action?.Invoke();
                        continue;
                    }
                }
                catch
                {
                    // ignored, we can't have the kernel die due to an exception
                }
                System.Threading.Thread.Sleep(250);
            } while (true);
        }

        private class TimeoutCallbackHandle : ITimeoutHandle
        {
            public TimeoutCallbackHandle(Action action, int timeout)
            {
                this.Registered = DateTime.Now;
                this.Timeout = this.Registered.AddMilliseconds(timeout);
                this.Action = action;
            }

            public DateTime Registered { get; }
            public DateTime Timeout { get; }
            public Action Action { get; }
            public Guid Id { get; } = Guid.NewGuid();
        }
    }
}
