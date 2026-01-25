using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Services.Database;
using eTicketing.Services.Database.Entities;
using eTicketing.Services.Helpers;
using eTicketing.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Services;

public class AuthService : IAuthService
{
    private readonly eTicketingDbContext _context;
    private readonly JwtHelper _jwtHelper;

    public AuthService(eTicketingDbContext context, JwtHelper jwtHelper)
    {
        _context = context;
        _jwtHelper = jwtHelper;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        // Check if email already exists
        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
        if (emailExists)
        {
            throw new InvalidOperationException("Email adresa je već registrovana");
        }

        // Check if username already exists
        var usernameExists = await _context.Users.AnyAsync(u => u.Username == request.Username);
        if (usernameExists)
        {
            throw new InvalidOperationException("Korisničko ime je već zauzeto");
        }

        // Create password hash
        PasswordHelper.CreatePasswordHash(request.Password, out string passwordHash, out string passwordSalt);

        // Generate verification code (6-digit OTP)
        var verificationCode = GenerateOTP();
        var otpExpiration = DateTime.UtcNow.AddMinutes(30);

        // Create user with basic user role (Role ID 5)
        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Username = request.Username,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            IsActive = true,
            IsEmailVerified = false,
            OTP = verificationCode,
            OTPExpiration = otpExpiration,
            RoleId = 5, // Basic User role
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // TODO: Send verification email with code
        // For now, the code will be in the database

        return new RegisterResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Message = $"Registracija uspješna. Molimo verifikujte email adresu pomoću koda poslanog na {user.Email}"
        };
    }

    public async Task<bool> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || user.IsEmailVerified || string.IsNullOrWhiteSpace(user.OTP) || 
            user.OTPExpiration < DateTime.UtcNow || user.OTP != request.Code)
        {
            throw new InvalidOperationException("Neispravan kod za verifikaciju");
        }

        // Mark email as verified and clear OTP
        user.IsEmailVerified = true;
        user.OTP = null;
        user.OTPExpiration = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        // Find user by email or username
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Email == request.EmailOrUsername || u.Username == request.EmailOrUsername);

        if (user == null || !PasswordHelper.VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new UnauthorizedAccessException("Neispravni pristupni podaci");
        }

        // Check if user is active
        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Neispravni pristupni podaci");
        }

        // Check if email is verified
        if (!user.IsEmailVerified)
        {
            throw new UnauthorizedAccessException("Molimo verifikujte email adresu prije prijave");
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Generate JWT token
        var token = _jwtHelper.GenerateToken(user);

        return new LoginResponse
        {
            Token = token
        };
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {
            // Don't reveal if email exists or not for security reasons
            return true;
        }

        // Generate reset code (6-digit OTP)
        var resetCode = GenerateOTP();
        var otpExpiration = DateTime.UtcNow.AddMinutes(30);

        user.OTP = resetCode;
        user.OTPExpiration = otpExpiration;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // TODO: Send reset code email
        // For now, the code will be in the database

        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || string.IsNullOrWhiteSpace(user.OTP) || 
            !user.OTPExpiration.HasValue || user.OTPExpiration.Value < DateTime.UtcNow || user.OTP != request.Code)
        {
            throw new InvalidOperationException("Neispravan kod za resetovanje lozinke");
        }

        // Create new password hash
        PasswordHelper.CreatePasswordHash(request.NewPassword, out string passwordHash, out string passwordSalt);

        user.PasswordHash = passwordHash;
        user.PasswordSalt = passwordSalt;
        user.OTP = null;
        user.OTPExpiration = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            throw new InvalidOperationException("Korisnik nije pronađen");
        }

        // Verify current password
        if (!PasswordHelper.VerifyPasswordHash(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            throw new UnauthorizedAccessException("Trenutna lozinka nije ispravna");
        }

        // Create new password hash
        PasswordHelper.CreatePasswordHash(request.NewPassword, out string passwordHash, out string passwordSalt);

        user.PasswordHash = passwordHash;
        user.PasswordSalt = passwordSalt;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<UserResponse?> GetMeAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return null;
        }

        return new UserResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            RoleName = user.Role.Name,
            OrganizationId = user.OrganizationId,
            OrganizationName = user.Organization?.Name,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }

    private static string GenerateOTP()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }
}
