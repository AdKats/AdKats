using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PRoConEvents
{
    public partial class AdKats
    {
        public class ThreadManager
        {
            private Logger Log;

            public ThreadManager(Logger log)
            {
                Log = log;
            }

            private readonly Dictionary<Int32, Thread> _watchdogThreads = new Dictionary<Int32, Thread>();
            private EventWaitHandle _masterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

            public Int32 Count()
            {
                return _watchdogThreads.Count();
            }

            public void Init()
            {
                _masterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            }

            public void Set()
            {
                _masterWaitHandle.Set();
            }

            public void Reset()
            {
                _masterWaitHandle.Reset();
            }

            public Boolean Wait(Int32 duration)
            {
                return _masterWaitHandle.WaitOne(duration);
            }

            public Boolean Wait(TimeSpan duration)
            {
                return _masterWaitHandle.WaitOne(duration);
            }

            public void StartWatchdog(Thread aThread)
            {
                try
                {
                    aThread.Start();
                    lock (_watchdogThreads)
                    {
                        if (!_watchdogThreads.ContainsKey(aThread.ManagedThreadId))
                        {
                            _watchdogThreads.Add(aThread.ManagedThreadId, aThread);
                            _masterWaitHandle.WaitOne(100);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error logging thread start.", e));
                }
            }

            public void StopWatchdog()
            {
                try
                {
                    lock (_watchdogThreads)
                    {
                        _watchdogThreads.Remove(Thread.CurrentThread.ManagedThreadId);
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error logging thread exit.", e));
                }
            }

            public void Prune()
            {
                try
                {
                    lock (_watchdogThreads)
                    {
                        var threads = _watchdogThreads.ToList();
                        foreach (Int32 deadThreadID in threads.Where(threadPair => threadPair.Value == null || !threadPair.Value.IsAlive)
                                                              .Select(threadPair => threadPair.Value == null ? threadPair.Key : threadPair.Value.ManagedThreadId))
                        {
                            _watchdogThreads.Remove(deadThreadID);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error pruning watchdog threads.", e));
                }
            }

            public void Monitor()
            {
                try
                {
                    lock (_watchdogThreads)
                    {
                        if (_watchdogThreads.Count() >= 20)
                        {
                            String aliveThreads = "";
                            foreach (Thread value in _watchdogThreads.Values.ToList())
                            {
                                aliveThreads = aliveThreads + (value.Name + "[" + value.ManagedThreadId + "] ");
                            }
                            Log.Warn("Thread warning: " + aliveThreads);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error monitoring watchdog thread count.", e));
                }
            }

            public Boolean IsAlive(String threadName)
            {
                try
                {
                    lock (_watchdogThreads)
                    {
                        return _watchdogThreads.Values.ToList().Any(aThread => aThread != null &&
                                                                               aThread.IsAlive &&
                                                                               aThread.Name == threadName);
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error checking for matching alive thread.", e));
                }
                return false;
            }

            public void MonitorShutdown()
            {
                try
                {
                    //Check to make sure all threads have completed and stopped
                    Int32 attempts = 0;
                    Boolean alive = false;
                    do
                    {
                        attempts++;
                        Thread.Sleep(500);
                        alive = false;
                        String aliveThreads = "";
                        lock (_watchdogThreads)
                        {
                            foreach (Int32 deadThreadID in _watchdogThreads.Values.ToList().Where(thread => !thread.IsAlive).Select(thread => thread.ManagedThreadId).ToList())
                            {
                                _watchdogThreads.Remove(deadThreadID);
                            }
                            foreach (Thread aliveThread in _watchdogThreads.Values.ToList())
                            {
                                alive = true;
                                aliveThreads += (aliveThread.Name + "[" + aliveThread.ManagedThreadId + "] ");
                            }
                        }
                        if (aliveThreads.Length > 0)
                        {
                            if (attempts > 20)
                            {
                                Log.Warn("Threads still exiting: " + aliveThreads);
                            }
                            else
                            {
                                Log.Debug(() => "Threads still exiting: " + aliveThreads, 2);
                            }
                        }
                    } while (alive);
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error while monitoring shutdown.", e));
                }
            }
        }
    }
}
