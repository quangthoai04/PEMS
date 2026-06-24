using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Profiles.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Enums;

namespace PEMS.Application.Profiles.Commands.UpdateProfile;

/// <summary>
/// UC-15 handler. Updates only the safe self-service fields (full_name, gender, phone,
/// and nationality for VISITOR). The caller is resolved from the JWT. All validation runs
/// before any mutation, so a rejected request never produces a partial update.
/// </summary>
public sealed class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, ProfileResponse>
{
    // Sensitive / system-owned fields that must never arrive on a self-service update.
    private static readonly HashSet<string> ForbiddenFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "userId", "id", "email", "role", "roleId", "roleCode", "subRole",
        "primaryCampusId", "campusId", "departmentId", "studentCode", "feId",
        "status", "password", "passwordHash", "avatarUrl",
        "createdAt", "createdBy", "updatedAt", "updatedBy",
    };

    // Allowed phone shape: optional leading '+', then digits and the usual separators.
    // The actual digit count is checked separately (8–15, international-friendly / E.164).
    private static readonly Regex PhoneShapeRegex = new(@"^\+?[0-9\s.\-()]+$", RegexOptions.Compiled);
    private const int PhoneMinDigits = 8;
    private const int PhoneMaxDigits = 15;

    private const int FullNameMaxLength = 150;
    private const int PhoneMaxLength = 30;
    private const int NationalityMaxLength = 100;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UpdateProfileCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ProfileResponse> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new AuthenticationFailedException("Phiên đăng nhập không hợp lệ.");

        // AF-05 / TC-17 — reject the whole request if it carries any disallowed field.
        if (request.ExtraFields is { Count: > 0 })
        {
            var offending = request.ExtraFields.Keys
                .Where(k => ForbiddenFields.Contains(k))
                .ToList();
            if (offending.Count == 0)
                offending = request.ExtraFields.Keys.ToList(); // unknown fields are rejected too

            throw new BusinessRuleException(
                $"Yêu cầu chứa trường không được phép cập nhật: {string.Join(", ", offending)}.",
                "PROFILE_FORBIDDEN_FIELD");
        }

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        if (user.Status != UserStatuses.Active)
            throw new ForbiddenException("Tài khoản của bạn hiện không thể cập nhật hồ sơ.");

        var isVisitor = user.Role.RoleCode == RoleCodes.Visitor;

        // ── full_name ── required (when provided), trimmed, non-empty, bounded ──
        if (request.FullName is not null)
        {
            var fullName = request.FullName.Trim();
            if (fullName.Length == 0)
                throw new BusinessRuleException("Họ và tên không được để trống.", "PROFILE_FULLNAME_REQUIRED");
            if (fullName.Length > FullNameMaxLength)
                throw new BusinessRuleException($"Họ và tên không được vượt quá {FullNameMaxLength} ký tự.", "PROFILE_FULLNAME_TOO_LONG");
            user.FullName = fullName;
        }

        // ── gender ── MALE / FEMALE / OTHER only ──
        if (request.Gender is not null)
        {
            user.Gender = ParseGender(request.Gender);
        }

        // ── phone ── nullable; empty clears it; otherwise format-checked ──
        if (request.Phone is not null)
        {
            var phone = request.Phone.Trim();
            if (phone.Length == 0)
            {
                user.Phone = null;
            }
            else
            {
                var digitCount = phone.Count(char.IsDigit);
                if (phone.Length > PhoneMaxLength
                    || !PhoneShapeRegex.IsMatch(phone)
                    || digitCount < PhoneMinDigits
                    || digitCount > PhoneMaxDigits)
                {
                    throw new BusinessRuleException(
                        "Số điện thoại không hợp lệ. Vui lòng nhập 8–15 chữ số, có thể kèm tiền tố + và các ký tự - . ( ).",
                        "PROFILE_PHONE_INVALID");
                }
                user.Phone = phone;
            }
        }

        // ── nationality ── VISITOR only (spec §3.2 / §9, TC-16) ──
        if (request.Nationality is not null)
        {
            if (!isVisitor)
                throw new ForbiddenException("Chỉ tài khoản Visitor được phép cập nhật quốc tịch.");

            var nationality = request.Nationality.Trim();
            if (nationality.Length == 0)
            {
                user.Nationality = null;
            }
            else
            {
                if (nationality.Length > NationalityMaxLength)
                    throw new BusinessRuleException($"Quốc tịch không được vượt quá {NationalityMaxLength} ký tự.", "PROFILE_NATIONALITY_TOO_LONG");
                user.Nationality = nationality;
            }
        }

        user.UpdatedAt = _clock.UtcNow;
        user.UpdatedBy = userId;

        await _db.SaveChangesAsync(cancellationToken);

        return await ProfileResponseBuilder.BuildAsync(_db, userId, cancellationToken);
    }

    private static Gender ParseGender(string raw) => raw.Trim().ToUpperInvariant() switch
    {
        "MALE" => Gender.Male,
        "FEMALE" => Gender.Female,
        "OTHER" => Gender.Other,
        _ => throw new BusinessRuleException("Giới tính chỉ được nhận giá trị MALE, FEMALE hoặc OTHER.", "PROFILE_GENDER_INVALID"),
    };
}
