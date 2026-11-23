using AccessControl.Application.Features.Scps;
using AccessControl.Application.Features.Scps.DTOs;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Enums.Scps;
using AccessControl.Web.Features.Scps.Models;
using AccessControl.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AccessControl.Web.Features.Scps;

/// <summary>Readers and doors of a controller (child records of the controller aggregate, code-style pattern 1).</summary>
public sealed partial class ScpsController
{
    // ---- Readers --------------------------------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> AddReader(Guid id, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.IsFailure)
        {
            return NotFound();
        }

        var form = new ReaderFormViewModel { RowVersion = Convert.ToBase64String(scp.Value!.RowVersion) };
        FillReaderForm(form, scp.Value, "Add reader");
        form.SioPanelId = form.SioOptions.FirstOrDefault()?.Id;
        return FormView(nameof(AddReader), form);
    }

    [HttpPost]
    public async Task<IActionResult> AddReader(Guid id, ReaderFormViewModel form, CancellationToken cancellationToken)
    {
        var rowVersion = ReadRowVersion(form.RowVersion);
        if (!ModelState.IsValid)
        {
            return await ReaderFormAsync(id, nameof(AddReader), "Add reader", form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await scpService.AddReaderAsync(id, ToDto(form, rowVersion), cancellationToken);
        return await ChildResultAsync(id, result, "Reader added.", "readers",
            () => ReaderFormAsync(id, nameof(AddReader), "Add reader", form, StatusFor(result.ErrorCode!), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> EditReader(Guid id, Guid readerId, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.Value?.Readers.FirstOrDefault(reader => reader.Id == readerId) is not { } reader)
        {
            return NotFound();
        }

        var form = new ReaderFormViewModel
        {
            SioPanelId = reader.SioPanelId,
            Number = reader.Number,
            Name = reader.Name,
            Model = reader.Model,
            Interface = reader.Interface,
            OsdpAddress = reader.OsdpAddress,
            OsdpBaudRate = reader.OsdpBaudRate,
            SecureChannel = reader.SecureChannel,
            HasKeypad = reader.HasKeypad,
            RowVersion = Convert.ToBase64String(scp.Value.RowVersion),
        };
        FillReaderForm(form, scp.Value, "Edit reader");
        return FormView(nameof(EditReader), form);
    }

    [HttpPost]
    public async Task<IActionResult> EditReader(Guid id, Guid readerId, ReaderFormViewModel form, CancellationToken cancellationToken)
    {
        var rowVersion = ReadRowVersion(form.RowVersion);
        if (!ModelState.IsValid)
        {
            return await ReaderFormAsync(id, nameof(EditReader), "Edit reader", form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await scpService.UpdateReaderAsync(id, readerId, ToDto(form, rowVersion), cancellationToken);
        return await ChildResultAsync(id, result, "Reader updated.", "readers",
            () => ReaderFormAsync(id, nameof(EditReader), "Edit reader", form, StatusFor(result.ErrorCode!), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> RemoveReader(Guid id, Guid readerId, CancellationToken cancellationToken)
    {
        var model = await LoadRemoveReaderAsync(id, readerId, cancellationToken);
        return model is null ? NotFound() : FormView(nameof(RemoveReader), model);
    }

    [HttpPost]
    [ActionName(nameof(RemoveReader))]
    public async Task<IActionResult> RemoveReaderConfirmed(Guid id, Guid readerId, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expected)
            ? await scpService.RemoveReaderAsync(id, readerId, expected, cancellationToken)
            : Result.Failure(ScpErrors.ConcurrencyConflict, ScpErrors.ConcurrencyConflictMessage);

        return await RemoveResultAsync(id, result, "Reader removed.", "readers", nameof(RemoveReader),
            () => LoadRemoveReaderAsync(id, readerId, cancellationToken), new { id, readerId });
    }

    // ---- Doors ----------------------------------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> AddDoor(Guid id, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.IsFailure)
        {
            return NotFound();
        }

        var dto = scp.Value!;
        var form = new DoorFormViewModel
        {
            Number = dto.NextFreeDoorNumber,
            Name = dto.NextFreeDoorNumber is { } number ? $"Door {number}" : string.Empty,
            RowVersion = Convert.ToBase64String(dto.RowVersion),
        };
        FillDoorForm(form, dto, "Add door", editingDoorId: null);
        return FormView(nameof(AddDoor), form);
    }

    [HttpPost]
    public async Task<IActionResult> AddDoor(Guid id, DoorFormViewModel form, CancellationToken cancellationToken)
    {
        var rowVersion = ReadRowVersion(form.RowVersion);
        if (!ModelState.IsValid)
        {
            return await DoorFormAsync(id, nameof(AddDoor), "Add door", null, form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await scpService.AddDoorAsync(id, ToDto(form, rowVersion), cancellationToken);
        return await ChildResultAsync(id, result, "Door added.", "doors",
            () => DoorFormAsync(id, nameof(AddDoor), "Add door", null, form, StatusFor(result.ErrorCode!), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> EditDoor(Guid id, Guid doorId, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.Value?.Doors.FirstOrDefault(door => door.Id == doorId) is not { } door)
        {
            return NotFound();
        }

        var form = new DoorFormViewModel
        {
            Number = door.Number,
            Name = door.Name,
            EntryReaderId = door.EntryReaderId,
            ExitReaderId = door.ExitReaderId,
            LockOutputId = door.LockOutputId,
            DoorContactInputId = door.DoorContactInputId,
            ExitButtonInputId = door.ExitButtonInputId,
            StrikeMinSeconds = door.StrikeMinSeconds,
            StrikeMaxSeconds = door.StrikeMaxSeconds,
            HeldOpenSeconds = door.HeldOpenSeconds,
            AccessMode = door.AccessMode,
            RowVersion = Convert.ToBase64String(scp.Value.RowVersion),
        };
        FillDoorForm(form, scp.Value, "Edit door", doorId);
        return FormView(nameof(EditDoor), form);
    }

    [HttpPost]
    public async Task<IActionResult> EditDoor(Guid id, Guid doorId, DoorFormViewModel form, CancellationToken cancellationToken)
    {
        var rowVersion = ReadRowVersion(form.RowVersion);
        if (!ModelState.IsValid)
        {
            return await DoorFormAsync(id, nameof(EditDoor), "Edit door", doorId, form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await scpService.UpdateDoorAsync(id, doorId, ToDto(form, rowVersion), cancellationToken);
        return await ChildResultAsync(id, result, "Door updated.", "doors",
            () => DoorFormAsync(id, nameof(EditDoor), "Edit door", doorId, form, StatusFor(result.ErrorCode!), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> RemoveDoor(Guid id, Guid doorId, CancellationToken cancellationToken)
    {
        var model = await LoadRemoveDoorAsync(id, doorId, cancellationToken);
        return model is null ? NotFound() : FormView(nameof(RemoveDoor), model);
    }

    [HttpPost]
    [ActionName(nameof(RemoveDoor))]
    public async Task<IActionResult> RemoveDoorConfirmed(Guid id, Guid doorId, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expected)
            ? await scpService.RemoveDoorAsync(id, doorId, expected, cancellationToken)
            : Result.Failure(ScpErrors.ConcurrencyConflict, ScpErrors.ConcurrencyConflictMessage);

        return await RemoveResultAsync(id, result, "Door removed.", "doors", nameof(RemoveDoor),
            () => LoadRemoveDoorAsync(id, doorId, cancellationToken), new { id, doorId });
    }

    // ---- helpers -------------------------------------------------------------------------------------------

    private static ReaderWriteDto ToDto(ReaderFormViewModel form, byte[] rowVersion) => new(
        form.SioPanelId!.Value,
        form.Number!.Value,
        form.Name,
        form.Model,
        form.Interface!.Value,
        form.OsdpAddress ?? 0,
        form.OsdpBaudRate ?? OsdpBaudRate.Baud9600,
        form.SecureChannel,
        form.HasKeypad,
        rowVersion);

    private static DoorWriteDto ToDto(DoorFormViewModel form, byte[] rowVersion) => new(
        form.Number!.Value,
        form.Name,
        form.EntryReaderId!.Value,
        form.ExitReaderId,
        form.LockOutputId!.Value,
        form.DoorContactInputId,
        form.ExitButtonInputId,
        form.StrikeMinSeconds!.Value,
        form.StrikeMaxSeconds!.Value,
        form.HeldOpenSeconds!.Value,
        form.AccessMode!.Value,
        rowVersion);

    private static void FillReaderForm(ReaderFormViewModel form, ScpDto scp, string title)
    {
        form.ScpDisplayName = DisplayName(scp);
        form.Title = title;
        form.SioOptions = scp.SioPanels
            .Where(panel => panel.ReaderPorts > 0)
            .Select(panel => new OptionItem(panel.Id, $"SIO {panel.Number} — {panel.Name} ({ScpLabels.ToLabel(panel.Model)})"))
            .ToList();
    }

    /// <summary>Only readers and points that are still free (or used by the door being edited) are offered.</summary>
    private static void FillDoorForm(DoorFormViewModel form, ScpDto scp, string title, Guid? editingDoorId)
    {
        form.ScpDisplayName = DisplayName(scp);
        form.Title = title;

        var otherDoors = scp.Doors.Where(door => door.Id != editingDoorId).ToList();
        var usedReaders = otherDoors.SelectMany(door => door.ExitReaderId is { } exit ? [door.EntryReaderId, exit] : new[] { door.EntryReaderId }).ToHashSet();
        var usedRelays = otherDoors.Select(door => door.LockOutputId).ToHashSet();
        var usedInputs = otherDoors
            .SelectMany(door => new[] { door.DoorContactInputId, door.ExitButtonInputId })
            .OfType<Guid>()
            .ToHashSet();

        form.ReaderOptions = scp.Readers
            .Where(reader => !usedReaders.Contains(reader.Id))
            .Select(reader => new OptionItem(reader.Id, $"{reader.Name} (SIO {reader.SioNumber}, reader {reader.Number})"))
            .ToList();
        form.RelayOptions = scp.SioPanels
            .SelectMany(panel => panel.Outputs.Select(point => (Panel: panel, Point: point)))
            .Where(pair => !usedRelays.Contains(pair.Point.Id))
            .Select(pair => new OptionItem(pair.Point.Id, $"SIO {pair.Panel.Number} · {pair.Point.Name}"))
            .ToList();
        form.InputOptions = scp.SioPanels
            .SelectMany(panel => panel.Inputs.Select(point => (Panel: panel, Point: point)))
            .Where(pair => pair.Point.Dedicated == DedicatedInput.None && !usedInputs.Contains(pair.Point.Id))
            .Select(pair => new OptionItem(pair.Point.Id, $"SIO {pair.Panel.Number} · {pair.Point.Name} ({ScpLabels.ToShortLabel(pair.Point.Contact)})"))
            .ToList();
    }

    private async Task<IActionResult> ReaderFormAsync(Guid id, string action, string title, ReaderFormViewModel form, int statusCode, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.IsFailure)
        {
            return NotFound();
        }

        FillReaderForm(form, scp.Value!, title);
        return FormView(action, form, statusCode);
    }

    private async Task<IActionResult> DoorFormAsync(
        Guid id,
        string action,
        string title,
        Guid? doorId,
        DoorFormViewModel form,
        int statusCode,
        CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.IsFailure)
        {
            return NotFound();
        }

        FillDoorForm(form, scp.Value!, title, doorId);
        return FormView(action, form, statusCode);
    }

    private async Task<IActionResult> ChildResultAsync(Guid id, Result result, string successMessage, string anchor, Func<Task<IActionResult>> reRender)
    {
        if (result.ErrorCode is ScpErrors.NotFound or ScpErrors.ReaderNotFound or ScpErrors.DoorNotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return await reRender();
        }

        return Saved(successMessage, DetailsUrl(id, anchor));
    }

    private async Task<IActionResult> RemoveResultAsync(
        Guid id,
        Result result,
        string successMessage,
        string anchor,
        string action,
        Func<Task<RemoveItemViewModel?>> reload,
        object routeValues)
    {
        if (result.ErrorCode is ScpErrors.NotFound or ScpErrors.ReaderNotFound or ScpErrors.DoorNotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            TempData[ErrorMessageKey] = result.ErrorMessage;
            if (!IsModalRequest)
            {
                return RedirectToAction(action, routeValues);
            }

            var current = await reload();
            return current is null ? NotFound() : FormView(action, current, StatusFor(result.ErrorCode!));
        }

        return Saved(successMessage, DetailsUrl(id, anchor));
    }

    private async Task<RemoveItemViewModel?> LoadRemoveReaderAsync(Guid id, Guid readerId, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.Value?.Readers.FirstOrDefault(reader => reader.Id == readerId) is not { } reader)
        {
            return null;
        }

        var door = scp.Value.Doors.FirstOrDefault(item => item.EntryReaderId == readerId || item.ExitReaderId == readerId);
        return new RemoveItemViewModel(
            id,
            DisplayName(scp.Value),
            $"Remove reader {reader.Name}?",
            $"SIO {reader.SioNumber}, reader {reader.Number} · {reader.Model} · {ScpLabels.ToLabel(reader.Interface)}",
            door is null ? null : $"Door {door.Number} — {door.Name} uses this reader. Remove the door or choose another reader first.",
            Convert.ToBase64String(scp.Value.RowVersion));
    }

    private async Task<RemoveItemViewModel?> LoadRemoveDoorAsync(Guid id, Guid doorId, CancellationToken cancellationToken)
    {
        var scp = await scpService.GetByIdAsync(id, cancellationToken);
        if (scp.Value?.Doors.FirstOrDefault(door => door.Id == doorId) is not { } door)
        {
            return null;
        }

        return new RemoveItemViewModel(
            id,
            DisplayName(scp.Value),
            $"Remove door {door.Number} — {door.Name}?",
            $"Reader {door.EntryReaderName} · lock {door.LockOutputName}. Its reader and points become free for other doors.",
            null,
            Convert.ToBase64String(scp.Value.RowVersion));
    }
}
