# Verhaeg.IoT.Processor

A .NET library providing reusable components for IoT applications: **thread-safe logging**, **queue management**, and **background task management**.

[![NuGet Version](https://img.shields.io/nuget/v/Verhaeg.IoT.Processor)](https://www.nuget.org/packages/Verhaeg.IoT.Processor/)

## Overview

Verhaeg.IoT.Processor offers three core components:

### 1. Log - Unified Logging Wrapper
A static utility class that simplifies Serilog configuration with automatic file path management and solution name detection. Creates log files with rolling daily rotation and thread ID enrichment.

### 2. QueueManager - Thread-Safe Queue Processor
An abstract, thread-safe singleton queue that processes objects asynchronously. Features include:
- Concurrent queue for thread safety
- Automatic background task with self-restart on failure
- Graceful shutdown with queue draining
- Event-driven waiting for efficiency
- Built-in Serilog integration

### 3. TaskManager - Thread-Safe Background Task
An abstract, thread-safe singleton for running continuous background tasks. Features include:
- Automatic background task management
- Event-driven waiting
- Proper disposal support
- Built-in Serilog integration

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

### 2. QueueManager

Create a custom queue processor by inheriting from `QueueManager`:

```csharp
using Verhaeg.IoT.Processor;

public class MyQueueProcessor : Processor.QueueManager
{
    public static MyQueueProcessor Instance { get; private set; }
    
    public MyQueueProcessor(string name) : base(name)
    {
        Instance = this;
    }
    
    protected override void Process(object obj)
    {
        // Process each item from the queue
        if (obj is MyData data)
        {
            Log.Information("Processing: {Data}", data);
            // Your processing logic here
        }
    }
    
    protected override void Dispose()
    {
        // Clean up resources
        Log.Information("Disposing MyQueueProcessor");
    }
}

// Usage
var processor = new MyQueueProcessor("MyQueue");

// Add items to the queue
processor.Write(new MyData { Id = 1, Value = "Test" });

// Stop gracefully (waits for queue to empty)
processor.Stop();
```

**Features:**
- Thread-safe concurrent queue
- Auto-starting background processor task
- Automatic restart if task fails
- Efficient waiting with `EventWaitHandle`
- Graceful stop: waits until all queued items are processed
- Built-in error logging

### 3. TaskManager

Create a custom background task by inheriting from `TaskManager`:

```csharp
using Verhaeg.IoT.Processor;

public class MyBackgroundTask : Processor.TaskManager
{
    public static MyBackgroundTask Instance { get; private set; }
    
    public MyBackgroundTask(string name) : base(name)
    {
        Instance = this;
    }
    
    protected override void Process()
    {
        // Your background task logic
        while (!ct.IsCancellationRequested)
        {
            Log.Information("Background task running...");
            
            // Do work here
            
            // Wait for signal or timeout
            ewh.WaitOne(1000); // Wait 1 second
        }
    }
}

// Usage
var task = new MyBackgroundTask("MyTask");

// Stop the task
task.Stop();

// Or dispose (calls Stop internally)
task.Dispose();
```

**Features:**
- Auto-starting background task
- Implements `IDisposable` for proper resource cleanup
- Event-driven waiting with `EventWaitHandle`
- Built-in cancellation token support
- Built-in error logging

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

## Best Practices

1. **Singleton Pattern**: Create one instance of each manager per application and reuse it via a static `Instance` property.

2. **Graceful Shutdown**: Always call `Stop()` or `Dispose()` when shutting down your application to ensure all queued items are processed.

3. **Error Handling**: The built-in logging captures exceptions, but implement additional error handling in your `Process()` methods.

4. **Resource Management**: Override `Dispose()` in `QueueManager` to clean up any additional resources.

5. **Log Rotation**: Log files rotate daily and retain 10 files by default. Adjust in your Serilog configuration if needed.

## Thread Safety

All components are designed for thread-safe operation:

- **QueueManager**: Uses `ConcurrentQueue<object>` for thread-safe enqueue/dequeue operations
- **TaskManager**: Uses proper synchronization primitives for thread safety
- **Log**: Each logger instance is thread-safe (Serilog's design)

## Platform Support

- .NET 10.0
- Windows and Linux (automatic path handling)
- Supports ARM64, x64 architectures

## Dependencies

- [Serilog](https://serilog.net/) (4.4.0+)
- [Serilog.Enrichers.Thread](https://github.com/serilog/serilog-enrichers-thread)
- [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration)
- [Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file)
- [Microsoft.Extensions.Configuration.Json](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.configuration.jsonconfigurationextensions)

## Project Information

- **Author**: Mario Verhaeg
- **License**: MIT
- **Repository**: https://github.com/marioverhaeg/Verhaeg.IoT.Processor
- **Website**: https://www.marioverhaeg.nl

## Version History

- **1.0.8** - Current version
- See [GitHub Releases](https://github.com/marioverhaeg/Verhaeg.IoT.Processor/releases) for details

## Contributing

Contributions are welcome! Please open an issue or submit a pull request on GitHub.
