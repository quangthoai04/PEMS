using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;
using PEMS.Domain.Entities.Emails;
using System.Text.Json;

using PEMS.Application.Common;
namespace PEMS.Application.Departments.Commands.AddDepartmentPersonnel;

public sealed class AddDepartmentPersonnelCommandHandler : IRequestHandler<AddDepartmentPersonnelCommand, AddDepartmentPersonnelResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;

    public AddDepartmentPersonnelCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, IEmailService emailService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
    }

    public async Task<AddDepartmentPersonnelResponse> Handle(AddDepartmentPersonnelCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        
        var department = await _context.Departments
            .Include(d => d.Campus)
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken);

        if (department == null || department.Status != "ACTIVE")
        {
            return new AddDepartmentPersonnelResponse { Success = false, Message = "Phòng ban không tồn tại hoặc không hoạt động." };
        }

        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailExists)
        {
            return new AddDepartmentPersonnelResponse { Success = false, Message = "Email đã tồn tại trong hệ thống." };
        }

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleCode == "DEPARTMENT", cancellationToken);
        if (role == null)
        {
            return new AddDepartmentPersonnelResponse { Success = false, Message = "Role DEPARTMENT không tồn tại." };
        }

        var newUser = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            Gender = request.Gender,
            RoleId = role.RoleId,
            SubRole = request.Role == "Trưởng phòng" ? "LEADER" : "STAFF",
            PrimaryCampusId = department.CampusId,
            DepartmentId = request.DepartmentId,
            Status = "ACTIVE",
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = VietnamTime.Now(),
            CreatedBy = currentUserId
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync(cancellationToken);

        // Send email
        var subject = "[PEMS] Chào mừng bạn gia nhập hệ thống PEMS";
        var body = $@"
<div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden;"">
    <div style=""background-color: #004c91; padding: 20px; text-align: center;"">
        <h2 style=""color: #fff; margin: 0;"">Hệ thống Quản lý PEMS</h2>
    </div>
    <div style=""padding: 30px;"">
        <p style=""font-size: 16px; margin-top: 0;"">Xin chào <strong>{newUser.FullName}</strong>,</p>
        <p>Tài khoản của bạn đã được khởi tạo thành công trên Hệ thống Quản lý Lễ tân & Khách tham quan (PEMS).</p>
        <div style=""background-color: #f9f9f9; border-left: 4px solid #f37021; padding: 15px; margin: 20px 0;"">
            <h3 style=""margin-top: 0; color: #004c91; font-size: 16px;"">Thông tin truy cập:</h3>
            <ul style=""list-style-type: none; padding-left: 0; margin-bottom: 0;"">
                <li style=""margin-bottom: 8px;""><strong>Email đăng nhập:</strong> {newUser.Email}</li>
                <li style=""margin-bottom: 8px;""><strong>Vai trò:</strong> Nhân viên phòng ban (DEPARTMENT {newUser.SubRole.ToUpper()})</li>
                <li style=""margin-bottom: 8px;""><strong>Đơn vị:</strong> {department.Name}</li>
                <li><strong>Cơ sở:</strong> {department.Campus?.Name ?? ""}</li>
            </ul>
        </div>
        <p>Vui lòng đăng nhập vào hệ thống để bắt đầu tiếp nhận và xử lý các nhiệm vụ được phân công.</p>
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""https://pems.fpt.edu.vn"" style=""background-color: #f37021; color: #fff; text-decoration: none; padding: 12px 25px; border-radius: 5px; font-weight: bold; display: inline-block;"">Đăng nhập vào PEMS</a>
        </div>
        <p style=""font-size: 13px; color: #777;"">Nếu bạn gặp bất kỳ vấn đề nào trong quá trình đăng nhập, vui lòng liên hệ Trưởng phòng ban hoặc Quản trị hệ thống để được hỗ trợ.</p>
    </div>
    <div style=""background-color: #f1f1f1; padding: 15px; text-align: center; font-size: 12px; color: #666;"">
        Đây là email tự động từ hệ thống PEMS. Vui lòng không trả lời email này.
    </div>
</div>";

        var sentEmail = new SentEmail
        {
            RelatedType = "USER",
            RelatedId = newUser.UserId,
            Subject = subject,
            BodySnapshot = body,
            Status = "QUEUED",
            SentBy = currentUserId,
            SentAt = VietnamTime.Now()
        };

        _context.SentEmails.Add(sentEmail);
        await _context.SaveChangesAsync(cancellationToken);

        var recipient = new SentEmailRecipient
        {
            RecipientEmail = newUser.Email,
            RecipientName = newUser.FullName,
            RecipientType = "TO",
            DeliveryStatus = "QUEUED"
        };

        sentEmail.Recipients.Add(recipient);
        await _context.SaveChangesAsync(cancellationToken);

        // Actually send the email using IEmailService
        try
        {
            await _emailService.SendAsync(newUser.Email, subject, body, cancellationToken);
            sentEmail.Status = "SENT";
            recipient.DeliveryStatus = "DELIVERED";
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            sentEmail.Status = "FAILED";
            recipient.DeliveryStatus = "FAILED";
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new AddDepartmentPersonnelResponse { Success = true, Message = "Đã thêm nhân sự thành công.", UserId = newUser.UserId };
    }
}
