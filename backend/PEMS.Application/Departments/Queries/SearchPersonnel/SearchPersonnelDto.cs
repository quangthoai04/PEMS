using System;

namespace PEMS.Application.Departments.Queries.SearchPersonnel;

public sealed class SearchPersonnelDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string Status { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? Gender { get; set; }
    public string? Campus { get; set; }
    public string? SystemRole { get; set; }
    public string? AvatarUrl { get; set; }
    public string RawStatus { get; set; } = null!;
}
