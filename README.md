# Verhaeg.IoT.Processor

A .NET library providing reusable components for IoT applications: **thread-safe logging**, **queue management**, and **background task management**.

[![NuGet Version](https://img.shields.io/nuget/v/Verhaeg.IoT.Processor)](https://www.nuget.org/packages/Verhaeg.IoT.Processor/)

## Overview

Verhaeg.IoT.Processor offers three core components:

### 1. Log - Unified Logging Wrapper
A static utility class that simplifies Serilog configuration with automatic file path management and solution name detection. Creates log files with rolling daily rotation and thread ID enrichment.

### 2. QueueManager - Thread-Safe Queue Processor
An abstract, thread-safe queue that processes objects asynchronously. Available in two versions:
- **QueueManager** (legacy, non-generic) - Deprecated, use QueueManager<T> instead
- **QueueManager<T>** (recommended) - Generic version with type safety

Features include:
- Concurrent queue for thread safety
- Automatic background task with self-restart on failure
- Graceful shutdown with queue draining
- Async-compatible waiting with SemaphoreSlim
- Built-in Serilog integration
- Singleton pattern support via GetInstance()
- Metrics (QueueLength, ProcessedCount, ErrorCount)

### 3. TaskManager - Thread-Safe Background Task
An abstract, thread-safe background task manager. Available in two versions:
- **TaskManager** (legacy, non-generic) - Deprecated, use TaskManager<T> instead
- **TaskManager<T>** (recommended) - Generic version with improved functionality

Features include:
- Automatic background task management
- Async-compatible waiting with SemaphoreSlim
- Proper disposal support
- Built-in Serilog integration
- Singleton pattern support via GetInstance()
- Metrics (IterationCount)

## Installation

```bash
# .NET CLI
dotnet add package Verhaeg.IoT.Processor

# Package Manager
Install-Package Verhaeg.IoT.Processor
```

## Usage

### Prerequisites

Add Serilog configuration to your `appsettings.json`:

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.File"],
    "MinimumLevel": "Debug"
  }
}
```

---

## 📋 Component Documentation

---

### 1. Log

Create a Serilog logger with automatic path configuration:

```csharp
using Verhaeg.IoT.Processor;

// Create a named logger
Serilog.ILogger logger = Processor.Log.CreateLog("MyComponent");

// Use it
logger.Debug("Debug message");
logger.Information("Info message");
logger.Warning("Warning message");
logger.Error("Error message");
```

**Features:**
- Automatic log file path based on machine and solution name
- Rolling log files (daily rotation, 10 files retained)
- Thread ID enrichment in log messages
- On Mario's machine: `D:\Log\{SolutionName}\{logname}.log`
- On other machines: `/log/{SolutionName}/{logname}.log` and `/log/error/{SolutionName}/{logname}.log` (errors only)
- Reads additional configuration from `appsettings.json`

---

### 2. QueueManager

#### ⚠️ Legacy Non-Generic Version (Deprecated)

The original `QueueManager` class is still available for backward compatibility but is **deprecated**. Use `QueueManager<T>` for new code.

```csharp
[Obsolete("Use QueueManager<T> for type safety")]
public class MyQueueProcessor : Processor.QueueManager
{
    public MyQueueProcessor(string name) : base(name) { }
    
    protected override void Process(object obj)
    {
        if (obj is MyData data)
        {
            // Process data
        }
    }
    
    protected override void Dispose() { }
}
```

#### ✅ Recommended: Generic QueueManager<T>

**Basic Usage:**

```csharp
using Verhaeg.IoT.Processor;

public class MyData
{
    public int Id { get; set; }
    public string Value { get; set; }
}

public class MyQueueProcessor : Processor.QueueManager<MyData>
{
    public MyQueueProcessor(string name) : base(name) { }
    
    protected override void Process(MyData data)
    {
        Log.Information("Processing item {Id}: {Value}", data.Id, data.Value);
        // Your processing logic here
    }
    
    // Optional: override Dispose for cleanup
    public override void Dispose()
    {
        Log.Information("Disposing MyQueueProcessor");
        base.Dispose();
    }
}

// Usage
var processor = new MyQueueProcessor("MyQueue");

// Add items to the queue
processor.Write(new MyData { Id = 1, Value = "Test" });

// Stop gracefully (waits for queue to empty)
processor.Stop();

// Or use async
await processor.StopAsync(TimeSpan.FromSeconds(30));
```

**Singleton Pattern:**

```csharp
public class MyQueueProcessor : Processor.QueueManager<MyData>
{
    public static MyQueueProcessor Instance { get; private set; }
    
    private MyQueueProcessor(string name) : base(name) { }
    
    protected override void Process(MyData data)
    {
        Log.Information("Processing: {Data}", data);
    }
    
    // Factory method for singleton
    public static MyQueueProcessor GetInstance(string name = "MyQueue")
    {
        return Instance ??= new MyQueueProcessor(name);
    }
}

