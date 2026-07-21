using MediatR;

namespace PEMS.Application.News.Commands.UploadNewsCoverImage;

/// <summary>
/// <paramref name="VisitInstanceId"/> is optional — when the news post being composed is attached
/// to a đoàn (visit instance), the image is routed into that đoàn's Drive folder
/// (<c>VR-{visit_request_id}/{campus_code}</c>, shared with Student visit-photo uploads) instead of
/// the flat News folder, mirroring the file's real-world origin regardless of whether the user
/// picked it fresh or from elsewhere on their device.
/// </summary>
public sealed record UploadNewsCoverImageCommand(
    byte[]   Content,
    string   FileName,
    string?  ContentType,
    ulong?   VisitInstanceId = null) : IRequest<UploadNewsCoverImageResponse>;
