using Microsoft.AspNetCore.Diagnostics;
using Orders.Api.Orders;
using Orders.Api.Validation;

namespace Orders.Api;

public sealed class ExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationFailedException failure:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    new ValidationErrors(failure.Errors), cancellationToken);
                return true;

            case OrderNotFoundException:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return true;

            default:
                return false;
        }
    }
}
