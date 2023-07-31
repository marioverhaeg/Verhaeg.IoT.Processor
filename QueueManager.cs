﻿using System;

// Extra
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Verhaeg.IoT.Processor
{
    public abstract class QueueManager
    {
        // Fields
        protected ConcurrentQueue<object> cq;
        protected bool blKeepRunning;
        protected EventWaitHandle ewh;
        protected Task t;
        protected CancellationTokenSource cts;

        // Logging
        protected ILogger Log;

        protected QueueManager(string name)
        {
            // Serilog Configuration
            Log = Processor.Log.CreateLog(name);
            Log.Debug("Created new log: " + name);

            // Thread monitoring
            cq = new ConcurrentQueue<object>();
            blKeepRunning = true;
            ewh = new AutoResetEvent(false);

            // Start new read thread
            cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            t = Task.Factory.StartNew(() => Start(), ct);
        }

        protected abstract void Dispose();

        private void Start()
        {
            while (blKeepRunning)
            {
                if (cq.Count > 0)
                {
                    // Read message from queue
                    Read();
                }
                else
                {
                    // Wait for next command
                    ewh.WaitOne();
                }
            }
            Log.Information("Stopped working. Restarting Manager...");
        }

        private void Read()
        {
            object obj;
            cq.TryDequeue(out obj);
            Process(obj);
        }

        // Add data to queue
        public void Write(object obj)
        {
            cq.Enqueue(obj);
            Log.Debug("Queuesize: " + cq.Count.ToString());

            try
            {
                // Read queue
                ewh.Set();
            }
            catch (Exception ex)
            {
                Log.Debug(ex.ToString());
            }

            // Check queue status
            if (t.IsCompleted || t.IsFaulted)
            {
                Log.Error("QueueManager has issues. Stopping QueueManager.");
                if (t.Exception != null)
                {
                    Log.Debug(t.Exception.ToString());
                }

                Log.Error("Restarting QueueManager...");
                CancellationToken ct = cts.Token;
                t = Task.Factory.StartNew(() => Start(), ct);
            }
        }

        public void Stop()
        {
            Log.Debug("Stop command received, checking queue...");
            // Wait until queue is empty
            while (cq.Count != 0)
            {
                Log.Debug("Queue not empty, waiting until queue is empty.");
                Task.Delay(2000);
            }
            cts.Cancel();
            Log.Debug("Queue empty, stopped working.");
            Dispose();
        }

        protected abstract void Process(object obj);
    }

    
}
