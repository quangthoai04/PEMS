using Microsoft.EntityFrameworkCore;
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

namespace PEMS.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext() { }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // ── A. RBAC ──────────────────────────────────────────────────────────
    public DbSet<Role> Roles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }

    // ── B. Tổ chức ───────────────────────────────────────────────────────
    public DbSet<Campus> Campuses { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<User> Users { get; set; }

    // ── C. Đối tác ───────────────────────────────────────────────────────
    public DbSet<Partner> Partners { get; set; }
    public DbSet<PartnerContact> PartnerContacts { get; set; }
    public DbSet<PartnerHistory> PartnerHistories { get; set; }
    public DbSet<PartnerDocument> PartnerDocuments { get; set; }
    public DbSet<PartnerSyncLog> PartnerSyncLogs { get; set; }

    // ── D. Đoàn khách ────────────────────────────────────────────────────
    public DbSet<VisitRequest> VisitRequests { get; set; }
    public DbSet<VisitDetail> VisitDetails { get; set; }
    public DbSet<VisitParticipant> VisitParticipants { get; set; }
    public DbSet<VisitAgenda> VisitAgendas { get; set; }
    public DbSet<AgendaTemplate> AgendaTemplates { get; set; }
    public DbSet<AgendaTemplateItem> AgendaTemplateItems { get; set; }

    // ── E. Công việc hậu cần ─────────────────────────────────────────────
    public DbSet<PemsTask> Tasks { get; set; }
    public DbSet<TaskAction> TaskActions { get; set; }
    public DbSet<ActionItem> ActionItems { get; set; }

    // ── F. Biên bản & Đánh giá ───────────────────────────────────────────
    public DbSet<Minute> Minutes { get; set; }
    public DbSet<MinuteParticipant> MinuteParticipants { get; set; }
    public DbSet<Feedback> Feedbacks { get; set; }
    public DbSet<FeedbackItem> FeedbackItems { get; set; }

    // ── G. Truyền thông & Email ───────────────────────────────────────────
    public DbSet<News> News { get; set; }
    public DbSet<EmailTemplate> EmailTemplates { get; set; }
    public DbSet<SentEmail> SentEmails { get; set; }
    public DbSet<SentEmailRecipient> SentEmailRecipients { get; set; }

    // ── H. Nội dung & Hỗ trợ ─────────────────────────────────────────────
    public DbSet<Document> Documents { get; set; }
    public DbSet<Gallery> Galleries { get; set; }
    public DbSet<GalleryImage> GalleryImages { get; set; }
    public DbSet<GalleryLocation> GalleryLocations { get; set; }
    public DbSet<GalleryLocationImage> GalleryLocationImages { get; set; }
    public DbSet<Faq> Faqs { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    // ── I. Nhật ký / Audit ────────────────────────────────────────────────
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<LoginLog> LoginLogs { get; set; }
    public DbSet<VisitStatusLog> VisitStatusLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        // Composite PK: role_permissions
        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });

        // BIGINT AUTO_INCREMENT cho bảng log
        modelBuilder.Entity<AuditLog>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<LoginLog>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<VisitStatusLog>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        // ── Relationships ─────────────────────────────────────────────────

        // RolePermission
        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role).WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);

        // Department → Campus
        modelBuilder.Entity<Department>()
            .HasOne(d => d.Campus).WithMany(c => c.Departments)
            .HasForeignKey(d => d.CampusId).OnDelete(DeleteBehavior.Restrict);

        // User → Role, Campus, Department
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role).WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<User>()
            .HasOne(u => u.Campus).WithMany(c => c.Users)
            .HasForeignKey(u => u.CampusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<User>()
            .HasOne(u => u.Department).WithMany(d => d.Users)
            .HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        // PartnerContact/History/Document/SyncLog → Partner (CASCADE)
        modelBuilder.Entity<PartnerContact>()
            .HasOne(pc => pc.Partner).WithMany(p => p.Contacts)
            .HasForeignKey(pc => pc.PartnerId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PartnerHistory>()
            .HasOne(ph => ph.Partner).WithMany(p => p.Histories)
            .HasForeignKey(ph => ph.PartnerId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PartnerDocument>()
            .HasOne(pd => pd.Partner).WithMany(p => p.Documents)
            .HasForeignKey(pd => pd.PartnerId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PartnerSyncLog>()
            .HasOne(ps => ps.Partner).WithMany(p => p.SyncLogs)
            .HasForeignKey(ps => ps.PartnerId).OnDelete(DeleteBehavior.SetNull);

        // VisitRequest → Campus, Partner
        modelBuilder.Entity<VisitRequest>()
            .HasOne(v => v.Campus).WithMany()
            .HasForeignKey(v => v.CampusId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisitRequest>()
            .HasOne(v => v.Partner).WithMany()
            .HasForeignKey(v => v.PartnerId).OnDelete(DeleteBehavior.SetNull);

        // VisitDetail, VisitParticipant, VisitAgenda → VisitRequest (CASCADE)
        modelBuilder.Entity<VisitDetail>()
            .HasOne(vd => vd.VisitRequest).WithMany(v => v.Details)
            .HasForeignKey(vd => vd.VisitId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VisitParticipant>()
            .HasOne(vp => vp.VisitRequest).WithMany(v => v.Participants)
            .HasForeignKey(vp => vp.VisitId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VisitParticipant>()
            .HasOne(vp => vp.User).WithMany()
            .HasForeignKey(vp => vp.UserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitAgenda>()
            .HasOne(va => va.VisitRequest).WithMany(v => v.Agendas)
            .HasForeignKey(va => va.VisitId).OnDelete(DeleteBehavior.Cascade);

        // AgendaTemplateItem → AgendaTemplate (CASCADE)
        modelBuilder.Entity<AgendaTemplateItem>()
            .HasOne(i => i.Template).WithMany(t => t.Items)
            .HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);

        // PemsTask → VisitRequest, Department
        modelBuilder.Entity<PemsTask>()
            .HasOne(t => t.VisitRequest).WithMany(v => v.Tasks)
            .HasForeignKey(t => t.VisitId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PemsTask>()
            .HasOne(t => t.Department).WithMany(d => d.Tasks)
            .HasForeignKey(t => t.DepartmentId).OnDelete(DeleteBehavior.SetNull);

        // TaskAction → PemsTask (CASCADE)
        modelBuilder.Entity<TaskAction>()
            .HasOne(a => a.Task).WithMany(t => t.Actions)
            .HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Cascade);

        // ActionItem → VisitRequest (CASCADE)
        modelBuilder.Entity<ActionItem>()
            .HasOne(ai => ai.VisitRequest).WithMany()
            .HasForeignKey(ai => ai.VisitId).OnDelete(DeleteBehavior.Cascade);

        // Minute → VisitRequest (CASCADE)
        modelBuilder.Entity<Minute>()
            .HasOne(m => m.VisitRequest).WithMany(v => v.Minutes)
            .HasForeignKey(m => m.VisitId).OnDelete(DeleteBehavior.Cascade);

        // MinuteParticipant → Minute (CASCADE)
        modelBuilder.Entity<MinuteParticipant>()
            .HasOne(mp => mp.Minute).WithMany(m => m.Participants)
            .HasForeignKey(mp => mp.MinuteId).OnDelete(DeleteBehavior.Cascade);

        // Feedback → VisitRequest (CASCADE)
        modelBuilder.Entity<Feedback>()
            .HasOne(f => f.VisitRequest).WithMany(v => v.Feedbacks)
            .HasForeignKey(f => f.VisitId).OnDelete(DeleteBehavior.Cascade);

        // FeedbackItem → Feedback (CASCADE)
        modelBuilder.Entity<FeedbackItem>()
            .HasOne(fi => fi.Feedback).WithMany(f => f.Items)
            .HasForeignKey(fi => fi.FeedbackId).OnDelete(DeleteBehavior.Cascade);

        // SentEmailRecipient → SentEmail (CASCADE)
        modelBuilder.Entity<SentEmailRecipient>()
            .HasOne(r => r.SentEmail).WithMany(e => e.Recipients)
            .HasForeignKey(r => r.EmailId).OnDelete(DeleteBehavior.Cascade);

        // GalleryImage → Gallery (CASCADE)
        modelBuilder.Entity<GalleryImage>()
            .HasOne(i => i.Gallery).WithMany(g => g.Images)
            .HasForeignKey(i => i.GalleryId).OnDelete(DeleteBehavior.Cascade);

        // GalleryLocationImage → GalleryLocation (CASCADE)
        modelBuilder.Entity<GalleryLocationImage>()
            .HasOne(i => i.Location).WithMany(l => l.Images)
            .HasForeignKey(i => i.LocationId).OnDelete(DeleteBehavior.Cascade);

        // VisitStatusLog → VisitRequest (CASCADE)
        modelBuilder.Entity<VisitStatusLog>()
            .HasOne(vsl => vsl.VisitRequest).WithMany()
            .HasForeignKey(vsl => vsl.VisitId).OnDelete(DeleteBehavior.Cascade);
    }
}
