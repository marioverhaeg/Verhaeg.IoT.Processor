# Introduction
Verhaeg.IoT.Processor has three sub classes:

* Log wrappes Serilog for re-use. 
* QueueManager is an abstract thread-safe singleton queue in which objects can be written and processed.
* TaskManager is an abstract thread-safe singleton task.

# Examples
## Log.cs
```csharp
Serilog.ILogger l = Processor.Log.CreateLog("Name of logfile");
l.Debug("Debug...");
```
## TaskManager.cs

```csharp
Processor.TaskManager.Instance(); // Starts a thread including log file.
```

## QueueManager.cs

```csharp
Processor.QueueManager.Instance().Write(object); // Writes an object into the queue. Process(object o); method is started to process object.
```
