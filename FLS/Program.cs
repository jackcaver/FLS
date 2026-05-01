using FLS.Implementation;
using Serilog;
using Serilog.Events;
using System;

namespace FLS
{
    public class Program
    {
        public static void Main()
        {
            var log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
                .CreateLogger();

            Log.Logger = log;

            var server = new Server("./config.json");

            server.Start();

            while (!Console.KeyAvailable)
                server.CheckClients();

            server.Stop();
        }
    }
}