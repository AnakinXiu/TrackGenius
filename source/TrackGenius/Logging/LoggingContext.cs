using Microsoft.Extensions.Logging;

namespace TrackGenius.UI.Logging;

public sealed record LoggingContext(ILoggerFactory LoggerFactory, string SessionId, string LogDirectory);