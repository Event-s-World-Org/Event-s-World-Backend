using EventsWorld.Shared.Infrastructure.Api;
using EventsWorld.Shared.Kernel.Results;

namespace EventsWorld.UnitTests;

public sealed class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.PreconditionFailed, 412)]
    [InlineData(ErrorType.Failure, 500)]
    public void StatusCodeFor_MapsEveryErrorType(ErrorType type, int expected)
    {
        Assert.Equal(expected, ResultExtensions.StatusCodeFor(type));
    }

    [Theory]
    [InlineData(404, "request.not_found")]
    [InlineData(405, "request.method_not_allowed")]
    [InlineData(429, "rate_limit.exceeded")]
    [InlineData(400, "request.invalid")]
    [InlineData(503, "internal.server_error")]
    public void ProblemCodes_ForStatus_GivesAStableCode(int status, string expected)
    {
        Assert.Equal(expected, ProblemCodes.ForStatus(status));
    }

    [Fact]
    public void ToProblem_OnASuccessfulResult_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Success().ToProblem());
    }
}
