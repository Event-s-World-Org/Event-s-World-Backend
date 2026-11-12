using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace EventsWorld.IntegrationTests.Infrastructure;

/// <summary>
/// The in-memory test server has no client IP, and ASP.NET Core then trusts any
/// <c>X-Forwarded-For</c>. Real connections always have one, so the tests use a fixed client IP.
/// </summary>
internal sealed class ClientIpStartupFilter : IStartupFilter
{
    public static readonly IPAddress ClientIp = IPAddress.Parse("198.51.100.7");

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress ??= ClientIp;
            return nextMiddleware(context);
        });
        next(app);
    };
}
