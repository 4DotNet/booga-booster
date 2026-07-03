using FourDotnet.BoogaBooster.Core;
using Xunit;

namespace FourDotnet.BoogaBooster.Core.Tests;

/// <summary>
/// Covers both constructors of <see cref="DomainValidationException"/> (ADR-0003).
/// </summary>
public class DomainValidationExceptionTests
{
    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var exception = new DomainValidationException("Guest id is required.");

        Assert.Equal("Guest id is required.", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var inner = new InvalidOperationException("boom");

        var exception = new DomainValidationException("Invalid state.", inner);

        Assert.Equal("Invalid state.", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void IsAnException()
    {
        var exception = new DomainValidationException("x");
        Assert.IsAssignableFrom<Exception>(exception);
    }
}
