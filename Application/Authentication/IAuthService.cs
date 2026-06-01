using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Authentication
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
    }
}
