using System;
using System.Collections.Generic;
using System.Text;

using Serilog;
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
            // Logging
            Serilog.ILogger Log = new LoggerConfiguration()
                       .WriteTo.File("log" + System.IO.Path.AltDirectorySeparatorChar + name + ".log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 5)
                       .WriteTo.Console()   
                       .ReadFrom.Configuration(slconf)
                       .CreateLogger();
            return Log;
        }
    }
}
