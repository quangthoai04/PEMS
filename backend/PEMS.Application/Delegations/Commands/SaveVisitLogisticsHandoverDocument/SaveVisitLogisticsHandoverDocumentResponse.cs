namespace PEMS.Application.Delegations.Commands.SaveVisitLogisticsHandoverDocument;

public sealed class SaveVisitLogisticsHandoverDocumentResponse
{
    public ulong DocumentId { get; init; }
    public ulong FileId { get; init; }
    public string? WebViewUrl { get; init; }
    public string? DownloadUrl { get; init; }
}
