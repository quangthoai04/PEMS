using MediatR;
using PEMS.Application.BusinessCardOcr.Common;

namespace PEMS.Application.BusinessCardOcr.Queries.GetBusinessCardOcrJob;

/// <summary>GET /api/business-card-ocr/jobs/{ocrJobId} — draft/status; never returns raw OCR text.</summary>
public sealed record GetBusinessCardOcrJobQuery(ulong OcrJobId) : IRequest<BusinessCardOcrJobDto>;
