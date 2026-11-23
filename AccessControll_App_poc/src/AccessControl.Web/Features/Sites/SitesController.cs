using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Sites;
using AccessControl.Application.Features.Sites.DTOs;
using AccessControl.Application.Features.Sites.Interfaces;
using AccessControl.Domain.Common.Results;
using AccessControl.Web.Controllers;
using AccessControl.Web.Features.Sites.Models;
using Microsoft.AspNetCore.Mvc;

namespace AccessControl.Web.Features.Sites;

public sealed class SitesController(ISiteService siteService) : ModalFormController
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var filter = new SiteListFilter(search);
        var list = await siteService.ListAsync(filter, new PagedRequest(page), cancellationToken);

        return View(new SiteListViewModel(list.Value!, filter));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await siteService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? View(SiteDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpGet]
    public IActionResult Create() => FormView(nameof(Create), new SiteFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(SiteFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return FormView(nameof(Create), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await siteService.CreateAsync(new CreateSiteDto(form.Code, form.Name, form.Description), cancellationToken);
        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return FormView(nameof(Create), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Site created.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id = result.Value })!);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await siteService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var site = result.Value!;
        return FormView(nameof(Edit), new SiteFormViewModel
        {
            Code = site.Code,
            Name = site.Name,
            Description = site.Description,
            RowVersion = Convert.ToBase64String(site.RowVersion),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, SiteFormViewModel form, CancellationToken cancellationToken)
    {
        if (!TryDecodeRowVersion(form.RowVersion, out var rowVersion))
        {
            ModelState.AddModelError(string.Empty, SiteErrors.ConcurrencyConflictMessage);
        }

        if (!ModelState.IsValid)
        {
            return FormView(nameof(Edit), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await siteService.UpdateAsync(
            id,
            new UpdateSiteDto(form.Code, form.Name, form.Description, rowVersion),
            cancellationToken);

        if (result.ErrorCode == SiteErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return FormView(nameof(Edit), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Site updated.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id })!);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await siteService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? FormView(nameof(Delete), SiteDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expectedRowVersion)
            ? await siteService.DeleteAsync(id, expectedRowVersion, cancellationToken)
            : Result.Failure(SiteErrors.ConcurrencyConflict, SiteErrors.ConcurrencyConflictMessage);

        if (result.ErrorCode == SiteErrors.NotFound)
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
            var current = await siteService.GetByIdAsync(id, cancellationToken);
            return current.IsSuccess
                ? FormView(nameof(Delete), SiteDetailsViewModel.From(current.Value!), StatusCodes.Status409Conflict)
                : NotFound();
        }

        // The details page of a deleted record no longer exists, so always return to the list.
        return Saved("Site deleted.", Url.Action(nameof(Index))!);
    }

    private static int StatusFor(string errorCode) => errorCode switch
    {
        SiteErrors.ConcurrencyConflict or SiteErrors.DuplicateCode or SiteErrors.DuplicateName or SiteErrors.HasScps
            => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity,
    };

    /// <summary>Shows the error next to the field it is about, or in the form summary.</summary>
    private void AddError(string errorCode, string errorMessage)
    {
        var key = errorCode switch
        {
            SiteErrors.DuplicateCode => nameof(SiteFormViewModel.Code),
            SiteErrors.DuplicateName => nameof(SiteFormViewModel.Name),
            _ => string.Empty,
        };

        ModelState.AddModelError(key, errorMessage);
    }
}
