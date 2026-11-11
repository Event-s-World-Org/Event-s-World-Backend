using EventsWorld.Shared.Kernel.Results;

namespace EventsWorld.UnitTests;

public sealed class ResultTests
{
    [Fact]
    public void Success_ShouldHaveIsSuccessTrue_AndErrorNone()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_ShouldHaveIsFailureTrue_AndExposeError()
    {
        var error = Error.Validation("Test.Invalid", "Invalid input.");
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void AccessingValue_OnFailureResult_ShouldThrowInvalidOperationException()
    {
        Result<int> result = Error.NotFound("Test.NotFound", "Item not found.");

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromNonNullValue_ShouldCreateSuccessResult()
    {
        Result<string> result = "events-world";

        Assert.True(result.IsSuccess);
        Assert.Equal("events-world", result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromNullValue_ShouldCreateNullValueFailure()
    {
        string? nullValue = null;
        Result<string> result = nullValue;

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NullValue, result.Error);
    }
}
