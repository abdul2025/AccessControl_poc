using AccessControl.Application.Features.Scps;
using AccessControl.Application.Features.Scps.DTOs;
using AccessControl.Domain.Common.Results;
using AccessControl.Web.Features.Scps.Models;
using AccessControl.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AccessControl.Web.Features.Scps;

/// <summary>
/// Hardware layout of a controller: SIO panels and their inputs / outputs (code-style pattern 1 — child records
/// are changed only through the controller, and every form carries the controller's row version).
/// </summary>
public sealed partial class ScpsController
{
    [HttpGet]
    public async Task<IActionResult> AddSio(Guid id, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.IsFailure)
        {
            return NotFound();
        }

        var dto = scp.Value!;
        return FormView(nameof(AddSio), new AddSioFormViewModel
        {
            Number = dto.NextFreeSioNumber,
            Name = dto.NextFreeSioNumber is { } number ? $"SIO {number}" : string.Empty,
            RowVersion = Convert.ToBase64String(dto.RowVersion),
            ScpDisplayName = DisplayName(dto),
        });
    }

    [HttpPost]
    public async Task<IActionResult> AddSio(Guid id, AddSioFormViewModel form, CancellationToken cancellationToken)
    {
        var rowVersion = ReadRowVersion(form.RowVersion);
        if (!ModelState.IsValid)
        {
            return await HardwareFormViewAsync(id, nameof(AddSio), form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await scpService.AddSioAsync(
            id,
            new AddSioDto(form.Number!.Value, form.Model!.Value, form.Port!.Value, form.Address!.Value, form.Name, rowVersion),
            cancellationToken);

        return await HardwareResultAsync(id, nameof(AddSio), form, result, "SIO added.", cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> EditSio(Guid id, Guid sioId, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.Value?.SioPanels.FirstOrDefault(panel => panel.Id == sioId) is not { } panel)
        {
            return NotFound();
        }

        return FormView(nameof(EditSio), new EditSioFormViewModel
        {
            Name = panel.Name,
            RowVersion = Convert.ToBase64String(scp.Value.RowVersion),
            ScpDisplayName = DisplayName(scp.Value),
            HardwareSummary = HardwareSummary(panel),
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditSio(Guid id, Guid sioId, EditSioFormViewModel form, CancellationToken cancellationToken)
    {
        var rowVersion = ReadRowVersion(form.RowVersion);
        if (!ModelState.IsValid)
        {
            return await HardwareFormViewAsync(id, nameof(EditSio), form, StatusCodes.Status422UnprocessableEntity, cancellationToken, sioId);
        }

        var result = await scpService.UpdateSioAsync(id, sioId, new UpdateSioDto(form.Name, rowVersion), cancellationToken);
        return await HardwareResultAsync(id, nameof(EditSio), form, result, "SIO renamed.", cancellationToken, sioId);
    }

    [HttpGet]
    public async Task<IActionResult> RemoveSio(Guid id, Guid sioId, CancellationToken cancellationToken)
    {
        var model = await LoadRemoveSioAsync(id, sioId, cancellationToken);
        return model is null ? NotFound() : FormView(nameof(RemoveSio), model);
    }

    [HttpPost]
    [ActionName(nameof(RemoveSio))]
    public async Task<IActionResult> RemoveSioConfirmed(Guid id, Guid sioId, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expected)
            ? await scpService.RemoveSioAsync(id, sioId, expected, cancellationToken)
            : Result.Failure(ScpErrors.ConcurrencyConflict, ScpErrors.ConcurrencyConflictMessage);

        if (result.ErrorCode is ScpErrors.NotFound or ScpErrors.SioNotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            TempData[ErrorMessageKey] = result.ErrorMessage;
            if (!IsModalRequest)
            {
                return RedirectToAction(nameof(RemoveSio), new { id, sioId });
            }

            // Re-render the confirmation with the current values and row version.
            var current = await LoadRemoveSioAsync(id, sioId, cancellationToken);
            return current is null ? NotFound() : FormView(nameof(RemoveSio), current, StatusFor(result.ErrorCode!));
        }

        return Saved("SIO removed.", DetailsUrl(id, anchor: "sio-panels"));
    }

    [HttpGet]
    public async Task<IActionResult> EditInput(Guid id, Guid inputId, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        var match = scp.Value?.SioPanels
            .SelectMany(panel => panel.Inputs.Select(point => (Panel: panel, Point: point)))
            .FirstOrDefault(pair => pair.Point.Id == inputId);

        if (match is not { Point: { } point, Panel: { } panel })
        {
            return NotFound();
        }

        return FormView(nameof(EditInput), new InputFormViewModel
        {
            Name = point.Name,
            Contact = point.Contact,
            Supervised = point.Supervised,
            Monitored = point.Monitored,
            RowVersion = Convert.ToBase64String(scp.Value!.RowVersion),
            ScpDisplayName = DisplayName(scp.Value),
            PointDisplayName = $"SIO {panel.Number} · input {point.Number}",
            Dedicated = point.Dedicated,
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditInput(Guid id, Guid inputId, InputFormViewModel form, CancellationToken cancellationToken)
    {
        var rowVersion = ReadRowVersion(form.RowVersion);
        if (!ModelState.IsValid)
        {
            return await PointFormViewAsync(id, inputId, form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await scpService.UpdateInputAsync(
            id,
            inputId,
            new UpdateInputDto(form.Name, form.Contact!.Value, form.Supervised, form.Monitored, rowVersion),
            cancellationToken);

        if (result.ErrorCode is ScpErrors.NotFound or ScpErrors.PointNotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return await PointFormViewAsync(id, inputId, form, StatusFor(result.ErrorCode!), cancellationToken);
        }

        return Saved("Input updated.", DetailsUrl(id, anchor: "sio-panels"));
    }

    [HttpGet]
    public async Task<IActionResult> EditOutput(Guid id, Guid outputId, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        var match = scp.Value?.SioPanels
            .SelectMany(panel => panel.Outputs.Select(point => (Panel: panel, Point: point)))
            .FirstOrDefault(pair => pair.Point.Id == outputId);

        if (match is not { Point: { } point, Panel: { } panel })
        {
            return NotFound();
        }

        return FormView(nameof(EditOutput), new OutputFormViewModel
        {
            Name = point.Name,
            PulseSeconds = point.PulseSeconds,
            RowVersion = Convert.ToBase64String(scp.Value!.RowVersion),
            ScpDisplayName = DisplayName(scp.Value),
            PointDisplayName = $"SIO {panel.Number} · output {point.Number}",
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditOutput(Guid id, Guid outputId, OutputFormViewModel form, CancellationToken cancellationToken)
    {
        var rowVersion = ReadRowVersion(form.RowVersion);
        if (!ModelState.IsValid)
        {
            return await OutputFormViewAsync(id, outputId, form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await scpService.UpdateOutputAsync(
            id,
            outputId,
            new UpdateOutputDto(form.Name, form.PulseSeconds!.Value, rowVersion),
            cancellationToken);

        if (result.ErrorCode is ScpErrors.NotFound or ScpErrors.PointNotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return await OutputFormViewAsync(id, outputId, form, StatusFor(result.ErrorCode!), cancellationToken);
        }

        return Saved("Output updated.", DetailsUrl(id, anchor: "sio-panels"));
    }

    // ---- helpers -------------------------------------------------------------------------------------------

    private static string DisplayName(ScpDto scp) => $"SCP {scp.ScpNumber} — {scp.Name}";

    private static string HardwareSummary(SioPanelDto panel) =>
        $"{ScpLabels.ToLabel(panel.Model)} · {ScpLabels.ToLabel(panel.Port)} · address {panel.Address}";

    private string DetailsUrl(Guid id, string anchor) => $"{Url.Action(nameof(Details), new { id })}#{anchor}";

    /// <summary>A missing or malformed row version is reported as a concurrency problem (the form was not loaded properly).</summary>
    private byte[] ReadRowVersion(string? value)
    {
        if (TryDecodeRowVersion(value, out var rowVersion))
        {
            return rowVersion;
        }

        ModelState.AddModelError(string.Empty, ScpErrors.ConcurrencyConflictMessage);
        return [];
    }

    private async Task<RemoveSioViewModel?> LoadRemoveSioAsync(Guid id, Guid sioId, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.Value?.SioPanels.FirstOrDefault(panel => panel.Id == sioId) is not { } panel)
        {
            return null;
        }

        return new RemoveSioViewModel(
            id, sioId, DisplayName(scp.Value), $"SIO {panel.Number} — {panel.Name}", HardwareSummary(panel),
            Convert.ToBase64String(scp.Value.RowVersion));
    }

    private async Task<IActionResult> HardwareResultAsync<TForm>(
        Guid id,
        string action,
        TForm form,
        Result result,
        string successMessage,
        CancellationToken cancellationToken,
        Guid? sioId = null)
        where TForm : class
    {
        if (result.ErrorCode is ScpErrors.NotFound or ScpErrors.SioNotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return await HardwareFormViewAsync(id, action, form, StatusFor(result.ErrorCode!), cancellationToken, sioId);
        }

        return Saved(successMessage, DetailsUrl(id, anchor: "sio-panels"));
    }

    /// <summary>Re-renders an SIO form with its display-only values reloaded.</summary>
    private async Task<IActionResult> HardwareFormViewAsync<TForm>(
        Guid id,
        string action,
        TForm form,
        int statusCode,
        CancellationToken cancellationToken,
        Guid? sioId = null)
        where TForm : class
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.IsFailure)
        {
            return NotFound();
        }

        switch (form)
        {
            case AddSioFormViewModel add:
                add.ScpDisplayName = DisplayName(scp.Value!);
                break;
            case EditSioFormViewModel edit:
                if (scp.Value!.SioPanels.FirstOrDefault(panel => panel.Id == sioId) is not { } panel)
                {
                    return NotFound();
                }

                edit.ScpDisplayName = DisplayName(scp.Value);
                edit.HardwareSummary = HardwareSummary(panel);
                break;
        }

        return FormView(action, form, statusCode);
    }

    private async Task<IActionResult> PointFormViewAsync(Guid id, Guid inputId, InputFormViewModel form, int statusCode, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        var match = scp.Value?.SioPanels
            .SelectMany(panel => panel.Inputs.Select(point => (Panel: panel, Point: point)))
            .FirstOrDefault(pair => pair.Point.Id == inputId);

        if (match is not { Point: { } point, Panel: { } panel })
        {
            return NotFound();
        }

        form.ScpDisplayName = DisplayName(scp.Value!);
        form.PointDisplayName = $"SIO {panel.Number} · input {point.Number}";
        form.Dedicated = point.Dedicated;
        return FormView(nameof(EditInput), form, statusCode);
    }

    private async Task<IActionResult> OutputFormViewAsync(Guid id, Guid outputId, OutputFormViewModel form, int statusCode, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        var match = scp.Value?.SioPanels
            .SelectMany(panel => panel.Outputs.Select(point => (Panel: panel, Point: point)))
            .FirstOrDefault(pair => pair.Point.Id == outputId);

        if (match is not { Point: { } point, Panel: { } panel })
        {
            return NotFound();
        }

        form.ScpDisplayName = DisplayName(scp.Value!);
        form.PointDisplayName = $"SIO {panel.Number} · output {point.Number}";
        return FormView(nameof(EditOutput), form, statusCode);
    }
}
