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

namespace PEMS.Domain.Entities.Campuses;

[Table("campuses")]
public class Campus
{
    [Key]
    [Column("campus_id")]
    public string CampusId { get; set; } = null!;

    [Column("campus_code")]
    public string CampusCode { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("location")]
    public string? Location { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("ic_head_user_id")]
    public string? IcHeadUserId { get; set; }

    [Column("capacity")]
    public int? Capacity { get; set; }

    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
