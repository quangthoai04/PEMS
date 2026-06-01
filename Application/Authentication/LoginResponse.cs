using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Authentication
{
    public class LoginResponse
    {
        public string Token { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string RoleCode { get; set; } = null!;
        public string? CampusCode { get; set; }
    }
}
