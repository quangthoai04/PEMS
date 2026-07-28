using System.Linq;
using FluentValidation;
using PEMS.Application.DepartmentLeaderPersonnel.Common;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.ListDepartmentPersonnel;

/// <summary>
/// Shape validation for the list query (spec §18). Structural only — scope is enforced in the handler,
/// which is the authority regardless of what passes here.
/// </summary>
public sealed class ListDepartmentPersonnelQueryValidator : AbstractValidator<ListDepartmentPersonnelQuery>
{
    public ListDepartmentPersonnelQueryValidator()
    {
        RuleFor(q => q.Keyword)
            .MaximumLength(DepartmentPersonnelListRules.MaxKeywordLength)
            .WithMessage($"Từ khóa tìm kiếm không được vượt quá {DepartmentPersonnelListRules.MaxKeywordLength} ký tự.");

        RuleFor(q => q.Status)
            .Must(DepartmentPersonnelListRules.IsSupportedStatusFilter)
            .WithMessage("Bộ lọc trạng thái không hợp lệ.");

        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Số trang phải lớn hơn hoặc bằng 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, DepartmentPersonnelListRules.MaxPageSize)
            .WithMessage($"Kích thước trang phải nằm trong khoảng 1 đến {DepartmentPersonnelListRules.MaxPageSize}.");

        RuleFor(q => q.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy)
                            || DepartmentPersonnelListRules.AllowedSortColumns.Contains(sortBy.Trim().ToLowerInvariant()))
            .WithMessage("Cột sắp xếp không được hỗ trợ.");

        RuleFor(q => q.SortDirection)
            .Must(dir => string.IsNullOrWhiteSpace(dir)
                         || dir.Trim().ToLowerInvariant() is "asc" or "desc")
            .WithMessage("Chiều sắp xếp chỉ nhận giá trị asc hoặc desc.");
    }
}
