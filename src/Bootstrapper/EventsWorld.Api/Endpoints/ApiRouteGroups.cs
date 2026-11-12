using EventsWorld.Api.RateLimiting;
using EventsWorld.Shared.Infrastructure.Api;

namespace EventsWorld.Api.Endpoints;

public static class ApiRouteGroups
{
    public const string V1Prefix = "/api/v1";

    /// <summary>
    /// Creates the <c>/api/v1</c> groups (backend.md §4) and maps every module's
    /// <see cref="IApiEndpoints"/>. Authentication and authorization policies are added to the
    /// groups in TS-005 and TS-006.
    /// </summary>
    public static IEndpointRouteBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var v1 = endpoints.MapGroup(V1Prefix);

        var groups = new ApiGroups(
            V1: v1,
            Public: v1.MapGroup("/public")
                .RequireRateLimiting(RateLimitingExtensions.AnonymousPolicy)
                .WithTags("Public"),
            Participant: v1.MapGroup("/participant")
                .RequireRateLimiting(RateLimitingExtensions.AnonymousPolicy)
                .WithTags("Participant"),
            Me: v1.MapGroup("/me").WithTags("Me"),
            Event: v1.MapGroup("/events/{eventId:guid}").WithTags("Events"),
            Admin: v1.MapGroup("/admin").WithTags("Admin"));

        foreach (var module in endpoints.ServiceProvider.GetServices<IApiEndpoints>())
        {
            module.MapEndpoints(groups);
        }

        return endpoints;
    }
}
