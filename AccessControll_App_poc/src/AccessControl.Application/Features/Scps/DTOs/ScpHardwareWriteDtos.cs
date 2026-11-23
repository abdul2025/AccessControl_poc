using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Application.Features.Scps.DTOs;

// Every change to a controller's hardware layout carries the controller's row version: the whole layout is one
// configuration, so a stale edit of any part is rejected (code-style pattern 1).

public sealed record AddSioDto(short Number, SioModel Model, SioPort Port, short Address, string Name, byte[] RowVersion);

public sealed record UpdateSioDto(string Name, byte[] RowVersion);

public sealed record UpdateInputDto(string Name, InputContact Contact, bool Supervised, bool Monitored, byte[] RowVersion);

public sealed record UpdateOutputDto(string Name, short PulseSeconds, byte[] RowVersion);
