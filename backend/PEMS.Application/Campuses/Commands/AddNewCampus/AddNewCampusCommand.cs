using MediatR;

namespace PEMS.Application.Campuses.Commands.AddNewCampus;

/// <summary>
/// UC-81 Add New Campus. HO supplies master data only — no IC head (BR-81-02); the
/// backend auto-creates the IC department in the same transaction (BR-81-03/04).
/// </summary>
public class AddNewCampusCommand : IRequest<AddNewCampusResponse>
{
    public string CampusCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string City { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Email { get; set; } = null!;
}
