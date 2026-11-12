using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace EventsWorld.Api.Validation;

public sealed class ValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            return await next(context);
        }

        var argument = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (argument is null)
        {
            return await next(context);
        }

        var validationResult = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (!validationResult.IsValid)
        {
            var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString()
                ?? context.HttpContext.TraceIdentifier;

            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            var problemDetails = new HttpValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457",
                Detail = "The request failed validation checks.",
                Instance = context.HttpContext.Request.Path
            };

            problemDetails.Extensions["code"] = "validation.failed";
            problemDetails.Extensions["correlationId"] = correlationId;

            return Results.Problem(problemDetails);
        }

        return await next(context);
    }
}

public static class ValidationEndpointFilterExtensions
{
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class
    {
        return builder.AddEndpointFilter<ValidationFilter<TRequest>>();
    }
}
