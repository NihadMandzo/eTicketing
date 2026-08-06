using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Business.Security;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user);
}
