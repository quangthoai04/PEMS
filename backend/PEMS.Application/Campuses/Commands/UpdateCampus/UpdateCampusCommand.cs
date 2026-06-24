using MediatR;

namespace PEMS.Application.Campuses.Commands.UpdateCampus;

/// <summary>
/// UC-85 Update Campus — master data only. Does NOT change status, ic_head_user_id,
/// created_at/by or the IC department (BR-85-02/03/04).
/// </summary>
public class UpdateCampusCommand : IRequest<UpdateCampusResponse>
{
    public ulong CampusId { get; set; }
    public string CampusCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string City { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Email { get; set; } = null!;
}
