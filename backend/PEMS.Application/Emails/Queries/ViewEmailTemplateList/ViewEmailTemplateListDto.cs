using System;
using System.Collections.Generic;

namespace PEMS.Application.Emails.Queries.ViewEmailTemplateList;

public sealed class ViewEmailTemplateListDto
{
    public List<EmailTemplateListItemDto> Templates { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class EmailTemplateListItemDto
{
    public ulong EmailTemplateId { get; set; }
    public string TemplateCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Purpose { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}