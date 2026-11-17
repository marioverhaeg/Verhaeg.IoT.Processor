using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Verhaeg.IoT.Processor
{
    /// <summary>
    /// Base class for non-generic task manager (deprecated - use TaskManager&lt;T&gt; instead).
    /// </summary>
    [Obsolete("Use TaskManager<T> for improved functionality. This class will be removed in version 2.0.")]
    public abstract class TaskManager : IDisposable
    {
        // Fields
        protected bool blKeepRunning;
        [Obsolete("Use _signal instead. This field will be removed in version 2.0.")]
        protected EventWaitHandle ewh;
        protected Task t;
        protected CancellationTokenSource cts;
        protected CancellationToken ct;
        protected SemaphoreSlim _signal;

        // Logging
        protected ILogger Log;

        /// <summary>
        /// Initializes a new instance of the TaskManager.
        /// </summary>
        [Obsolete("Use TaskManager<T> for improved functionality. This constructor will be removed in version 2.0.")]
        protected TaskManager(string name)
        {
            Initialize(name);
        }

        /// <summary>
        /// Initializes the task manager.
        /// </summary>
        protected void Initialize(string name)
        {
            // Serilog Configuration
            Log = Processor.Log.CreateLog(name);
            Log.Debug("Created task manager: " + name);

            blKeepRunning = true;
            _signal = new SemaphoreSlim(0, int.MaxValue);
            ewh = new AutoResetEvent(false); // For backward compatibility
            
            // Start new read thread
            cts = new CancellationTokenSource();
            ct = cts.Token;
            t = Task.Factory.StartNew(() => RunProcess(), ct, TaskCreationOptions.LongRunning);
        }

        private void RunProcess()
        {
            try
            {
                Process();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in task processing");
            }
        }

        /// <summary>
        /// Signals the task to wake up.
        /// </summary>
        public virtual void Signal()
        {
            _signal.Release();
            ewh.Set(); // For backward compatibility
        }

        /// <summary>
        /// Stops the task manager.
        /// </summary>
        [Obsolete("Use StopAsync() for async scenarios. This method may not properly await completion.")]
        public virtual void Stop()
        {
            Log.Debug("Stop command received...");
            blKeepRunning = false;
            cts.Cancel();
            _signal.Release();
            ewh.Set();
            
            try
            {
                t?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error waiting for task to stop");
            }
            
            Log.Debug("Stopped working.");
        }

        /// <summary>
        /// Stops the task manager asynchronously.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for graceful shutdown.</param>
        /// <returns>A task that completes when the task manager has stopped.</returns>
        public virtual async Task StopAsync(TimeSpan timeout)
        {
            Log.Debug("Stop command received...");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            blKeepRunning = false;
            cts.Cancel();
            _signal.Release();
            ewh.Set();
            
            // Wait for task to complete
            try
            {
                if (t != null)
                {
                    var remainingTime = timeout - stopwatch.Elapsed;
                    if (remainingTime > TimeSpan.Zero)
                    {
                        using var ctsTimeout = new CancellationTokenSource(remainingTime);
                        await t; // Wait for task to complete
                    }
                    else
                    {
                        // Timeout already exceeded
                        Log.Warning("Timeout expired before task could complete");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during task manager shutdown");
            }
            
            Log.Debug("Stopped working.");
        }

        /// <summary>
        /// Disposes the task manager resources.
        /// </summary>
        public virtual void Dispose()
        {
            Log.Debug("Dispose...");
            Stop();
            
            _signal?.Dispose();
            cts?.Dispose();
            ewh?.Dispose();
        }

        /// <summary>
        /// The main processing method to be implemented by derived classes.
        /// </summary>
        protected abstract void Process();
    }

    /// <summary>
    /// Task manager with improved functionality and generic support.
    /// </summary>
    public abstract class TaskManager<T> : IDisposable where T : class
    {
        // Fields
        protected bool blKeepRunning;
        protected Task t;
        protected CancellationTokenSource cts;
        protected CancellationToken ct;
        protected SemaphoreSlim _signal;

        // Logging
        protected ILogger Log;

        // Metrics
        private int _iterationCount = 0;

        /// <summary>
        /// Gets the total number of iterations completed.
        /// </summary>
        public int IterationCount => _iterationCount;

        // Static singleton instances
        private static readonly ConcurrentDictionary<string, object> _instances = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Gets or creates a singleton instance of the task manager.
        /// </summary>
        /// <typeparam name="TImpl">The concrete implementation type.</typeparam>
        /// <param name="name">The name/key for the instance.</param>
        /// <returns>The singleton instance.</returns>
        public static TImpl GetInstance<TImpl>(string name) where TImpl : TaskManager<T>, new()
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
        /// Initializes a new instance of the TaskManager.
        /// </summary>
        protected TaskManager()
        {
            // Empty constructor for GetInstance factory
        }

        /// <summary>
        /// Initializes a new instance of the TaskManager with the specified name.
        /// </summary>
        protected TaskManager(string name)
        {
            _instanceName = name;
            Initialize(name);
        }

        /// <summary>
        /// Initializes the task manager.
        /// </summary>
        protected void Initialize(string name)
        {
            _instanceName = name;
            
            // Serilog Configuration
            Log = Processor.Log.CreateLog(name);
            Log.Debug("Created task manager: " + name);

            blKeepRunning = true;
            _signal = new SemaphoreSlim(0, int.MaxValue);
            
            // Start new read thread
            cts = new CancellationTokenSource();
            ct = cts.Token;
            t = Task.Factory.StartNew(() => RunProcess(), ct, TaskCreationOptions.LongRunning);
        }

        private void RunProcess()
        {
            while (blKeepRunning && !ct.IsCancellationRequested)
            {
                try
                {
                    Process();
                    Interlocked.Increment(ref _iterationCount);
                    
                    // Wait for signal
                    _signal.Wait(ct);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in task processing loop");
                }
            }
            Log.Information("Task manager '{Name}' stopped.", _instanceName);
        }

        /// <summary>
        /// Signals the task to wake up.
        /// </summary>
        public virtual void Signal()
        {
            _signal.Release();
        }

        /// <summary>
        /// Stops the task manager.
        /// </summary>
        [Obsolete("Use StopAsync() for async scenarios. This method may deadlock if called from a synchronization context.")]
        public virtual void Stop()
        {
            StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Stops the task manager asynchronously.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for graceful shutdown.</param>
        /// <returns>A task that completes when the task manager has stopped.</returns>
        public virtual async Task StopAsync(TimeSpan timeout)
        {
            if (string.IsNullOrEmpty(_instanceName))
                _instanceName = "unnamed";
                
            Log.Debug("Stop command received for task manager '{Name}'...", _instanceName);
            
            blKeepRunning = false;
            cts.Cancel();
            _signal.Release();
            
            // Wait for task to complete
            try
            {
                if (t != null)
                {
                    using var timeoutCts = new CancellationTokenSource(timeout);
                    var combinedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                    
                    await t;
                }
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Task manager '{Name}' stop timed out", _instanceName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during task manager '{Name}' shutdown", _instanceName);
            }
            
            // Remove from singleton instances
            if (!string.IsNullOrEmpty(_instanceName))
            {
                _instances.TryRemove(_instanceName, out _);
            }
            
            Log.Debug("Task manager '{Name}' stopped.", _instanceName);
        }

        /// <summary>
        /// Disposes the task manager resources.
        /// </summary>
        public virtual void Dispose()
        {
            if (string.IsNullOrEmpty(_instanceName))
                _instanceName = "unnamed";
                
            Log.Debug("Disposing task manager '{Name}'...", _instanceName);
            
            _signal?.Dispose();
            cts?.Dispose();
            
            Log.Debug("Task manager '{Name}' disposed.", _instanceName);
        }

        /// <summary>
        /// The main processing method to be implemented by derived classes.
        /// This method is called repeatedly until the task manager is stopped.
        /// Use Signal() to wake up the task, or implement a polling loop.
        /// </summary>
        protected abstract void Process();
    }
}
