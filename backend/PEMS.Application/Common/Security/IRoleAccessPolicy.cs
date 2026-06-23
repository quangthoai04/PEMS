using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Common.Security;

public interface IRoleAccessPolicy
{
    bool CanAccessAccountManagement(ICurrentUserService user);
    bool CanManageAccount(ICurrentUserService user, User targetAccount);
    
    bool CanAccessCampusManagement(ICurrentUserService user);
    bool CanManageCampus(ICurrentUserService user, Campus? campus);

    bool CanAccessDepartmentManagement(ICurrentUserService user);
    
    bool CanAccessVisitManagement(ICurrentUserService user);
    bool CanViewVisitRequest(ICurrentUserService user, VisitRequest request);
    bool CanProcessVisitRequest(ICurrentUserService user, VisitRequest request);
}
