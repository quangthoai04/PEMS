using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Queries.GetDepartmentAssigneeCandidates
{
    public class CandidateDto
    {
        public ulong UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class GetDepartmentAssigneeCandidatesQuery : IRequest<List<CandidateDto>>
    {
    }

    public class GetDepartmentAssigneeCandidatesQueryHandler : IRequestHandler<GetDepartmentAssigneeCandidatesQuery, List<CandidateDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetDepartmentAssigneeCandidatesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<CandidateDto>> Handle(GetDepartmentAssigneeCandidatesQuery request, CancellationToken cancellationToken)
        {
            ulong userId = _currentUserService.UserId.Value;
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (currentUser == null) return new List<CandidateDto>();

            var candidates = await _context.Users.AsNoTracking()
                .Where(u => u.DepartmentId == currentUser.DepartmentId 
                            && u.Status == "ACTIVE" 
                            && u.Role.RoleCode == "DEPARTMENT")
                .Select(u => new CandidateDto
                {
                    UserId = u.UserId,
                    Name = u.FullName,
                    Email = u.Email
                })
                .ToListAsync(cancellationToken);

            return candidates;
        }
    }
}
