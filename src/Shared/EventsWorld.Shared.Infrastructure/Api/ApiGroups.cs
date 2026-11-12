using Microsoft.AspNetCore.Routing;

namespace EventsWorld.Shared.Infrastructure.Api;

/// <summary>
/// The <c>/api/v1</c> route groups (backend.md §4). Each group already carries its policies
/// (for example the per-IP rate limit on anonymous groups), so modules only add endpoints.
/// </summary>
/// <param name="V1">The <c>/api/v1</c> root, for routes outside the groups (e.g. <c>POST /events</c>).</param>
/// <param name="Public"><c>/public</c>: anonymous, rate limited per IP.</param>
/// <param name="Participant"><c>/participant</c>: participant token, rate limited per IP.</param>
/// <param name="Me"><c>/me</c>: the current user.</param>
/// <param name="Event"><c>/events/{eventId}</c>: event roles.</param>
/// <param name="Admin"><c>/admin</c>: Super Admin.</param>
public sealed record ApiGroups(
    RouteGroupBuilder V1,
    RouteGroupBuilder Public,
    RouteGroupBuilder Participant,
    RouteGroupBuilder Me,
    RouteGroupBuilder Event,
    RouteGroupBuilder Admin);

/// <summary>
/// A set of endpoints added to the API groups. Each module registers its own implementation
/// (<c>services.AddSingleton&lt;IApiEndpoints, XEndpoints&gt;()</c>) and the host maps them all.
/// </summary>
public interface IApiEndpoints
{
    void MapEndpoints(ApiGroups groups);
}
