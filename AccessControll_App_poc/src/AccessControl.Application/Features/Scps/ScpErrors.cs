using ScpEntity = AccessControl.Domain.Entities.Scp;

namespace AccessControl.Application.Features.Scps;

public static class ScpErrors
{
    public const string NotFound = "Scp.NotFound";
    public const string Validation = ScpEntity.ValidationErrorCode;
    public const string NumberLocked = ScpEntity.NumberLockedErrorCode;
    public const string DuplicateNumber = "Scp.DuplicateNumber";
    public const string DuplicateSerial = "Scp.DuplicateSerial";
    public const string DuplicateName = "Scp.DuplicateName";
    public const string SiteNotFound = "Scp.SiteNotFound";
    public const string ConcurrencyConflict = "Scp.ConcurrencyConflict";
    public const string SioNotFound = ScpEntity.SioNotFoundErrorCode;
    public const string PointNotFound = ScpEntity.PointNotFoundErrorCode;
    public const string DuplicateSioNumber = ScpEntity.DuplicateSioNumberErrorCode;
    public const string DuplicateSioAddress = ScpEntity.DuplicateSioAddressErrorCode;
    public const string DuplicateSioName = ScpEntity.DuplicateSioNameErrorCode;
    public const string SioLimit = ScpEntity.SioLimitErrorCode;
    public const string InternalSio = ScpEntity.InternalSioErrorCode;
    public const string SioInUse = ScpEntity.SioInUseErrorCode;
    public const string ReaderNotFound = ScpEntity.ReaderNotFoundErrorCode;
    public const string DoorNotFound = ScpEntity.DoorNotFoundErrorCode;
    public const string DuplicateReaderNumber = ScpEntity.DuplicateReaderNumberErrorCode;
    public const string DuplicateReaderName = ScpEntity.DuplicateReaderNameErrorCode;
    public const string DuplicateDoorNumber = ScpEntity.DuplicateDoorNumberErrorCode;
    public const string DuplicateDoorName = ScpEntity.DuplicateDoorNameErrorCode;
    public const string ReaderInUse = ScpEntity.ReaderInUseErrorCode;
    public const string PointInUse = ScpEntity.PointInUseErrorCode;
    public const string DoorLimit = ScpEntity.DoorLimitErrorCode;

    public const string VersionNotFound = "Scp.VersionNotFound";

    public const string NotFoundMessage = "The controller was not found.";
    public const string VersionNotFoundMessage = "This configuration version does not exist. Publish the controller first.";
    public const string DuplicateNumberMessage = "Another controller already uses this SCP number.";
    public const string DuplicateSerialMessage = "Another controller already uses this serial number.";
    public const string DuplicateNameMessage = "Another controller on this site already uses this name.";
    public const string SiteNotFoundMessage = "The selected site does not exist.";
    public const string ConcurrencyConflictMessage =
        "This controller was changed by someone else after you opened it. Reload and try again.";
}
