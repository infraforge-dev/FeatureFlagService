using System.Text.Json;
using Banderas.Application.Exceptions;
using Banderas.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Banderas.Api.Middleware;

/// <summary>
/// Catches all unhandled exceptions in the pipeline and returns a
/// consistent ProblemDetails response. Domain exceptions are mapped
/// to their declared HTTP status codes. All other exceptions are logged
/// and returned as a generic 500.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BanderasException ex)
        {
            await WriteProblemDetailsAsync(
                context,
                statusCode: ex.StatusCode,
                title: GetTitleForStatusCode(ex.StatusCode),
                detail: ex.Message
            );
        }
        catch (AiAnalysisUnavailableException ex)
        {
            _logger.LogWarning(
                ex,
                "Flag health analysis is currently unavailable. Reason={Reason}",
                ex.Message
            );

            await WriteProblemDetailsAsync(
                context,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Flag health analysis is currently unavailable.",
                detail: "The flag health analysis service could not be reached. "
                    + "Please try again later.",
                type: "https://tools.ietf.org/html/rfc9110#section-15.6.4"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path
            );

            await WriteProblemDetailsAsync(
                context,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "An unexpected error occurred.",
                detail: "An internal error occurred. Please try again later."
            );
        }
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string type = "about:blank"
    )
    {
        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path,
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static string GetTitleForStatusCode(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            _ => "An error occurred",
        };
}
