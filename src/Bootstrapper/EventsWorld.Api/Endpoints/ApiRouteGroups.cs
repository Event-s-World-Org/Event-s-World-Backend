using EventsWorld.Api.RateLimiting;
using EventsWorld.Api.Validation;
using FluentValidation;

namespace EventsWorld.Api.Endpoints;

public static class ApiRouteGroups
{
    public const string V1Prefix = "/api/v1";

    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup(V1Prefix);

        // Anonymous public group - rate limited
        var publicGroup = api.MapGroup("/public")
            .RequireRateLimiting(RateLimitingExtensions.AnonymousPolicy)
            .WithTags("Public");

        // Baseline public endpoints for health/ping, validation and testing
        publicGroup.MapGet("/ping", () => Results.Ok(new { message = "pong" }));

        publicGroup.MapPost("/validate", (PingRequest request) => Results.Ok(request))
            .WithRequestValidation<PingRequest>();

        publicGroup.MapGet("/crash", () =>
        {
            throw new InvalidOperationException("Simulated crash for exception testing.");
        });

        // Anonymous participant group - rate limited
        api.MapGroup("/participant")
            .RequireRateLimiting(RateLimitingExtensions.AnonymousPolicy)
            .WithTags("Participant");

        // Authenticated current user group
        api.MapGroup("/me")
            .WithTags("Me");

        // Event-scoped group
        api.MapGroup("/events/{eventId}")
            .WithTags("Events");

        // Admin group
        api.MapGroup("/admin")
            .WithTags("Admin");

        return api;
    }
}

public sealed record PingRequest(string Name, string Email);

public sealed class PingRequestValidator : AbstractValidator<PingRequest>
{
    public PingRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email is required.");
    }
}
