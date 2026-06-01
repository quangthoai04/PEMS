using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Domain.Entities; // Gọi thực thể sạch từ tầng Domain sang

namespace Infrastructure._Persistence
{
    public partial class ApplicationDbContext : DbContext
    {
        // Hàm khởi tạo không tham số (Cứ giữ nguyên)
        public ApplicationDbContext()
        {
        }

        // Hàm khởi tạo nhận Options từ file Program.cs truyền xuống
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Toàn bộ các dòng public virtual DbSet<...> phía dưới của bạn giữ nguyên 100%...

        public virtual DbSet<Actionitem> Actionitems { get; set; }

    public virtual DbSet<Delegation> Delegations { get; set; }

    public virtual DbSet<Delegationagenda> Delegationagendas { get; set; }

    public virtual DbSet<Delegationmember> Delegationmembers { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Forumcomment> Forumcomments { get; set; }

    public virtual DbSet<Forumpost> Forumposts { get; set; }

    public virtual DbSet<Fptcampus> Fptcampuses { get; set; }

    public virtual DbSet<Meetingminute> Meetingminutes { get; set; }

    public virtual DbSet<News> News { get; set; }

    public virtual DbSet<Partner> Partners { get; set; }

    public virtual DbSet<Partnercontact> Partnercontacts { get; set; }

    public virtual DbSet<Partnerdocument> Partnerdocuments { get; set; }

    public virtual DbSet<Partnersynclog> Partnersynclogs { get; set; }

    public virtual DbSet<Resourcerequest> Resourcerequests { get; set; }

    public virtual DbSet<Useraccount> Useraccounts { get; set; }

    public virtual DbSet<Userrole> Userroles { get; set; }

   
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Actionitem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PRIMARY");

            entity
                .ToTable("actionitems", tb => tb.HasComment("Danh sách đầu việc phát sinh (chốt chặn bắt buộc hoàn thành trước khi Đóng đoàn)"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.MinutesId, "FK_ActionItems_MeetingMinutes");

            entity.HasIndex(e => e.AssigneeUserId, "FK_ActionItems_UserAccounts");

            entity.Property(e => e.ItemId).HasComment("UUID đầu việc phát sinh");
            entity.Property(e => e.AssigneeUserId).HasComment("Cán bộ hoặc Sinh viên hỗ trợ chịu trách nhiệm thực hiện");
            entity.Property(e => e.Deadline).HasComment("Hạn chót hoàn thành đầu việc");
            entity.Property(e => e.IsCompleted).HasComment("Trạng thái (0: Chưa xong, 1: Đã hoàn thành)");
            entity.Property(e => e.MinutesId).HasComment("Sinh ra từ biên bản cuộc họp nào");
            entity.Property(e => e.TaskDescription)
                .HasComment("Nội dung công việc chi tiết cần làm")
                .HasColumnType("text");

            entity.HasOne(d => d.AssigneeUser).WithMany(p => p.Actionitems)
                .HasForeignKey(d => d.AssigneeUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ActionItems_UserAccounts");

            entity.HasOne(d => d.Minutes).WithMany(p => p.Actionitems)
                .HasForeignKey(d => d.MinutesId)
                .HasConstraintName("FK_ActionItems_MeetingMinutes");
        });

        modelBuilder.Entity<Delegation>(entity =>
        {
            entity.HasKey(e => e.DelegationId).HasName("PRIMARY");

            entity
                .ToTable("delegations", tb => tb.HasComment("Bảng trung tâm quản lý thông tin các đoàn khách quốc tế"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.CreatedBy, "FK_Delegations_Creator");

            entity.HasIndex(e => e.CampusCode, "FK_Delegations_FptCampuses");

            entity.HasIndex(e => e.HostUserId, "FK_Delegations_Host");

            entity.HasIndex(e => e.PartnerId, "FK_Delegations_Partners");

            entity.Property(e => e.DelegationId).HasComment("UUID định danh đoàn khách");
            entity.Property(e => e.CampusCode)
                .HasMaxLength(10)
                .HasComment("Cơ sở chịu trách nhiệm đón tiếp chính");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.CreatedBy).HasComment("Người tạo đơn");
            entity.Property(e => e.DelegationName)
                .HasMaxLength(255)
                .HasComment("Tên đoàn khách (Ví dụ: Đoàn Đại học Sarawak Malaysia ghé thăm)");
            entity.Property(e => e.DelegationStatus)
                .HasMaxLength(30)
                .HasDefaultValueSql("'PendingApproval'")
                .HasComment("Trạng thái đoàn (PendingApproval, Approved, Ongoing, Closed, Cancelled)");
            entity.Property(e => e.HostUserId).HasComment("Cán bộ HTQT phụ trách chính điều phối đoàn này");
            entity.Property(e => e.IsApprovedByHo)
                .HasComment("Cờ chốt chặn HO duyệt cho đơn liên cơ sở")
                .HasColumnName("IsApprovedByHO");
            entity.Property(e => e.IsCrossCampus).HasComment("Cờ nhận diện đoàn đi liên cơ sở/chéo nhiều campus");
            entity.Property(e => e.PartnerId).HasComment("Đối tác liên kết (NULL nếu là khách vãng lai đăng ký trực tuyến)");
            entity.Property(e => e.VisitDate).HasComment("Ngày đón tiếp chính thức");
            entity.Property(e => e.VisitType)
                .HasMaxLength(20)
                .HasComment("Hình thức (DIRECT - Trực tiếp, ONLINE - Trực tuyến)");

            entity.HasOne(d => d.CampusCodeNavigation).WithMany(p => p.Delegations)
                .HasForeignKey(d => d.CampusCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Delegations_FptCampuses");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DelegationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Delegations_Creator");

            entity.HasOne(d => d.HostUser).WithMany(p => p.DelegationHostUsers)
                .HasForeignKey(d => d.HostUserId)
                .HasConstraintName("FK_Delegations_Host");

            entity.HasOne(d => d.Partner).WithMany(p => p.Delegations)
                .HasForeignKey(d => d.PartnerId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Delegations_Partners");
        });

        modelBuilder.Entity<Delegationagenda>(entity =>
        {
            entity.HasKey(e => e.AgendaId).HasName("PRIMARY");

            entity
                .ToTable("delegationagendas", tb => tb.HasComment("Chi tiết Agenda lịch trình hoạt động trong ngày của đoàn khách"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DelegationId, "FK_DelegationAgendas_Delegations");

            entity.Property(e => e.AgendaId).HasComment("UUID lịch trình nhỏ");
            entity.Property(e => e.ActivityDescription)
                .HasComment("Mô tả chi tiết hoạt động (Ví dụ: Tham quan Tượng Thinker, Họp ký kết)")
                .HasColumnType("text");
            entity.Property(e => e.DelegationId).HasComment("Thuộc đoàn khách nào");
            entity.Property(e => e.Location)
                .HasMaxLength(150)
                .HasComment("Địa điểm diễn ra (Ví dụ: Phòng họp 202 Alpha)");
            entity.Property(e => e.TimeSlot)
                .HasComment("Mốc thời gian (Ví dụ: 09:00:00)")
                .HasColumnType("time");

            entity.HasOne(d => d.Delegation).WithMany(p => p.Delegationagenda)
                .HasForeignKey(d => d.DelegationId)
                .HasConstraintName("FK_DelegationAgendas_Delegations");
        });

        modelBuilder.Entity<Delegationmember>(entity =>
        {
            entity.HasKey(e => new { e.DelegationId, e.UserId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("delegationmembers", tb => tb.HasComment("Bảng bắc cầu quản lý danh sách thành viên nội bộ và khách tham gia đoàn"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.UserId, "FK_DelegationMembers_UserAccounts");

            entity.Property(e => e.DelegationId).HasComment("Mã đoàn");
            entity.Property(e => e.UserId).HasComment("Mã User được add (Cán bộ ban khác, Sinh viên Buddy, Media, Khách ngoài)");
            entity.Property(e => e.SpecificRole)
                .HasMaxLength(50)
                .HasComment("Vai trò chi tiết được gán trong đoàn (Buddy, Media, Attendee)");

            entity.HasOne(d => d.Delegation).WithMany(p => p.Delegationmembers)
                .HasForeignKey(d => d.DelegationId)
                .HasConstraintName("FK_DelegationMembers_Delegations");

            entity.HasOne(d => d.User).WithMany(p => p.Delegationmembers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_DelegationMembers_UserAccounts");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PRIMARY");

            entity
                .ToTable("departments", tb => tb.HasComment("Danh mục phòng ban chức năng điều phối nội bộ"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.CampusCode, "FK_Departments_FptCampuses");

            entity.Property(e => e.DepartmentId).HasComment("UUID định danh phòng ban");
            entity.Property(e => e.CampusCode)
                .HasMaxLength(10)
                .HasComment("Liên kết thuộc cơ sở nào");
            entity.Property(e => e.DepartmentName)
                .HasMaxLength(100)
                .HasComment("Tên phòng ban phối hợp (Hành chính, Tuyển sinh, HTQT,...)");

            entity.HasOne(d => d.CampusCodeNavigation).WithMany(p => p.Departments)
                .HasForeignKey(d => d.CampusCode)
                .HasConstraintName("FK_Departments_FptCampuses");
        });

        modelBuilder.Entity<Forumcomment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PRIMARY");

            entity
                .ToTable("forumcomments", tb => tb.HasComment("Phản hồi thảo luận tiến độ công việc giữa các thành viên của đoàn"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.PostId, "FK_ForumComments_ForumPosts");

            entity.HasIndex(e => e.AuthorUserId, "FK_ForumComments_UserAccounts");

            entity.Property(e => e.CommentId).HasComment("UUID bình luận");
            entity.Property(e => e.AuthorUserId).HasComment("Người bình luận");
            entity.Property(e => e.CommentContent)
                .HasComment("Nội dung phản hồi")
                .HasColumnType("text");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.PostId).HasComment("Bình luận thuộc bài viết nào");

            entity.HasOne(d => d.AuthorUser).WithMany(p => p.Forumcomments)
                .HasForeignKey(d => d.AuthorUserId)
                .HasConstraintName("FK_ForumComments_UserAccounts");

            entity.HasOne(d => d.Post).WithMany(p => p.Forumcomments)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("FK_ForumComments_ForumPosts");
        });

        modelBuilder.Entity<Forumpost>(entity =>
        {
            entity.HasKey(e => e.PostId).HasName("PRIMARY");

            entity
                .ToTable("forumposts", tb => tb.HasComment("Bài viết thảo luận nội bộ kín của từng đoàn khách cụ thể"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DelegationId, "FK_ForumPosts_Delegations");

            entity.HasIndex(e => e.AuthorUserId, "FK_ForumPosts_UserAccounts");

            entity.Property(e => e.PostId).HasComment("UUID bài đăng thảo luận");
            entity.Property(e => e.AttachmentUrl)
                .HasMaxLength(255)
                .HasComment("File tài liệu đính kèm phục vụ công việc");
            entity.Property(e => e.AuthorUserId).HasComment("Người đăng bài (Staff/Buddy/Media/Guest)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.DelegationId).HasComment("Không gian diễn đàn kín của riêng đoàn khách nào");
            entity.Property(e => e.PostContent)
                .HasComment("Nội dung thông báo, trao đổi tiến độ hậu cần")
                .HasColumnType("text");

            entity.HasOne(d => d.AuthorUser).WithMany(p => p.Forumposts)
                .HasForeignKey(d => d.AuthorUserId)
                .HasConstraintName("FK_ForumPosts_UserAccounts");

            entity.HasOne(d => d.Delegation).WithMany(p => p.Forumposts)
                .HasForeignKey(d => d.DelegationId)
                .HasConstraintName("FK_ForumPosts_Delegations");
        });

        modelBuilder.Entity<Fptcampus>(entity =>
        {
            entity.HasKey(e => e.CampusCode).HasName("PRIMARY");

            entity
                .ToTable("fptcampuses", tb => tb.HasComment("Danh mục 5 cơ sở Đại học FPT toàn quốc"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.CampusCode)
                .HasMaxLength(10)
                .HasComment("Mã cơ sở (HL, HCM, DN, qn, CT)");
            entity.Property(e => e.CampusName)
                .HasMaxLength(100)
                .HasComment("Tên cơ sở hiển thị (Hòa Lạc, TP. Hồ Chí Minh...)");
        });

        modelBuilder.Entity<Meetingminute>(entity =>
        {
            entity.HasKey(e => e.MinutesId).HasName("PRIMARY");

            entity
                .ToTable("meetingminutes", tb => tb.HasComment("Biên bản ghi nhận nội dung cuộc họp chính thức của đoàn khách"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DelegationId, "UQ_MeetingMinutes_DelegationId").IsUnique();

            entity.Property(e => e.MinutesId).HasComment("UUID biên bản cuộc họp");
            entity.Property(e => e.DelegationId).HasComment("Thuộc đoàn khách nào");
            entity.Property(e => e.DiscussionContent).HasComment("Nội dung chi tiết các vấn đề đã thảo luận ghi lại");
            entity.Property(e => e.IsDraft)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasComment("Cờ lưu trạng thái (1: Bản nháp Staff đang viết, 0: Biên bản chính thức)");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Delegation).WithOne(p => p.Meetingminute)
                .HasForeignKey<Meetingminute>(d => d.DelegationId)
                .HasConstraintName("FK_MeetingMinutes_Delegations");
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.HasKey(e => e.NewsId).HasName("PRIMARY");

            entity
                .ToTable("news", tb => tb.HasComment("Quản lý tin tức sự kiện truyền thông công cộng (Hỗ trợ nạp API tự động)"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DelegationId, "FK_News_Delegations");

            entity.HasIndex(e => e.CreatedBy, "FK_News_UserAccounts");

            entity.Property(e => e.NewsId).HasComment("UUID bài viết tin tức");
            entity.Property(e => e.Content).HasComment("Nội dung bài viết (HTML format)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.CreatedBy).HasComment("Người soạn thảo");
            entity.Property(e => e.DelegationId).HasComment("Bài viết liên kết với đoàn khách cụ thể nào");
            entity.Property(e => e.IsFromOutbound).HasComment("Cờ nhận diện (1: Bài nạp tự động từ trang Outbound về, 0: Bài tự viết)");
            entity.Property(e => e.NewsStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Draft'")
                .HasComment("Trạng thái phê duyệt (Draft, PendingApproval, Published)");
            entity.Property(e => e.ThumbnailUrl)
                .HasMaxLength(255)
                .HasComment("Ảnh đại diện bài viết");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasComment("Tiêu đề bài báo truyền thông sự kiện");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.News)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_News_UserAccounts");

            entity.HasOne(d => d.Delegation).WithMany(p => p.News)
                .HasForeignKey(d => d.DelegationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_News_Delegations");
        });

        modelBuilder.Entity<Partner>(entity =>
        {
            entity.HasKey(e => e.PartnerId).HasName("PRIMARY");

            entity
                .ToTable("partners", tb => tb.HasComment("Mạng lưới thực thể Đối tác toàn cầu"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.CreatedBy, "FK_Partners_UserAccounts");

            entity.Property(e => e.PartnerId).HasComment("UUID định danh đối tác");
            entity.Property(e => e.CollaborationStatus)
                .HasMaxLength(30)
                .HasDefaultValueSql("'Potential'")
                .HasComment("Trạng thái hợp tác (Potential, In-Discussion, Signed_MoU, Signed_MoA)");
            entity.Property(e => e.Country)
                .HasMaxLength(100)
                .HasComment("Quốc gia/Vùng lãnh thổ");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.CreatedBy).HasComment("Cán bộ tạo thực thể");
            entity.Property(e => e.EnglishName)
                .HasMaxLength(255)
                .HasComment("Tên tiếng Anh chính thức của trường đối tác");
            entity.Property(e => e.IsApproved).HasComment("Trạng thái Admin/HO duyệt (1: Đã duyệt, 0: Chờ duyệt thô)");
            entity.Property(e => e.LocalName)
                .HasMaxLength(255)
                .HasComment("Tên theo tiếng bản địa");
            entity.Property(e => e.LogoUrl)
                .HasMaxLength(255)
                .HasComment("Link CDN ảnh logo đối tác");
            entity.Property(e => e.Website)
                .HasMaxLength(255)
                .HasComment("Đường dẫn trang web đối tác");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Partners)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Partners_UserAccounts");
        });

        modelBuilder.Entity<Partnercontact>(entity =>
        {
            entity.HasKey(e => e.ContactId).HasName("PRIMARY");

            entity
                .ToTable("partnercontacts", tb => tb.HasComment("Danh sách đầu mối liên lạc con thuộc Hồ sơ Đối tác"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.PartnerId, "FK_PartnerContacts_Partners");

            entity.Property(e => e.ContactId).HasComment("UUID đầu mối liên hệ");
            entity.Property(e => e.ContactEmail)
                .HasMaxLength(150)
                .HasComment("Email làm việc trực tiếp");
            entity.Property(e => e.ContactName)
                .HasMaxLength(100)
                .HasComment("Họ tên cán bộ đầu mối phía đối tác");
            entity.Property(e => e.ContactPhone)
                .HasMaxLength(20)
                .HasComment("Số điện thoại/Whatsapp");
            entity.Property(e => e.Designation)
                .HasMaxLength(100)
                .HasComment("Chức vụ, chức danh làm việc");
            entity.Property(e => e.PartnerId).HasComment("Liên kết thuộc đối tác nào");

            entity.HasOne(d => d.Partner).WithMany(p => p.Partnercontacts)
                .HasForeignKey(d => d.PartnerId)
                .HasConstraintName("FK_PartnerContacts_Partners");
        });

        modelBuilder.Entity<Partnerdocument>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("PRIMARY");

            entity
                .ToTable("partnerdocuments", tb => tb.HasComment("Kho lưu trữ văn bản ký kết phụ thuộc thực thể Đối tác"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.PartnerId, "FK_PartnerDocuments_Partners");

            entity.Property(e => e.DocumentId).HasComment("UUID file đính kèm");
            entity.Property(e => e.DocumentTitle)
                .HasMaxLength(255)
                .HasComment("Tiêu đề tài liệu (Ví dụ: MoU_Signing_2026)");
            entity.Property(e => e.DocumentType)
                .HasMaxLength(50)
                .HasComment("Phân loại (MoU, MoA, Proposal, Brochure)");
            entity.Property(e => e.ExpiryDate).HasComment("Ngày hết hạn hiệu lực văn bản ký kết");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(255)
                .HasComment("Đường dẫn vật lý lưu file an toàn trên Cloud Storage");
            entity.Property(e => e.PartnerId).HasComment("Thuộc đối tác nào");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Partner).WithMany(p => p.Partnerdocuments)
                .HasForeignKey(d => d.PartnerId)
                .HasConstraintName("FK_PartnerDocuments_Partners");
        });

        modelBuilder.Entity<Partnersynclog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PRIMARY");

            entity
                .ToTable("partnersynclogs", tb => tb.HasComment("Bảo mật nhật ký tích hợp API với trang Outbound"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.PartnerId, "FK_PartnerSyncLogs_Partners");

            entity.HasIndex(e => e.SyncedBy, "FK_PartnerSyncLogs_UserAccounts");

            entity.Property(e => e.LogId).HasComment("UUID mã log");
            entity.Property(e => e.PartnerId).HasComment("Đối tác được đồng bộ");
            entity.Property(e => e.ResponseContent)
                .HasComment("Nội dung phản hồi từ API Outbound hoặc thông báo lỗi")
                .HasColumnType("text");
            entity.Property(e => e.SyncDirection)
                .HasMaxLength(20)
                .HasComment("Hướng đồng bộ (PUSH_TO_OUTBOUND, PULL_PROGRAM_FROM_OUTBOUND)");
            entity.Property(e => e.SyncStatus)
                .HasMaxLength(20)
                .HasComment("Trạng thái (SUCCESS, FAILED)");
            entity.Property(e => e.SyncedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.SyncedBy).HasComment("Cán bộ click đồng bộ");

            entity.HasOne(d => d.Partner).WithMany(p => p.Partnersynclogs)
                .HasForeignKey(d => d.PartnerId)
                .HasConstraintName("FK_PartnerSyncLogs_Partners");

            entity.HasOne(d => d.SyncedByNavigation).WithMany(p => p.Partnersynclogs)
                .HasForeignKey(d => d.SyncedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnerSyncLogs_UserAccounts");
        });

        modelBuilder.Entity<Resourcerequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PRIMARY");

            entity
                .ToTable("resourcerequests", tb => tb.HasComment("Quản lý điều phối mượn xe điện, teabreak liên phòng ban chức năng"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DelegationId, "FK_ResourceRequests_Delegations");

            entity.HasIndex(e => e.DepartmentId, "FK_ResourceRequests_Departments");

            entity.HasIndex(e => e.ConfirmedBy, "FK_ResourceRequests_UserAccounts");

            entity.Property(e => e.RequestId).HasComment("UUID yêu cầu tài nguyên hậu cần");
            entity.Property(e => e.ConfirmationStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'")
                .HasComment("Trạng thái phòng ban phản hồi (Pending, Confirmed, Rejected)");
            entity.Property(e => e.ConfirmedBy).HasComment("Cán bộ phòng ban xử lý duyệt");
            entity.Property(e => e.DelegationId).HasComment("Thuộc đoàn khách nào");
            entity.Property(e => e.DepartmentId).HasComment("Phòng ban chịu trách nhiệm phê duyệt mượn (Hành chính/Tuyển sinh)");
            entity.Property(e => e.Quantity)
                .HasDefaultValueSql("'1'")
                .HasComment("Số lượng tài nguyên yêu cầu");
            entity.Property(e => e.RejectedReason)
                .HasMaxLength(255)
                .HasComment("Lý do từ chối mượn của phòng ban liên quan");
            entity.Property(e => e.ResourceType)
                .HasMaxLength(50)
                .HasComment("Loại tài nguyên mượn (Electric_Car, Meeting_Room, TeaBreak, LED_Welcome)");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("datetime");
            entity.Property(e => e.UsageDetails)
                .HasComment("Mô tả chi tiết yêu cầu, khung giờ mượn cụ thể")
                .HasColumnType("text");

            entity.HasOne(d => d.ConfirmedByNavigation).WithMany(p => p.Resourcerequests)
                .HasForeignKey(d => d.ConfirmedBy)
                .HasConstraintName("FK_ResourceRequests_UserAccounts");

            entity.HasOne(d => d.Delegation).WithMany(p => p.Resourcerequests)
                .HasForeignKey(d => d.DelegationId)
                .HasConstraintName("FK_ResourceRequests_Delegations");

            entity.HasOne(d => d.Department).WithMany(p => p.Resourcerequests)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ResourceRequests_Departments");
        });

        modelBuilder.Entity<Useraccount>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity
                .ToTable("useraccounts", tb => tb.HasComment("Bảng lưu trữ thông tin tài khoản toàn hệ thống"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DepartmentId, "FK_UserAccounts_Departments");

            entity.HasIndex(e => e.CampusCode, "FK_UserAccounts_FptCampuses");

            entity.HasIndex(e => e.RoleCode, "FK_UserAccounts_UserRoles");

            entity.HasIndex(e => e.Email, "UQ_UserAccounts_Email").IsUnique();

            entity.Property(e => e.UserId).HasComment("UUID người dùng");
            entity.Property(e => e.CampusCode)
                .HasMaxLength(10)
                .HasComment("Thuộc cơ sở quản lý nào (HO để NULL)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.DepartmentId).HasComment("Thuộc phòng ban nào (Guest/Student để NULL)");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasComment("Email đăng nhập (SSO FPT hoặc email cá nhân của Guest)");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasComment("Họ và tên người dùng");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasComment("Trạng thái kích hoạt tài khoản (1: Active, 0: Disabled)");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasComment("Mật khẩu mã hóa (Chỉ dùng cho Guest đăng ký ngoài)");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .HasComment("Số điện thoại liên hệ");
            entity.Property(e => e.RoleCode)
                .HasMaxLength(20)
                .HasComment("Mã quyền tối cao");

            entity.HasOne(d => d.CampusCodeNavigation).WithMany(p => p.Useraccounts)
                .HasForeignKey(d => d.CampusCode)
                .HasConstraintName("FK_UserAccounts_FptCampuses");

            entity.HasOne(d => d.Department).WithMany(p => p.Useraccounts)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_UserAccounts_Departments");

            entity.HasOne(d => d.RoleCodeNavigation).WithMany(p => p.Useraccounts)
                .HasForeignKey(d => d.RoleCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAccounts_UserRoles");
        });

        modelBuilder.Entity<Userrole>(entity =>
        {
            entity.HasKey(e => e.RoleCode).HasName("PRIMARY");

            entity
                .ToTable("userroles", tb => tb.HasComment("Bảng phân quyền vai trò hệ thống cố định"))
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.RoleCode)
                .HasMaxLength(20)
                .HasComment("Mã vai trò (HO, Admin, Staff, Student, Guest)");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasComment("Tên hiển thị vai trò tiếng Anh");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
}
