using System;
using System.Collections.Generic;
using System.Text;

using Serilog;
using Serilog.Enrichers;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Verhaeg.IoT.Processor
{
    public static class Log
    {
        public static Serilog.ILogger CreateLog(string name)
        {
            // Serilog configuration
            var slconf = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            string solution_name = GetSolutionName();

            // Serilog configuration
            string machinename = System.Environment.MachineName;
            if (machinename.ToLower().Contains("mario"))
            {
                Serilog.ILogger Log = new LoggerConfiguration()
                           .WriteTo.File(AppContext.BaseDirectory + Path.AltDirectorySeparatorChar + "log" + Path.AltDirectorySeparatorChar + solution_name + "_" + name + ".log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 10,
                           outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] <{ThreadId}> {Message:lj} {NewLine}{Exception}")
                           .Enrich.WithThreadId()
                           .MinimumLevel.Debug()
                           .CreateLogger();
                return Log;
            }
            else
            {
                Serilog.ILogger Log = new LoggerConfiguration()
                           .WriteTo.File(AppContext.BaseDirectory + Path.AltDirectorySeparatorChar + "log" + Path.AltDirectorySeparatorChar + solution_name + "_" + name + ".log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 10,
                           outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] <{ThreadId}> {Message:lj} {NewLine}{Exception}")
                           .WriteTo.TcpSyslog("192.168.21.156", 514, solution_name + "_" + name, Serilog.Sinks.Syslog.FramingType.OCTET_COUNTING, Serilog.Sinks.Syslog.SyslogFormat.RFC5424,
                           Serilog.Sinks.Syslog.Facility.Local0, false, null, null, null, Serilog.Events.LogEventLevel.Error, name, null, machinename, null, null)
                           .ReadFrom.Configuration(slconf)
                           .Enrich.WithThreadId()
                           .CreateLogger();
                return Log;
            }
        }

        private static string GetSolutionName()
        {
            try
            {
                string basedir = AppDomain.CurrentDomain.BaseDirectory;
                int index1 = basedir.IndexOf("Verhaeg.IoT");
                int index2 = 0;
                string sn = basedir.Remove(0, index1);
                if (sn.Contains("//"))
                {
                    index2 = sn.IndexOf("//");
                }
                else if (sn.Contains("\\"))
                {
                    index2 = sn.IndexOf("\\");
                }
                else if (sn.Contains("/"))
                {
                    index2 = sn.IndexOf("/");
                }
                else
                {
                    throw new Exception();
                }
                sn = sn.Remove(index2, sn.Count() - index2);
                sn = sn.Replace(".", "_");
                return sn;
            }
            catch 
            {
                return "Unknown_Application";
            }
        }
    }
}
