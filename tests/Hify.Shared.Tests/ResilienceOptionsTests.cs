using System.ComponentModel.DataAnnotations;

using Hify.Shared.Resilience;

namespace Hify.Shared.Tests;

public class ResilienceOptionsTests
{
    [Fact]
    public void Defaults_AlignWithSpec()
    {
        var options = new ResilienceOptions();

        Assert.Equal(60, options.AttemptTimeoutSeconds);
        Assert.Equal(2, options.RetryCount);
        Assert.Equal(50, options.MaxConcurrency);
        Assert.Equal(0, options.QueueLimit);
    }

    [Theory]
    [InlineData(0, false)]      // 超时不可为 0
    [InlineData(60, true)]
    [InlineData(601, false)]    // 超出上限
    public void AttemptTimeout_RespectsRange(int seconds, bool expectedValid)
    {
        var options = new ResilienceOptions { AttemptTimeoutSeconds = seconds };

        var valid = Validator.TryValidateObject(
            options,
            new ValidationContext(options),
            new List<ValidationResult>(),
            validateAllProperties: true);

        Assert.Equal(expectedValid, valid);
    }
}
