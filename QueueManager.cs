using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Verhaeg.IoT.Processor
{
    /// <summary>
    /// Base class for non-generic queue manager (deprecated - use QueueManager&lt;T&gt; instead).
    /// </summary>
    [Obsolete("Use QueueManager<T> for type safety. This class will be removed in version 2.0.")]
    public abstract class QueueManager : IDisposable
    {
        // Fields
        protected ConcurrentQueue<object> cq;
        protected bool blKeepRunning;
        [Obsolete("Use _signal instead. This field will be removed in version 2.0.")]
        protected EventWaitHandle ewh;
        protected Task t;
        protected CancellationTokenSource cts;
        protected SemaphoreSlim _signal;

        // Logging
        protected ILogger Log;

        /// <summary>
        /// Gets the current number of items in the queue.
        /// </summary>
        public int QueueLength => cq?.Count ?? 0;

        /// <summary>
        /// Initializes a new instance of the QueueManager.
        /// </summary>
        [Obsolete("Use QueueManager<T> for type safety. This constructor will be removed in version 2.0.")]
        protected QueueManager(string name)
        {
            Initialize(name);
        }

        /// <summary>
        /// Initializes the queue manager with the specified name.
        /// </summary>
        protected void Initialize(string name)
        {
            // Serilog Configuration
            Log = Processor.Log.CreateLog(name);
            Log.Information("Created queue manager: " + name);

            // Thread monitoring
            cq = new ConcurrentQueue<object>();
            blKeepRunning = true;
            _signal = new SemaphoreSlim(0, int.MaxValue);
            ewh = new AutoResetEvent(false); // For backward compatibility

            // Start new read thread
            cts = new CancellationTokenSource();
            t = Task.Factory.StartNew(() => Start(), cts.Token, TaskCreationOptions.LongRunning);
        }

        private void Start()
        {
            while (blKeepRunning && !cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (cq.Count > 0)
                    {
                        Read();
                    }
                    else
                    {
                        // Use async-compatible wait
                        _signal.Wait(cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in queue processing loop");
                }
            }
            Log.Information("Queue manager stopped.");
        }

        private void Read()
        {
            if (cq.TryDequeue(out object obj))
            {
                try
                {
                    Process(obj);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing queue item");
                }
            }
        }

        /// <summary>
        /// Writes an object to the queue for processing.
        /// </summary>
        public virtual void Write(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            cq.Enqueue(obj);
            
            try
            {
                // Signal that there's work to do
                _signal.Release();
                ewh.Set(); // For backward compatibility
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error signaling queue");
            }

            // Check queue status and restart if needed
            CheckQueueStatus();
        }

        /// <summary>
        /// Checks if the processing task is still running and restarts if needed.
        /// </summary>
        protected virtual void CheckQueueStatus()
        {
            if (t == null || t.IsCompleted || t.IsFaulted)
            {
                Log.Error("QueueManager task has issues. Restarting...");
                if (t?.Exception != null)
                {
                    Log.Error(t.Exception, "Task exception");
                }

                cts = new CancellationTokenSource();
                t = Task.Factory.StartNew(() => Start(), cts.Token, TaskCreationOptions.LongRunning);
            }
        }

        /// <summary>
        /// Stops the queue manager and waits for all queued items to be processed.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for queue to empty. Default is 30 seconds.</param>
        [Obsolete("Use StopAsync() for async scenarios. This method may deadlock if called from a synchronization context.")]
        public virtual void Stop()
        {
            StopAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Stops the queue manager asynchronously and waits for all queued items to be processed.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for queue to empty.</param>
        /// <returns>A task that completes when the queue manager has stopped.</returns>
        public virtual async Task StopAsync(TimeSpan timeout)
        {
            Log.Debug("Stop command received, checking queue...");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Wait for queue to empty or timeout
            while (QueueLength > 0 && stopwatch.Elapsed < timeout)
            {
                Log.Debug("Queue not empty ({Count} items), waiting...", QueueLength);
                await Task.Delay(100, cts.Token);
            }
            
            if (QueueLength > 0)
            {
                Log.Warning("Queue still has {Count} items after timeout", QueueLength);
            }
            
            // Signal shutdown
            blKeepRunning = false;
            cts.Cancel();
            
            // Release any waiting threads
            _signal.Release();
            ewh.Set();
            
            // Wait for task to complete
            try
            {
                if (t != null)
                {
                    await t; // Wait for current processing to finish
                }
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    Log.Error(ex, "Error during queue manager shutdown");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during queue manager shutdown");
            }
            
            Log.Debug("Queue manager stopped.");
            Dispose();
        }

        /// <summary>
        /// Disposes the queue manager resources.
        /// </summary>
        protected abstract void Dispose();

        /// <summary>
        /// Processes a queued item.
        /// </summary>
        protected abstract void Process(object obj);
    }

    /// <summary>
    /// Thread-safe queue manager with generic type support.
    /// </summary>
    /// <typeparam name="T">The type of items in the queue.</typeparam>
    public abstract class QueueManager<T> : IDisposable where T : class
    {
        // Fields
        protected ConcurrentQueue<T> cq;
        protected bool blKeepRunning;
        protected Task t;
        protected CancellationTokenSource cts;
        protected SemaphoreSlim _signal;

        // Logging
        protected ILogger Log;

        // Metrics
        private int _processedCount = 0;
        private int _errorCount = 0;

        /// <summary>
        /// Gets the current number of items in the queue.
        /// </summary>
        public int QueueLength => cq?.Count ?? 0;

        /// <summary>
        /// Gets the total number of items successfully processed.
        /// </summary>
        public int ProcessedCount => _processedCount;

        /// <summary>
        /// Gets the total number of processing errors.
        /// </summary>
        public int ErrorCount => _errorCount;

        // Static singleton instances
        private static readonly ConcurrentDictionary<string, object> _instances = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Gets or creates a singleton instance of the queue manager.
        /// </summary>
        /// <typeparam name="TImpl">The concrete implementation type.</typeparam>
        /// <param name="name">The name/key for the instance.</param>
        /// <returns>The singleton instance.</returns>
        public static TImpl GetInstance<TImpl>(string name) where TImpl : QueueManager<T>, new()
        {
            return (TImpl)_instances.GetOrAdd(
                name,
                n => new TImpl { _instanceName = n }
            );
        }

        /// <summary>
        /// Gets the instance name.
        /// </summary>
        protected string _instanceName { get; private set; }

        /// <summary>
        /// Initializes a new instance of the QueueManager.
        /// </summary>
        protected QueueManager()
        {
            // Empty constructor for GetInstance factory
        }

        /// <summary>
        /// Initializes a new instance of the QueueManager with the specified name.
        /// </summary>
        protected QueueManager(string name)
        {
            _instanceName = name;
            Initialize(name);
        }

        /// <summary>
        /// Initializes the queue manager.
        /// </summary>
        protected void Initialize(string name)
        {
            _instanceName = name;
            
            // Serilog Configuration
            Log = Processor.Log.CreateLog(name);
            Log.Information("Created queue manager: " + name);

            // Thread monitoring
            cq = new ConcurrentQueue<T>();
            blKeepRunning = true;
            _signal = new SemaphoreSlim(0, int.MaxValue);

            // Start new read thread
            cts = new CancellationTokenSource();
            t = Task.Factory.StartNew(() => Start(), cts.Token, TaskCreationOptions.LongRunning);
        }

        private void Start()
        {
            while (blKeepRunning && !cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (cq.Count > 0)
                    {
                        Read();
                    }
                    else
                    {
                        // Use async-compatible wait
                        _signal.Wait(cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in queue processing loop");
                }
            }
            Log.Information("Queue manager '{Name}' stopped.", _instanceName);
        }

        private void Read()
        {
            if (cq.TryDequeue(out T item))
            {
                try
                {
                    Process(item);
                    Interlocked.Increment(ref _processedCount);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _errorCount);
                    Log.Error(ex, "Error processing queue item");
                }
            }
        }

        /// <summary>
        /// Writes an item to the queue for processing.
        /// </summary>
        public virtual void Write(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            cq.Enqueue(item);
            
            try
            {
                // Signal that there's work to do
                _signal.Release();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error signaling queue");
            }

            // Check queue status and restart if needed
            CheckQueueStatus();
        }

        /// <summary>
        /// Checks if the processing task is still running and restarts if needed.
        /// </summary>
        protected virtual void CheckQueueStatus()
        {
            if (t == null || t.IsCompleted || t.IsFaulted)
            {
                Log.Error("QueueManager task has issues. Restarting...");
                if (t?.Exception != null)
                {
                    Log.Error(t.Exception, "Task exception");
                }

                cts = new CancellationTokenSource();
                t = Task.Factory.StartNew(() => Start(), cts.Token, TaskCreationOptions.LongRunning);
            }
        }

        /// <summary>
        /// Stops the queue manager and waits for all queued items to be processed.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for queue to empty. Default is 30 seconds.</param>
        [Obsolete("Use StopAsync() for async scenarios. This method may deadlock if called from a synchronization context.")]
        public virtual void Stop()
        {
            StopAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Stops the queue manager asynchronously and waits for all queued items to be processed.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for queue to empty.</param>
        /// <returns>A task that completes when the queue manager has stopped.</returns>
        public virtual async Task StopAsync(TimeSpan timeout)
        {
            if (string.IsNullOrEmpty(_instanceName))
                _instanceName = "unnamed";
                
            Log.Debug("Stop command received for queue manager '{Name}', checking queue...", _instanceName);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Wait for queue to empty or timeout
            while (QueueLength > 0 && stopwatch.Elapsed < timeout)
            {
                Log.Debug("Queue '{Name}' not empty ({Count} items), waiting...", _instanceName, QueueLength);
                await Task.Delay(100, cts.Token);
            }
            
            if (QueueLength > 0)
            {
                Log.Warning("Queue '{Name}' still has {Count} items after timeout", _instanceName, QueueLength);
            }
            
            // Signal shutdown
            blKeepRunning = false;
            cts.Cancel();
            
            // Release any waiting threads
            _signal.Release();
            
            // Wait for task to complete
            try
            {
                if (t != null)
                {
                    await t; // Wait for current processing to finish
                }
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    Log.Error(ex, "Error during queue manager '{Name}' shutdown", _instanceName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during queue manager '{Name}' shutdown", _instanceName);
            }
            
            Log.Debug("Queue manager '{Name}' stopped.", _instanceName);
            
            // Remove from singleton instances
            if (!string.IsNullOrEmpty(_instanceName))
            {
                _instances.TryRemove(_instanceName, out _);
            }
            
            Dispose();
        }

        /// <summary>
        /// Processes a queued item.
        /// </summary>
        protected abstract void Process(T item);

        /// <summary>
        /// Disposes the queue manager resources.
        /// </summary>
        public virtual void Dispose()
        {
            Log?.Debug("Disposing queue manager '{Name}'...", _instanceName);
            
            _signal?.Dispose();
            cts?.Dispose();
            
            // Clear the queue
            while (cq.TryDequeue(out _)) { }
            cq = null;
            
            Log?.Debug("Queue manager '{Name}' disposed.", _instanceName);
        }
    }
}
