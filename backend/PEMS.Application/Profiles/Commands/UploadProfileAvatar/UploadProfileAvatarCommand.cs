using MediatR;

namespace PEMS.Application.Profiles.Commands.UploadProfileAvatar;

/// <summary>
/// UC-15 — replace the current user's avatar. The caller is resolved from the JWT
/// (<see cref="PEMS.Application.Common.Interfaces.ICurrentUserService"/>); a user can only ever
/// change their OWN avatar, so no userId is accepted from the client. The bytes are buffered by the
/// controller so the handler can both sniff magic bytes and upload without re-reading a stream.
/// </summary>
public sealed record UploadProfileAvatarCommand(
    byte[] Content,
    string FileName,
    string? ContentType,
    long FileSize) : IRequest<UploadProfileAvatarResponse>;
