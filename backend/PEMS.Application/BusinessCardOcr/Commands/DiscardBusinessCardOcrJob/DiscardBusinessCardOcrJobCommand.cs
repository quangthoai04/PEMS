using MediatR;

namespace PEMS.Application.BusinessCardOcr.Commands.DiscardBusinessCardOcrJob;

/// <summary>POST /api/business-card-ocr/jobs/{ocrJobId}/discard — no contact is ever created from a discarded job.</summary>
public sealed record DiscardBusinessCardOcrJobCommand(ulong OcrJobId) : IRequest<DiscardBusinessCardOcrJobResponse>;

public sealed class DiscardBusinessCardOcrJobResponse
{
    public ulong OcrJobId { get; set; }
    public string Status { get; set; } = "DISCARDED";
}
