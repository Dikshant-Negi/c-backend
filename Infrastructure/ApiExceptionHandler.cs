using EquipmentManagementBackend.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Infrastructure;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            ServiceException serviceException => serviceException.StatusCode,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            ArgumentException => StatusCodes.Status400BadRequest,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        await Results.Problem(
            statusCode: statusCode,
            detail: statusCode == 500 ? "An unexpected error occurred." : exception.Message)
            .ExecuteAsync(context);

        return true;
    }
}
