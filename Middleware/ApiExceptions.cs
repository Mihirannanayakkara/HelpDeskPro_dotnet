using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HelpDeskPro.Middleware;

public class NotFoundException(string message) : Exception(message);
public class ForbiddenException(string message) : Exception(message);
public class BusinessRuleException(string message) : Exception(message);

public class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            NotFoundException => (404, "Not found"),
            ForbiddenException => (403, "Forbidden"),
            BusinessRuleException => (400, "Business rule violation"),
            _ => (0, "")
        };

        if (status == 0) return false;   // unknown error: let the default handler return 500

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title, Detail = ex.Message }, ct);
        return true;
    }
}