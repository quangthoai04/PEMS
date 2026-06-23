using MediatR;
using PEMS.Application.Departments.Queries.SearchPersonnel; // reusing the dto

namespace PEMS.Application.Departments.Queries.ViewPersonnelDetails;

public class ViewPersonnelDetailsQuery : IRequest<SearchPersonnelDto>
{
    public ulong DepartmentId { get; set; }
    public ulong UserId { get; set; }
}
