using MediatR;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Commands.UploadVisitPhotos;

public sealed record UploadVisitPhotoFileDto(byte[] Content, string FileName, string ContentType);

public class UploadVisitPhotosCommand : IRequest<UploadVisitPhotosResponse>
{
    public ulong VisitInstanceId { get; set; }
    public List<UploadVisitPhotoFileDto> Files { get; set; } = new();
}