// Usage
var processor = MyQueueProcessor.GetInstance();
processor.Write(new MyData());
```

**Or use built-in GetInstance:**

```csharp
// Automatically manages singleton instances
var processor = Processor.QueueManager<MyData>.GetInstance<MyQueueProcessor>("MyQueue");
```

**Async Usage:**

```csharp
// Write items
processor.Write(new MyData());

// Stop with timeout
await processor.StopAsync(TimeSpan.FromSeconds(10));
```

**Metrics:**

```csharp
int pending = processor.QueueLength;
int processed = processor.ProcessedCount;
int errors = processor.ErrorCount;
```

**Features:**
- ✅ Type safety (no casting needed)
- ✅ Thread-safe concurrent queue
- ✅ Auto-starting background processor task
- ✅ Automatic restart if task fails
- ✅ Efficient async-compatible waiting with SemaphoreSlim
- ✅ Graceful stop: waits until all queued items are processed (with timeout)
- ✅ Built-in error logging and metrics
- ✅ Singleton pattern support
- ⚠️ Backward compatible: old QueueManager still works but is deprecated

---

### 3. TaskManager

#### ⚠️ Legacy Non-Generic Version (Deprecated)

The original `TaskManager` class is still available for backward compatibility but is **deprecated**. Use `TaskManager<T>` for new code.

```csharp
[Obsolete("Use TaskManager<T> for improved functionality")]
public class MyBackgroundTask : Processor.TaskManager
{
    public MyBackgroundTask(string name) : base(name) { }
    
    protected override void Process()
    {
        while (!ct.IsCancellationRequested)
        {
            Log.Information("Background task running...");
            ewh.WaitOne(1000);
        }
    }
}
```

#### ✅ Recommended: Generic TaskManager<T>

**Basic Usage:**

```csharp
using Verhaeg.IoT.Processor;

public class MyBackgroundTask : Processor.TaskManager<MyContext>
{
    private MyContext _context;
    
    public MyBackgroundTask(string name) : base(name) { }
    
    protected override void Process()
    {
        Log.Information("Background task started");
        
        while (!ct.IsCancellationRequested)
        {
            Log.Information("Background task running...");
            
            // Do work here
            
            // Wait for signal or timeout
            _signal.Wait(ct, TimeSpan.FromSeconds(1));
        }
        
        Log.Information("Background task stopped");
    }
}

// Usage
var task = new MyBackgroundTask("MyTask");

// Signal the task to wake up
task.Signal();

// Stop the task
task.Stop();

// Or use async
await task.StopAsync(TimeSpan.FromSeconds(5));
```

**Singleton Pattern:**

```csharp
public class MyBackgroundTask : Processor.TaskManager<object>
{
    public static MyBackgroundTask Instance { get; private set; }
    
    private MyBackgroundTask(string name) : base(name) { }
    
    protected override void Process()
    {
        while (!ct.IsCancellationRequested)
        {
            Log.Information("Running...");
            _signal.Wait(ct, TimeSpan.FromSeconds(1));
        }
    }
    
    public static MyBackgroundTask GetInstance(string name = "MyTask")
    {
        return Instance ??= new MyBackgroundTask(name);
    }
}

// Usage
var task = MyBackgroundTask.GetInstance();
task.Signal();
```

**Or use built-in GetInstance:**

```csharp
// Automatically manages singleton instances
var task = Processor.TaskManager<object>.GetInstance<MyBackgroundTask>("MyTask");
```

**Metrics:**

```csharp
int iterations = task.IterationCount;
```

**Features:**
- ✅ Improved async support
- ✅ SemaphoreSlim for efficient waiting
- ✅ Auto-starting background task
- ✅ Implements IDisposable for proper resource cleanup
- ✅ Built-in cancellation token support
- ✅ Built-in error logging
- ✅ Signal() method to wake up the task
- ✅ Singleton pattern support
- ⚠️ Backward compatible: old TaskManager still works but is deprecated

---

## Configuration

### Log Path Configuration

The logger automatically determines the log file path:

- **Windows (Mario's machine)**: `D:\Log\{SolutionName}\{logname}.log`
- **Other machines**: `/log/{SolutionName}/{logname}.log` (general) and `/log/error/{SolutionName}/{logname}.log` (errors only)

The solution name is extracted from the application's base directory.

### Serilog Configuration

The logger reads from `appsettings.json` in your application's base directory. Example:

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.File", "Serilog.Sinks.Console"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/log-.txt",
          "rollingInterval": "Day"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

---

## Migration Guide

### From Non-Generic to Generic QueueManager

**Before (v1.0):**
```csharp
public class MyProcessor : Processor.QueueManager
{
    public MyProcessor(string name) : base(name) { }
    
    protected override void Process(object obj)
    {
        if (obj is MyData data)
        {
            // Process data
        }
    }
    
