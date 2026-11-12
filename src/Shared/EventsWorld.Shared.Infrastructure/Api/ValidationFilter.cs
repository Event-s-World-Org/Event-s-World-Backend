using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EventsWorld.Shared.Infrastructure.Api;

/// <summary>
/// Validates the request shape with FluentValidation before the handler runs (backend.md §4) and
/// returns 400 with the failing fields. Business rules stay in the domain and return a Result.
/// </summary>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (validator is null || request is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        return TypedResults.ValidationProblem(
            result.ToDictionary(),
            detail: "The request failed validation checks.",
            extensions: new Dictionary<string, object?> { [ProblemCodes.ExtensionName] = ProblemCodes.ValidationFailed });
    }
}

public static class ValidationEndpointFilterExtensions
{
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class =>
        builder.AddEndpointFilter<ValidationFilter<TRequest>>();
}
