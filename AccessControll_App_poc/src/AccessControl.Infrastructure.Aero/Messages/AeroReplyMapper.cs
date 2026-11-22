using HID.Aero.ScpdNet.Wrapper;

namespace AccessControl.Infrastructure.Aero.Messages;

/// <summary>The only place that reads wrapper reply fields; everything downstream uses <see cref="AeroReply"/>.</summary>
internal static class AeroReplyMapper
{
    public static AeroReply Map(SCPReplyMessage message, DateTimeOffset receivedAtUtc)
    {
        var scp = message.SCPId;

        return message.ReplyType switch
        {
            (int)enSCPReplyType.enSCPReplyCommStatus => new CommStatusReply(
                scp,
                receivedAtUtc,
                message.comm.nChannelId,
                message.comm.status,
                (int)message.comm.error_code,
                message.comm.current_primary_comm,
                message.comm.previous_primary_comm),

            (int)enSCPReplyType.enSCPReplyIDReport => new IdReportReply(
                scp,
                receivedAtUtc,
                message.id.device_id,
                message.id.device_ver,
                $"{message.id.sft_rev_major}.{message.id.sft_rev_minor}",
                message.id.serial_number,
                FormatMac(message.id.mac_addr),
                message.id.scp_id,
                DateTimeOffset.FromUnixTimeSeconds(message.id.e_sec),
                message.id.db_max,
                message.id.db_active,
                message.id.dip_switch_pwrup,
                message.id.dip_switch_current,
                message.id.tls_status,
                message.id.firmware_advisory,
                message.id.nOemCode),

            (int)enSCPReplyType.enSCPReplyCmndStatus => new CommandStatusReply(
                scp,
                receivedAtUtc,
                message.cmnd_sts.sequence_number,
                message.cmnd_sts.status),

            (int)enSCPReplyType.enSCPReplyNAK => new NakReply(
                scp,
                receivedAtUtc,
                message.nak.reason,
                message.nak.data,
                message.nak.description_code,
                message.nak.command is { } bytes ? Convert.ToHexString(bytes, 0, Math.Min(bytes.Length, 20)) : string.Empty),

            (int)enSCPReplyType.enSCPReplyTransaction => new TransactionReply(
                scp,
                receivedAtUtc,
                message.tran.ser_num,
                DateTimeOffset.FromUnixTimeSeconds((uint)message.tran.time),
                message.tran.source_type,
                message.tran.source_number,
                message.tran.tran_type,
                message.tran.tran_code),

            _ => new OtherReply(scp, receivedAtUtc, message.ReplyType),
        };
    }

    private static string FormatMac(byte[]? bytes) =>
        bytes is { Length: > 0 } ? string.Join(':', bytes.Take(6).Select(value => value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture))) : string.Empty;
}
