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
