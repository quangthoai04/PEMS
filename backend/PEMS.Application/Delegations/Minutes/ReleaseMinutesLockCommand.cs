using MediatR;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>Release the edit lock without saving (Hủy chỉnh sửa). No-op if the caller doesn't hold it.</summary>
public sealed record ReleaseMinutesLockCommand(ulong MinutesId, string EditLockToken) : IRequest<MinuteDto>;
