using AccessControl.Application.Features.Scps;
using AccessControl.Domain.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace AccessControl.Web.Features.Scps;

/// <summary>
/// Publishing (spec §3.7 rule 1): freezes the current configuration as a new version for the Gateway to apply, and shows
/// the SDK commands the Gateway compiled for it. The Web still never talks to the controller.
/// </summary>
public sealed partial class ScpsController
{
    [HttpPost]
    public async Task<IActionResult> Publish(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        Result<int> result = TryDecodeRowVersion(rowVersion, out var expected)
            ? await scpService.PublishAsync(id, expected, cancellationToken)
            : Result<int>.Failure(ScpErrors.ConcurrencyConflict, ScpErrors.ConcurrencyConflictMessage);

        if (result.ErrorCode == ScpErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            TempData[ErrorMessageKey] = result.ErrorMessage;
        }
        else
        {
            TempData[StatusMessageKey] =
                $"Version {result.Value} published. The Gateway applies it the next time the controller is connected.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Commands(Guid id, int? version, CancellationToken cancellationToken)
    {
        var result = await scpService.GetCommandsAsync(id, version, cancellationToken);
        return result.IsSuccess ? View(result.Value!) : NotFound();
    }
}
