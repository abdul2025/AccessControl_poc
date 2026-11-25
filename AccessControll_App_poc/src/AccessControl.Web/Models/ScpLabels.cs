using AccessControl.Domain.Enums.Scps;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccessControl.Web.Models;

/// <summary>Display text, CSS modifiers and select options for controller enums — views never ToString() an enum.</summary>
public static class ScpLabels
{
    public static string ToLabel(ConnectionMode mode) => mode switch
    {
        ConnectionMode.IpClient => "IP Client",
        ConnectionMode.IpServer => "IP Server",
        _ => mode.ToString(),
    };

    public static string ToHint(ConnectionMode mode) => mode switch
    {
        ConnectionMode.IpClient => "The controller connects to the Gateway and is recognised by its serial number (recommended).",
        ConnectionMode.IpServer => "The Gateway connects to the controller at its IP address (lab use).",
        _ => string.Empty,
    };

    public static string ToLabel(ScpSyncStatus status) => status switch
    {
        ScpSyncStatus.NotPublished => "Not published",
        ScpSyncStatus.Pending => "Pending",
        ScpSyncStatus.Applying => "Applying",
        ScpSyncStatus.InSync => "In sync",
        ScpSyncStatus.Failed => "Failed",
        _ => status.ToString(),
    };

    public static string ToCssModifier(ScpSyncStatus status) => status switch
    {
        ScpSyncStatus.InSync => "success",
        ScpSyncStatus.Pending or ScpSyncStatus.Applying => "info",
        ScpSyncStatus.Failed => "danger",
        _ => "neutral",
    };

    public static string ToLabel(ScpApplyOutcome outcome) => outcome switch
    {
        ScpApplyOutcome.Running => "Running",
        ScpApplyOutcome.Succeeded => "Applied",
        ScpApplyOutcome.UpToDate => "Already up to date",
        ScpApplyOutcome.Failed => "Failed",
        ScpApplyOutcome.Interrupted => "Interrupted (connection lost)",
        _ => outcome.ToString(),
    };

    public static string ToCssModifier(ScpApplyOutcome outcome) => outcome switch
    {
        ScpApplyOutcome.Succeeded or ScpApplyOutcome.UpToDate => "success",
        ScpApplyOutcome.Running => "info",
        ScpApplyOutcome.Failed => "danger",
        ScpApplyOutcome.Interrupted => "warning",
        _ => "neutral",
    };

    public static string ToLabel(ScpCommandResult result) => result switch
    {
        ScpCommandResult.Ok => "OK",
        ScpCommandResult.Nak => "Rejected (NAK)",
        ScpCommandResult.NoReply => "No reply",
        ScpCommandResult.PostRejected => "Not queued by the driver",
        _ => result.ToString(),
    };

    public static string ToCssModifier(ScpCommandResult result) => result == ScpCommandResult.Ok ? "success" : "danger";

    public static string ToLabel(ScpModel model) => model switch
    {
        ScpModel.X1100 => "HID Aero X1100",
        _ => model.ToString(),
    };

    public static string ToLabel(SioModel model) => model switch
    {
        SioModel.X1100Internal => "X1100 internal (196)",
        SioModel.X100 => "X100 (193)",
        SioModel.X200 => "X200 (194)",
        SioModel.X300 => "X300 (195)",
        _ => model.ToString(),
    };

    public static string ToLabel(SioPort port) => port switch
    {
        SioPort.Internal => "Internal",
        SioPort.Rs485Port1 => "RS-485 port 1",
        SioPort.Rs485Port2 => "RS-485 port 2",
        _ => port.ToString(),
    };

    public static string ToLabel(InputContact contact) => contact switch
    {
        InputContact.NormallyClosed => "Normally closed",
        InputContact.NormallyOpen => "Normally open",
        _ => contact.ToString(),
    };

    public static string ToShortLabel(InputContact contact) => contact switch
    {
        InputContact.NormallyClosed => "NC",
        InputContact.NormallyOpen => "NO",
        _ => contact.ToString(),
    };

    public static string ToLabel(DedicatedInput dedicated) => dedicated switch
    {
        DedicatedInput.None => "General input",
        DedicatedInput.Tamper => "Enclosure tamper",
        DedicatedInput.AcFail => "Power supply AC fail",
        DedicatedInput.BatteryFail => "Power supply battery fail",
        _ => dedicated.ToString(),
    };

