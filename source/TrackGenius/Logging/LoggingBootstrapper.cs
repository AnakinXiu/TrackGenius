using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace TrackGenius.UI.Logging;

public static class LoggingBootstrapper
{
    public static LoggingContext Configure()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        var sessionId = Guid.NewGuid().ToString("N");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("SessionId", sessionId)
            .WriteTo.Logger(configuration => configuration
                .Filter.ByIncludingOnly(Matching.FromSource("TrackGenius.Communication"))
                .WriteTo.File(
                    Path.Combine(logDirectory, "communication-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    shared: true))
            .WriteTo.Logger(configuration => configuration
                .Filter.ByIncludingOnly(Matching.FromSource("TrackGenius.UserBehavior"))
                .WriteTo.File(
                    Path.Combine(logDirectory, "user-behavior-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    shared: true))
            .WriteTo.Logger(configuration => configuration
                .Filter.ByExcluding(Matching.FromSource("TrackGenius.Communication"))
                .Filter.ByExcluding(Matching.FromSource("TrackGenius.UserBehavior"))
                .WriteTo.File(
                    Path.Combine(logDirectory, "application-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    shared: true))
            .WriteTo.File(
                Path.Combine(logDirectory, "error-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                restrictedToMinimumLevel: LogEventLevel.Error,
                shared: true)
            .CreateLogger();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddSerilog(Log.Logger, dispose: false);
        });

        return new LoggingContext(loggerFactory, sessionId, logDirectory);
    }
}