    protected override void Dispose() { }
}

var processor = new MyProcessor("MyQueue");
processor.Write(myData);
processor.Stop();
```

**After (v1.2+):**
```csharp
public class MyProcessor : Processor.QueueManager<MyData>
{
    public MyProcessor(string name) : base(name) { }
    
    protected override void Process(MyData data)
    {
        // Process data directly (no casting)
    }
}

var processor = new MyProcessor("MyQueue");
processor.Write(myData);  // Strong typing
await processor.StopAsync(TimeSpan.FromSeconds(30));
```

### Changes in v1.2

| Feature | v1.0 | v1.2 | Migration Notes |
|---------|------|------|-----------------|
| QueueManager | Non-generic | Both (generic recommended) | Use QueueManager<T> for new code |
| TaskManager | Non-generic | Both (generic recommended) | Use TaskManager<T> for new code |
| Waiting mechanism | EventWaitHandle | SemaphoreSlim (internal) | No code changes needed |
| Stop() | Synchronous, could deadlock | Synchronous with timeout | Behavior improved, backward compatible |
| StopAsync() | Not available | Available | Use for async scenarios |
| GetInstance() | Not available | Available | Use for singleton pattern |
| Metrics | Not available | QueueLength, ProcessedCount, ErrorCount | Use for monitoring |

---

## Best Practices

### 1. **Use Generic Versions**
```csharp
// ✅ Recommended
public class MyProcessor : Processor.QueueManager<MyData> { ... }

// ❌ Avoid (deprecated)
public class MyProcessor : Processor.QueueManager { ... }
```

### 2. **Use Singleton Pattern**
```csharp
// ✅ Recommended - use GetInstance
var processor = Processor.QueueManager<MyData>.GetInstance<MyProcessor>("MyQueue");

// Or implement your own singleton
public class MyProcessor : Processor.QueueManager<MyData>
{
    public static MyProcessor Instance { get; } = new MyProcessor("MyQueue");
    private MyProcessor(string name) : base(name) { }
}
```

### 3. **Graceful Shutdown with Async**
```csharp
// ✅ Recommended
await processor.StopAsync(TimeSpan.FromSeconds(30));

// ⚠️ Acceptable but may deadlock
processor.Stop();
```

### 4. **Error Handling**
```csharp
protected override void Process(MyData data)
{
    try
    {
        // Your processing logic
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to process item");
        // Optionally: implement retry logic
    }
}
```

### 5. **Resource Management**
```csharp
// ✅ Always dispose or use using statement
using (var processor = new MyProcessor("MyQueue"))
{
    // Use processor
    processor.Write(item);
    
    // Stop when done
    await processor.StopAsync(TimeSpan.FromSeconds(10));
}
// Dispose is called automatically
```

### 6. **Signal TaskManager**
```csharp
// For TaskManager, use Signal() to wake up the task
var task = new MyBackgroundTask("MyTask");

// Wake up the task
task.Signal();

// Stop when done
await task.StopAsync(TimeSpan.FromSeconds(5));
```

### 7. **Log Rotation**
Log files rotate daily and retain 10 files by default. Adjust in your Serilog configuration if needed.

---

## Thread Safety

All components are designed for thread-safe operation:

- **QueueManager<T>**: Uses `ConcurrentQueue<T>` for thread-safe enqueue/dequeue operations
- **TaskManager<T>**: Uses `SemaphoreSlim` for thread-safe signaling
- **Log**: Each logger instance is thread-safe (Serilog's design)

---

## Platform Support

- .NET 10.0
- Windows and Linux (automatic path handling)
- Supports ARM64, x64 architectures

---

## Dependencies

- [Serilog](https://serilog.net/) (4.4.0+)
- [Serilog.Enrichers.Thread](https://github.com/serilog/serilog-enrichers-thread)
- [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration)
- [Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file)
- [Microsoft.Extensions.Configuration.Json](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.configuration.jsonconfigurationextensions)

---

## Project Information

- **Author**: Mario Verhaeg
- **License**: MIT
- **Repository**: https://github.com/marioverhaeg/Verhaeg.IoT.Processor
- **Website**: https://www.marioverhaeg.nl

---

## Version History

- **1.0.9** - Current version
  - Added README.md to NuGet package for better documentation visibility
  - Added GitHub Actions workflow for NuGet trusted publishing with OIDC
  - Fixed EventWaitHandle with SemaphoreSlim for async compatibility
  - Fixed Task.Factory.StartNew parameter order
  - Made Dispose() public in non-generic QueueManager

- **1.0.8** - Previous version
  - Base class for thread-safe queue and task manager
  - Serilog wrapper

See [GitHub Releases](https://github.com/marioverhaeg/Verhaeg.IoT.Processor/releases) for details.

---

## Contributing

Contributions are welcome! Please open an issue or submit a pull request on GitHub.
