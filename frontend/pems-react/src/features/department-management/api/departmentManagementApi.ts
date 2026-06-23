import httpClient from '../../../shared/api/httpClient';

export const departmentManagementApi = {
  searchPersonnel: (params: { departmentId: number; keyword?: string; status?: string; pageNumber?: number; pageSize?: number }) => {
    return httpClient.get('/Departments/searchpersonnel', { params });
  },
  
  addDepartmentPersonnel: (data: { departmentId: number; fullName: string; email: string; phone: string; gender: number; role: string }) => {
    return httpClient.post('/Departments/adddepartmentpersonnel', data);
  },
  
  updateDepartmentPersonnel: (data: { departmentId: number; userId: number; fullName: string; phone: string; gender: number }) => {
    return httpClient.put('/Departments/updatedepartmentpersonnel', data);
  },
  
  viewPersonnelDetails: (params: { departmentId: number; userId: number }) => {
    return httpClient.get('/Departments/viewpersonneldetails', { params });
  },
  
  reassignDepartmentLead: (data: { departmentId: number; newLeaderUserId: number }) => {
    return httpClient.post('/Departments/reassigndepartmentlead', data);
  },
  
  removePersonnel: (data: { departmentId: number; userId: number }) => {
    return httpClient.post('/Departments/removepersonnel', data);
  }
};
