using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Mạng lưới thực thể Đối tác toàn cầu
/// </summary>
public partial class Partner
{
    /// <summary>
    /// UUID định danh đối tác
    /// </summary>
    public Guid PartnerId { get; set; }

    /// <summary>
    /// Tên tiếng Anh chính thức của trường đối tác
    /// </summary>
    public string EnglishName { get; set; } = null!;

    /// <summary>
    /// Tên theo tiếng bản địa
    /// </summary>
    public string? LocalName { get; set; }

    /// <summary>
    /// Quốc gia/Vùng lãnh thổ
    /// </summary>
    public string Country { get; set; } = null!;

    /// <summary>
    /// Đường dẫn trang web đối tác
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Link CDN ảnh logo đối tác
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Trạng thái hợp tác (Potential, In-Discussion, Signed_MoU, Signed_MoA)
    /// </summary>
    public string CollaborationStatus { get; set; } = null!;

    /// <summary>
    /// Cán bộ tạo thực thể
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Trạng thái Admin/HO duyệt (1: Đã duyệt, 0: Chờ duyệt thô)
    /// </summary>
    public bool IsApproved { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Useraccount CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<Delegation> Delegations { get; set; } = new List<Delegation>();

    public virtual ICollection<Partnercontact> Partnercontacts { get; set; } = new List<Partnercontact>();

    public virtual ICollection<Partnerdocument> Partnerdocuments { get; set; } = new List<Partnerdocument>();

    public virtual ICollection<Partnersynclog> Partnersynclogs { get; set; } = new List<Partnersynclog>();
}
