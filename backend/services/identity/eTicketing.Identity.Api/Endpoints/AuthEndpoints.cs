using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;

namespace eTicketing.Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", Register).AllowAnonymous();
        group.MapPost("/login", Login).AllowAnonymous();
    }

    private static async Task<IResult> Register(RegisterRequest request, IAuthService service, CancellationToken ct)
    {
        var result = await service.RegisterAsync(request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Login(LoginRequest request, IAuthService service, CancellationToken ct)
    {
        var result = await service.LoginAsync(request, ct);
        return result.ToHttpResult();
    }
}
