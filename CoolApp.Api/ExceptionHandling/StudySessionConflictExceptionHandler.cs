using CoolApp.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CoolApp.ExceptionHandling;

// Turns StudySessionConflictException into a 409 Conflict, for every endpoint at once -
// no try/catch in the controller. Registered in Program.cs with AddExceptionHandler and
// called by app.UseExceptionHandler() when a request throws.
public class StudySessionConflictExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // false: "not mine" - the next handler (or the default 500) gets the exception.
        if (exception is not StudySessionConflictException conflict)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The study session was changed by someone else",
                Detail = conflict.Message,
                Extensions = { ["sessionId"] = conflict.SessionId }
            }
        });
    }
}
