using MediatR;

namespace PEMS.Application.Profiles.Commands.UploadProfileAvatar;

/// <summary>
/// UC-15 — replace the current user's avatar. The caller is always resolved from the JWT
/// (never from the request body), so a user can only ever change their own avatar.
/// The controller passes the raw upload stream + client-reported metadata; all validation
/// happens in the handler.
/// </summary>
public sealed record UploadProfileAvatarCommand(
    Stream FileStream,
    string OriginalFileName,
    string ContentType,
    long FileSize
) : IRequest<UploadProfileAvatarResponse>;
