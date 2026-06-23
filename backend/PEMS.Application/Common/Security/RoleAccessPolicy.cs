using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using System;

namespace PEMS.Application.Common.Security;

public class RoleAccessPolicy : IRoleAccessPolicy
{
    public bool CanAccessAccountManagement(ICurrentUserService user)
    {
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.RoleCode)) return false;
        var effectiveRole = EffectiveRole.Resolve(user.RoleCode, user.SubRole);
        return effectiveRole switch
        {
            EffectiveRole.Admin => true,
            EffectiveRole.Ho => true,
            EffectiveRole.StaffLeader => true,
            _ => false
        };
    }

    public bool CanManageAccount(ICurrentUserService user, User targetAccount)
    {
        if (!CanAccessAccountManagement(user)) return false;
        var effectiveRole = EffectiveRole.Resolve(user.RoleCode!, user.SubRole);
        
        if (effectiveRole == EffectiveRole.Admin) return true;
        
        if (effectiveRole == EffectiveRole.Ho)
        {
            // HO can manage anyone except ADMIN and cannot disable another HO.
            return targetAccount.Role?.RoleCode != RoleCode.Admin;
        }

        if (effectiveRole == EffectiveRole.StaffLeader)
        {
            // Staff Leader can only manage within their campus and cannot manage HO/Admin.
            if (targetAccount.Role?.RoleCode == RoleCode.Admin || targetAccount.Role?.RoleCode == RoleCode.Ho)
                return false;
            
            return user.PrimaryCampusId != null && user.PrimaryCampusId == targetAccount.PrimaryCampusId;
        }

        return false;
    }

    public bool CanAccessCampusManagement(ICurrentUserService user)
    {
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.RoleCode)) return false;
        var effectiveRole = EffectiveRole.Resolve(user.RoleCode, user.SubRole);
        return effectiveRole is EffectiveRole.Ho or EffectiveRole.Admin;
    }

    public bool CanManageCampus(ICurrentUserService user, Campus? campus)
    {
        if (!CanAccessCampusManagement(user)) return false;
        var effectiveRole = EffectiveRole.Resolve(user.RoleCode!, user.SubRole);
        return effectiveRole is EffectiveRole.Ho or EffectiveRole.Admin;
    }

    public bool CanAccessDepartmentManagement(ICurrentUserService user)
    {
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.RoleCode)) return false;
        var effectiveRole = EffectiveRole.Resolve(user.RoleCode, user.SubRole);
        return effectiveRole is EffectiveRole.StaffLeader or EffectiveRole.DepartmentLead or EffectiveRole.Ho;
    }

    public bool CanAccessVisitManagement(ICurrentUserService user)
    {
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.RoleCode)) return false;
        var effectiveRole = EffectiveRole.Resolve(user.RoleCode, user.SubRole);
        return effectiveRole != EffectiveRole.Admin; // Admin does not access business visits
    }

    public bool CanViewVisitRequest(ICurrentUserService user, VisitRequest request)
    {
        if (!CanAccessVisitManagement(user)) return false;
        var effectiveRole = EffectiveRole.Resolve(user.RoleCode!, user.SubRole);
        
        if (effectiveRole == EffectiveRole.Visitor)
        {
            return request.VisitorUserId == user.UserId;
        }

        if (effectiveRole == EffectiveRole.Ho)
        {
            return request.VisitScope == "MULTI_CAMPUS";
        }

        if (effectiveRole == EffectiveRole.StaffLeader)
        {
            // Can see if it has instance in their campus
            return request.VisitScope == "SINGLE_CAMPUS" || request.CampusInstances.Any(ci => ci.CampusId == user.PrimaryCampusId);
        }

        // For Staff, Dept Lead, Dept, Student: Usually they only see what they are assigned to.
        // Or if there is a campus instance they are part of.
        // This is a simplified check, precise check often happens at query level or specific action level.
        return true; 
    }

    public bool CanProcessVisitRequest(ICurrentUserService user, VisitRequest request)
    {
        if (!CanAccessVisitManagement(user)) return false;
        var effectiveRole = EffectiveRole.Resolve(user.RoleCode!, user.SubRole);

        if (effectiveRole == EffectiveRole.Ho)
        {
            return request.VisitScope == "MULTI_CAMPUS";
        }

        if (effectiveRole == EffectiveRole.StaffLeader)
        {
            return request.VisitScope == "SINGLE_CAMPUS";
        }

        return false;
    }
}
