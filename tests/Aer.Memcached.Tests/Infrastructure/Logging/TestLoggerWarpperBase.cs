using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Tests.Infrastructure.Logging;

internal class TestLoggerWarpperBase
{
	private readonly ILogger _logger;

	public int WarningCount { get; set; }

	public int ErrorCount { get; set; }

	public List<string> LoggedMessages { get; } = new();

	public TestLoggerWarpperBase(ILogger logger)
	{
		_logger = logger;
	}
	
	public void Clear()
	{
		WarningCount = 0;
		ErrorCount = 0;
		LoggedMessages.Clear();
	}

	public IDisposable BeginScope<TState>(TState state)
	{
		return _logger.BeginScope(state);
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		return true;
	}

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception exception,
		Func<TState, Exception, string> formatter)
	{
		if (logLevel == LogLevel.Error)
		{
			ErrorCount++;
		}

		if (logLevel == LogLevel.Warning)
		{
			WarningCount++;
		}

		var message = formatter(state, exception);
		LoggedMessages.Add(message);
		
		_logger.Log(logLevel, eventId, state,exception, formatter);
	}
}