    public static IEnumerable<SelectListItem> ContactOptions() =>
        Enum.GetValues<InputContact>().Select(contact => new SelectListItem(ToLabel(contact), contact.ToString()));

    /// <summary>Models that can be added as external SIOs (the internal one comes with the controller).</summary>
    public static IEnumerable<SelectListItem> ExternalSioModelOptions() =>
        new[] { SioModel.X100, SioModel.X200, SioModel.X300 }
            .Select(model => new SelectListItem($"{ToLabel(model)} — {SioCapacity(model)}", model.ToString()));

    public static IEnumerable<SelectListItem> Rs485PortOptions() =>
        new[] { SioPort.Rs485Port1, SioPort.Rs485Port2 }.Select(port => new SelectListItem(ToLabel(port), port.ToString()));

    /// <summary>"7 inputs · 4 relays · 2 reader ports" from the hardware catalog.</summary>
    public static string SioCapacity(SioModel model)
    {
        var spec = AccessControl.Domain.Catalog.HardwareCatalog.For(model);
        var readers = spec.ReaderPorts > 0 ? $" · {spec.ReaderPorts} reader ports" : string.Empty;
        return $"{spec.Inputs} inputs · {spec.Outputs} relays{readers}";
    }

    public static string ToLabel(ReaderInterface readerInterface) => readerInterface switch
    {
        ReaderInterface.Osdp => "OSDP (RS-485)",
        ReaderInterface.Wiegand => "Wiegand",
        _ => readerInterface.ToString(),
    };

    public static string ToLabel(OsdpBaudRate baudRate) => baudRate switch
    {
        OsdpBaudRate.Baud9600 => "9600",
        OsdpBaudRate.Baud19200 => "19200",
        OsdpBaudRate.Baud38400 => "38400",
        OsdpBaudRate.Baud57600 => "57600",
        OsdpBaudRate.Baud115200 => "115200",
        OsdpBaudRate.Baud230400 => "230400",
        _ => baudRate.ToString(),
    };

    public static string ToLabel(DoorAccessMode mode) => mode switch
    {
        DoorAccessMode.CardOnly => "Card only",
        DoorAccessMode.CardOrPin => "Card or PIN",
        DoorAccessMode.CardAndPin => "Card and PIN",
        DoorAccessMode.Locked => "Locked (no access)",
        DoorAccessMode.Unlocked => "Unlocked (free access)",
        _ => mode.ToString(),
    };

    public static IEnumerable<SelectListItem> ReaderInterfaceOptions() =>
        Enum.GetValues<ReaderInterface>().Select(value => new SelectListItem(ToLabel(value), value.ToString()));

    /// <summary>Baud rates in ascending speed (the enum is ordered by SDK code, not speed).</summary>
    public static IEnumerable<SelectListItem> BaudRateOptions() =>
        new[] { OsdpBaudRate.Baud9600, OsdpBaudRate.Baud19200, OsdpBaudRate.Baud38400, OsdpBaudRate.Baud57600, OsdpBaudRate.Baud115200, OsdpBaudRate.Baud230400 }
            .Select(value => new SelectListItem(ToLabel(value), value.ToString()));

    public static IEnumerable<SelectListItem> AccessModeOptions() =>
        new[] { DoorAccessMode.CardOnly, DoorAccessMode.CardOrPin, DoorAccessMode.CardAndPin, DoorAccessMode.Locked, DoorAccessMode.Unlocked }
            .Select(value => new SelectListItem(ToLabel(value), value.ToString()));

    public static IEnumerable<SelectListItem> ConnectionModeOptions() =>
        Enum.GetValues<ConnectionMode>().Select(mode => new SelectListItem(ToLabel(mode), mode.ToString()));

    public static IEnumerable<SelectListItem> ModelOptions() =>
        Enum.GetValues<ScpModel>().Select(model => new SelectListItem(ToLabel(model), model.ToString()));

    /// <summary>"UTC+03:00" from an offset in hours.</summary>
    public static string ToUtcOffsetLabel(double hours)
    {
        var sign = hours < 0 ? "-" : "+";
        var span = TimeSpan.FromHours(Math.Abs(hours));
        return $"UTC{sign}{span.Hours:00}:{span.Minutes:00}";
    }
}
