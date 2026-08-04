using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Business.Security;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
