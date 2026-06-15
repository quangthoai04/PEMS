using PEMS.Domain.Entities.AgendaTemplates;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Faqs;
using PEMS.Domain.Entities.Feedbacks;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Minutes;
using PEMS.Domain.Entities.News;
using PEMS.Domain.Entities.Notifications;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Reports;
using PEMS.Domain.Entities.Tasks;
using PEMS.Domain.Entities.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Galleries;

[Table("gallery_images")]
public class GalleryImage
{
    [Key]
    [Column("image_id")]
    public string ImageId { get; set; } = null!;

    [Column("gallery_id")]
    public string GalleryId { get; set; } = null!;

    [Column("url")]
    public string Url { get; set; } = null!;

    [Column("caption")]
    public string? Caption { get; set; }

    [Column("upload_date")]
    public DateTime UploadDate { get; set; }

    public virtual Gallery Gallery { get; set; } = null!;
}
