using Finance.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Host;

/// <summary>
/// Turns the exceptions use cases raise into RFC 7807 responses with the right
/// status code: <see cref="NotFoundException"/> → 404, argument problems → 400.
/// Anything else is left to the framework's default 500 handler.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => 0,
        };
        if (status == 0)
            return false; // not one of ours

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = exception.Message },
            cancellationToken);
        return true;
    }
}
