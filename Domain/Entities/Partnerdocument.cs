using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Kho lưu trữ văn bản ký kết phụ thuộc thực thể Đối tác
/// </summary>
public partial class Partnerdocument
{
    /// <summary>
    /// UUID file đính kèm
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Thuộc đối tác nào
    /// </summary>
    public Guid PartnerId { get; set; }

    /// <summary>
    /// Tiêu đề tài liệu (Ví dụ: MoU_Signing_2026)
    /// </summary>
    public string DocumentTitle { get; set; } = null!;

    /// <summary>
    /// Phân loại (MoU, MoA, Proposal, Brochure)
    /// </summary>
    public string DocumentType { get; set; } = null!;

    /// <summary>
    /// Đường dẫn vật lý lưu file an toàn trên Cloud Storage
    /// </summary>
    public string FileUrl { get; set; } = null!;

    /// <summary>
    /// Ngày hết hạn hiệu lực văn bản ký kết
    /// </summary>
    public DateOnly? ExpiryDate { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual Partner Partner { get; set; } = null!;
}
