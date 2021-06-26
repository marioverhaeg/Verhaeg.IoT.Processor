# Introduction
Verhaeg.IoT.Processor has three purposes:

* Wrapping Serilog for re-use (Log.cs). 
* Thread-safe management of a queue: QueueManager.cs is an abstract thread-safe singleton queue in which objects can be written and processed.
* Thread-safe management of a task: TaskManager is an abstract thread-safe singleton task.

# Examples
## Log.cs
```csharp
Serilog.ILogger l = Processor.Log.CreateLog("Name of logfile");
l.Debug("Debug...");
```

Don't forget to configure the MinimumLevel for Serilog in the appsettings.json file of your project:
```xml
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.File" ],
    "MinimumLevel": "Debug"
  }
}

```
## TaskManager.cs
This is an abstract class that needs to be inherited. You can use the Instance() method 
```csharp
Processor.TaskManager.Instance(); // Starts a thread including log file.
```

## QueueManager.cs

```csharp
Processor.QueueManager.Instance().Write(object); // Writes an object into the queue. Process(object o); method is started to process object.
```
