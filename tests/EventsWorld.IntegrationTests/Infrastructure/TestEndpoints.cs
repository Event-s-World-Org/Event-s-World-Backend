using EventsWorld.Shared.Infrastructure.Api;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EventsWorld.IntegrationTests.Infrastructure;

/// <summary>
/// Endpoints that exist only in the tests, mapped into the real <c>/api/v1/public</c> group so they
/// go through the same pipeline (rate limit, validation, errors) as module endpoints.
/// </summary>
internal sealed class TestEndpoints : IApiEndpoints
{
    public const string Ping = "/api/v1/public/test/ping";
    public const string Echo = "/api/v1/public/test/echo";
    public const string Crash = "/api/v1/public/test/crash";
    public const string CrashMessage = "Simulated crash for the exception tests.";

    public void MapEndpoints(ApiGroups groups)
    {
        var test = groups.Public.MapGroup("/test");

        test.MapGet("/ping", () => TypedResults.Ok(new { message = "pong" }));

        test.MapPost("/echo", (EchoRequest request) => TypedResults.Ok(request))
            .WithRequestValidation<EchoRequest>();

        test.MapGet("/crash", IResult () => throw new InvalidOperationException(CrashMessage));
    }
}

public sealed record EchoRequest(string Name, string Email);

internal sealed class EchoRequestValidator : AbstractValidator<EchoRequest>
{
    public EchoRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
