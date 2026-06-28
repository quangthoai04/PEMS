using MediatR;

namespace PEMS.Application.News.Commands.UploadNewsCoverImage;

public sealed record UploadNewsCoverImageCommand(
    byte[]  Content,
    string  FileName,
    string? ContentType) : IRequest<UploadNewsCoverImageResponse>;
