using CriticalCare.ConsoleApp;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static CriticalCare.ConsoleApp.Views.Shared.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

[ApiController]
[Route("api/backup")]
[Authorize(Roles = "staff")]
[Tags("Staff")]
public sealed class BackupController : MenuController
{
    private readonly IBackupsService backups;
    private readonly IReportsService reports;
    private readonly IHttpContextAccessor? httpContextAccessor;

    public BackupController(
        IAuthenticationService auth,
        IBackupsService backups,
        IReportsService reports,
        IHttpContextAccessor? httpContextAccessor = null) : base(auth)
    {
        this.backups = backups;
        this.reports = reports;
        this.httpContextAccessor = httpContextAccessor;
    }

    [NonAction]
    public Task RunStaffAsync(Session session) => RunAsync(session, "staff");

    [NonAction]
    public Task RunAsync(Session session, string role) => RunMenuAsync(session, role, "Backup allocation", new()
    {
        ["1"] = ("Request backup allocation", () => RequestAndConfirmAsync(session)),
        ["2"] = ("Allocation history", () => AllocationHistoryAsync(session))
    });

    /// <summary>Create a backup request for the authenticated Staff user.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BackupDetails), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateBackupHttpRequest request)
    {
        var result = await backups.RequestAsync(
            CurrentSession(),
            request.EquipmentTypeId,
            request.RequestedWard,
            request.BedNumber);

        return new ObjectResult(result) { StatusCode = StatusCodes.Status201Created };
    }

    /// <summary>List backup requests belonging to the authenticated Staff user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<Models.BackupRequest>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] BackupQuery query)
    {
        var session = CurrentSession();
        var result = await backups.ListAsync(session, query.Page, ownOnly: true);
        return new OkObjectResult(result);
    }

    /// <summary>Confirm pickup of the active allocation for a backup request.</summary>
    [HttpPost("{requestId:long}/confirm")]
    [ProducesResponseType(typeof(BackupDetails), StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm([Range(1, long.MaxValue)] long requestId)
    {
        var session = CurrentSession();
        var details = await backups.DetailsAsync(session, requestId);
        var allocationId = details.Reservation?.Allocation.Id
            ?? throw new ServiceException(409, "No active allocation is available for pickup.");

        var result = await backups.ActionAsync(session, "pickup", requestId, allocationId);
        return new OkObjectResult(result);
    }

    /// <summary>Cancel a backup request belonging to the authenticated Staff user.</summary>
    [HttpPost("{requestId:long}/cancel")]
    [ProducesResponseType(typeof(BackupDetails), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel([Range(1, long.MaxValue)] long requestId)
    {
        var result = await backups.ActionAsync(CurrentSession(), "cancel", requestId);
        return new OkObjectResult(result);
    }

    private Session CurrentSession()
    {
        var value = httpContextAccessor?.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var userId)
            ? new Session(userId)
            : throw new UnauthorizedAccessException("Authenticated user ID is missing.");
    }

    private async Task RequestAndConfirmAsync(Session session)
    {
        var ward = Choice<WardType>("Ward type");
        var equipmentTypeId = Number("Equipment type ID");
        var bedNumber = Read("Bed number");

        var details = await backups.RequestAsync(session, equipmentTypeId, ward, bedNumber);
        BackupView.ShowAllocationResult(details);

        if (details.Reservation?.Allocation.Status != BackupAllocationStatus.reserved)
        {
            return;
        }

        var choice = BackupView.ReadAllocationDecision();
        var action = choice == "1" ? "pickup" : "cancel";
        var result = await backups.ActionAsync(
            session,
            action,
            details.Request.Id,
            details.Reservation.Allocation.Id);

        BackupView.ShowAllocationResult(result);
    }

    private async Task AllocationHistoryAsync(Session session)
    {
        var pageNumber = 1;
        while (true)
        {
            var history = await backups.ListAsync(session, pageNumber, ownOnly: true);
            if (history.TotalCount == 0)
            {
                ConsoleView.ShowMessage("No backup requests found.");
                return;
            }

            BackupView.ShowList(history);
            if (pageNumber >= history.TotalPages)
            {
                return;
            }
            pageNumber++;
        }
    }

}

public sealed record CreateBackupHttpRequest(
    [property: EnumDataType(typeof(WardType))] WardType RequestedWard,
    [property: Range(1, long.MaxValue)] long EquipmentTypeId,
    [property: Required, MaxLength(50)] string BedNumber
);
