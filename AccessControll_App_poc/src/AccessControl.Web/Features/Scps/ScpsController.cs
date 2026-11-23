using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Scps;
using AccessControl.Application.Features.Scps.DTOs;
using AccessControl.Application.Features.Scps.Interfaces;
using AccessControl.Domain.Common.Results;
using AccessControl.Web.Controllers;
using AccessControl.Web.Features.Scps.Models;
using Microsoft.AspNetCore.Mvc;

namespace AccessControl.Web.Features.Scps;

/// <summary>Controllers (HID SCPs): the desired configuration only — nothing is sent to hardware from here.</summary>
public sealed partial class ScpsController(IScpService scpService) : ModalFormController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        Guid? siteId = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = new ScpListFilter(search, siteId);
        var list = await scpService.ListAsync(filter, new PagedRequest(page), cancellationToken);
        var sites = await scpService.ListSiteOptionsAsync(cancellationToken);

        return View(new ScpListViewModel(list.Value!, filter, sites.Value!));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await scpService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? View(ScpDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? siteId = null, CancellationToken cancellationToken = default)
    {
        var nextNumber = await scpService.GetNextFreeScpNumberAsync(cancellationToken);
        var form = new ScpFormViewModel { SiteId = siteId, ScpNumber = nextNumber.Value };
        await LoadOptionsAsync(form, cancellationToken);

        return FormView(nameof(Create), form);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ScpFormViewModel form, CancellationToken cancellationToken)
    {
        if (form.Model is null)
        {
            ModelState.AddModelError(nameof(ScpFormViewModel.Model), "Model is required.");
        }

        if (form.UtcOffsetHours is null)
        {
            ModelState.AddModelError(nameof(ScpFormViewModel.UtcOffsetHours), "Time zone is required.");
        }

        if (!ModelState.IsValid)
        {
            return await FormViewAsync(nameof(Create), form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await scpService.CreateAsync(
            new CreateScpDto(
                form.SiteId!.Value,
                form.Name,
                form.ScpNumber!.Value,
                form.Model!.Value,
                form.ConnectionMode!.Value,
                form.SerialNumber,
                form.IpAddress,
                form.Port,
                form.Description,
                form.UtcOffsetHours!.Value),
            cancellationToken);

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return await FormViewAsync(nameof(Create), form, StatusFor(result.ErrorCode!), cancellationToken);
        }

        return Saved("Controller created. Its configuration is saved but not yet published.",
            Url.Action(nameof(Details), new { id = result.Value })!);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await scpService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var scp = result.Value!;
        var form = new ScpFormViewModel
        {
            SiteId = scp.SiteId,
            Name = scp.Name,
            ScpNumber = scp.ScpNumber,
            Model = scp.Model,
            ConnectionMode = scp.ConnectionMode,
            SerialNumber = scp.SerialNumber,
            IpAddress = scp.IpAddress,
            Port = scp.Port,
            Description = scp.Description,
            UtcOffsetHours = scp.Allocation.UtcOffsetHours,
            RowVersion = Convert.ToBase64String(scp.RowVersion),
        };

        return await FormViewAsync(nameof(Edit), form, StatusCodes.Status200OK, cancellationToken, scp);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, ScpFormViewModel form, CancellationToken cancellationToken)
    {
        if (!TryDecodeRowVersion(form.RowVersion, out var rowVersion))
        {
            ModelState.AddModelError(string.Empty, ScpErrors.ConcurrencyConflictMessage);
        }

        if (!ModelState.IsValid)
        {
            return await EditFormViewAsync(id, form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await scpService.UpdateAsync(
            id,
            new UpdateScpDto(
                form.SiteId!.Value,
                form.Name,
                form.ScpNumber!.Value,
                form.ConnectionMode!.Value,
                form.SerialNumber,
                form.IpAddress,
                form.Port,
                form.Description,
                rowVersion),
            cancellationToken);

        if (result.ErrorCode == ScpErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return await EditFormViewAsync(id, form, StatusFor(result.ErrorCode!), cancellationToken);
        }

        return Saved("Controller updated.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id })!);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await scpService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? FormView(nameof(Delete), ScpDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expectedRowVersion)
            ? await scpService.DeleteAsync(id, expectedRowVersion, cancellationToken)
            : Result.Failure(ScpErrors.ConcurrencyConflict, ScpErrors.ConcurrencyConflictMessage);

        if (result.ErrorCode == ScpErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            TempData[ErrorMessageKey] = result.ErrorMessage;
            if (!IsModalRequest)
            {
                return RedirectToAction(nameof(Delete), new { id });
            }

            // Re-render the confirmation with the current values and row version.
            var current = await scpService.GetByIdAsync(id, cancellationToken);
            return current.IsSuccess
                ? FormView(nameof(Delete), ScpDetailsViewModel.From(current.Value!), StatusCodes.Status409Conflict)
                : NotFound();
        }

        // The details page of a deleted record no longer exists, so always return to the list.
        return Saved("Controller deleted.", Url.Action(nameof(Index))!);
    }

    /// <summary>Re-renders the edit form with the read-only values (allocation, SCP number lock) reloaded.</summary>
    private async Task<IActionResult> EditFormViewAsync(Guid id, ScpFormViewModel form, int statusCode, CancellationToken cancellationToken)
    {
        var current = await scpService.GetByIdAsync(id, cancellationToken);
        if (current.IsFailure)
        {
            return NotFound();
        }

        form.Model = current.Value!.Model;
        form.UtcOffsetHours = current.Value.Allocation.UtcOffsetHours;
        return await FormViewAsync(nameof(Edit), form, statusCode, cancellationToken, current.Value);
    }

    private async Task<IActionResult> FormViewAsync(
        string action,
        ScpFormViewModel form,
        int statusCode,
        CancellationToken cancellationToken,
        ScpDto? existing = null)
    {
        await LoadOptionsAsync(form, cancellationToken);
        if (existing is not null)
        {
            form.IsEdit = true;
            form.CanChangeScpNumber = existing.CanChangeScpNumber;
            form.Allocation = existing.Allocation;
        }

        return FormView(action, form, statusCode);
    }

    private async Task LoadOptionsAsync(ScpFormViewModel form, CancellationToken cancellationToken)
    {
        var sites = await scpService.ListSiteOptionsAsync(cancellationToken);
        form.SiteOptions = sites.Value!;
    }

    private static int StatusFor(string errorCode) => errorCode switch
    {
        ScpErrors.ConcurrencyConflict or ScpErrors.DuplicateNumber or ScpErrors.DuplicateSerial
            or ScpErrors.DuplicateName or ScpErrors.NumberLocked
            or ScpErrors.DuplicateSioNumber or ScpErrors.DuplicateSioAddress or ScpErrors.DuplicateSioName
            or ScpErrors.SioLimit or ScpErrors.InternalSio => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity,
    };

    /// <summary>Shows the error next to the field it is about, or in the form summary.</summary>
    private void AddError(string errorCode, string errorMessage)
    {
        var key = errorCode switch
        {
            ScpErrors.DuplicateNumber or ScpErrors.NumberLocked => nameof(ScpFormViewModel.ScpNumber),
            ScpErrors.DuplicateSerial => nameof(ScpFormViewModel.SerialNumber),
            ScpErrors.DuplicateName => nameof(ScpFormViewModel.Name),
            ScpErrors.SiteNotFound => nameof(ScpFormViewModel.SiteId),
            ScpErrors.DuplicateSioNumber => nameof(AddSioFormViewModel.Number),
            ScpErrors.DuplicateSioAddress => nameof(AddSioFormViewModel.Address),
            ScpErrors.DuplicateSioName => nameof(AddSioFormViewModel.Name),
            _ => string.Empty,
        };

        ModelState.AddModelError(key, errorMessage);
    }
}
