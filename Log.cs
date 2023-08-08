﻿using System;
using System.Collections.Generic;
using System.Text;

using Serilog;
using Serilog.Enrichers;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Verhaeg.IoT.Processor
{
    public static class Log
    {
        public static Serilog.ILogger CreateLog(string name)
        {
            // Serilog configuration
            var slconf = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
            // Serilog configuration
            string machinename = System.Environment.MachineName;
            if (machinename.ToLower().Contains("mario"))
            {
                Serilog.ILogger Log = new LoggerConfiguration()
                           .WriteTo.File("log" + Path.AltDirectorySeparatorChar + name + ".log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 10,
                           outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] <{ThreadId}> {Message:lj} {NewLine}{Exception}")
                           .Enrich.WithThreadId()
                           .MinimumLevel.Debug()
                           .CreateLogger();
                return Log;
            }
            else
            {
                Serilog.ILogger Log = new LoggerConfiguration()
                           .WriteTo.File("log" + Path.AltDirectorySeparatorChar + name + ".log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 5,
                           outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] <{ThreadId}> {Message:lj} {NewLine}{Exception}")
                           .WriteTo.TcpSyslog("192.168.21.156", 5140, name, Serilog.Sinks.Syslog.FramingType.OCTET_COUNTING, Serilog.Sinks.Syslog.SyslogFormat.RFC5424,
                           Serilog.Sinks.Syslog.Facility.Local0, false, null, null, null, Serilog.Events.LogEventLevel.Error, name, null, machinename, null, null)
                           .ReadFrom.Configuration(slconf)
                           .Enrich.WithThreadId()
                           .CreateLogger();
                return Log;
            }
        }
    }
}
