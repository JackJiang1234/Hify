using Hify.Host.Logging;

using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Hify.IntegrationTests;

public class SensitiveDataEnricherTests
{
    private sealed class StubPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }

    private static LogEvent EventWith(params LogEventProperty[] properties)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplate("test", Array.Empty<MessageTemplateToken>()),
            properties);
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("ApiKey")]
    [InlineData("Authorization")]
    [InlineData("Token")]
    [InlineData("Prompt")]
    public void Enrich_MasksSensitiveProperties(string propertyName)
    {
        var logEvent = EventWith(new LogEventProperty(propertyName, new ScalarValue("super-secret")));

        new SensitiveDataEnricher().Enrich(logEvent, new StubPropertyFactory());

        var masked = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        Assert.Equal("***", masked.Value);
    }

    [Fact]
    public void Enrich_LeavesNonSensitivePropertiesUntouched()
    {
        var logEvent = EventWith(
            new LogEventProperty("UserId", new ScalarValue(42)),
            new LogEventProperty("Password", new ScalarValue("secret")));

        new SensitiveDataEnricher().Enrich(logEvent, new StubPropertyFactory());

        Assert.Equal(42, ((ScalarValue)logEvent.Properties["UserId"]).Value);
        Assert.Equal("***", ((ScalarValue)logEvent.Properties["Password"]).Value);
    }
}
