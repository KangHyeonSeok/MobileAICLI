using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

/// <summary>
/// Authentication service for password verification and rate limiting
/// Uses PBKDF2 for secure password hashing
/// </summary>
public class AuthService
{
    private readonly IOptionsSnapshot<MobileAICLISettings> _settings;
    private readonly ILogger<AuthService> _logger;
    private readonly Dictionary<string, LoginAttemptInfo> _loginAttempts = new();
    private readonly string? _passwordHash;
    private static readonly string DefaultAdminHash = GeneratePasswordHash("admin");

    public AuthService(IOptionsSnapshot<MobileAICLISettings> settings, ILogger<AuthService> logger, IConfiguration configuration)
    {
        _settings = settings;
        _logger = logger;

        // Prefer configured hash; fall back to a default "admin" password hash if missing.
        _passwordHash = string.IsNullOrWhiteSpace(_settings.Value.PasswordHash)
            ? DefaultAdminHash
            : _settings.Value.PasswordHash;

        if (string.IsNullOrWhiteSpace(_settings.Value.PasswordHash))
        {
            _logger.LogWarning("Password hash not configured; using default 'admin' password. Please change it immediately in Settings.");
        }
    }

    public bool IsAuthenticationEnabled()
    {
        return _settings.Value.EnableAuthentication && !string.IsNullOrEmpty(_passwordHash);
    }

    public async Task<(bool Success, string? ErrorMessage)> ValidatePasswordAsync(string password, string ipAddress)
    {
        if (!IsAuthenticationEnabled())
        {
            _logger.LogWarning("Authentication is disabled or not configured properly");
            return (false, "Authentication is not properly configured");
        }

        // Check rate limiting
        if (IsRateLimited(ipAddress))
        {
            _logger.LogWarning("Login attempt blocked due to rate limiting for IP: {IpAddress}", MaskIpAddress(ipAddress));
            return (false, "Too many failed attempts. Please try again later.");
        }

        // Validate password
        bool isValid = VerifyPassword(password, _passwordHash!);

        if (isValid)
        {
            // Clear failed attempts on success
            _loginAttempts.Remove(ipAddress);
            _logger.LogInformation("Successful login from IP: {IpAddress}", MaskIpAddress(ipAddress));
            return (true, null);
        }
        else
        {
            // Record failed attempt
            RecordFailedAttempt(ipAddress);
            
            // Add delay to slow down brute force attacks
            await Task.Delay(TimeSpan.FromSeconds(_settings.Value.FailedLoginDelaySeconds));
            
            _logger.LogWarning("Failed login attempt from IP: {IpAddress}", MaskIpAddress(ipAddress));
            return (false, "Invalid password");
        }
    }

    private bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            // Expected format: algorithm$iterations$salt$hash
            // Example: pbkdf2$100000$base64salt$base64hash
            var parts = storedHash.Split('$');
            
            if (parts.Length != 4 || parts[0] != "pbkdf2")
            {
                _logger.LogError("Invalid password hash format");
                return false;
            }

            int iterations = int.Parse(parts[1]);
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expectedHash = Convert.FromBase64String(parts[3]);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            byte[] actualHash = pbkdf2.GetBytes(expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying password");
            return false;
        }
    }

    private bool IsRateLimited(string ipAddress)
    {
        if (!_loginAttempts.TryGetValue(ipAddress, out var info))
        {
            return false;
        }

        // Reset if last attempt was more than configured minutes ago
        if (DateTime.UtcNow - info.LastAttempt > TimeSpan.FromMinutes(_settings.Value.RateLimitResetMinutes))
        {
            _loginAttempts.Remove(ipAddress);
            return false;
        }

        return info.FailedAttempts >= _settings.Value.MaxFailedLoginAttempts;
    }

    private void RecordFailedAttempt(string ipAddress)
    {
        if (!_loginAttempts.TryGetValue(ipAddress, out var info))
        {
            info = new LoginAttemptInfo();
            _loginAttempts[ipAddress] = info;
        }

        info.FailedAttempts++;
        info.LastAttempt = DateTime.UtcNow;
    }

    /// <summary>
    /// Generates a password hash in the format expected by this service
    /// This is a utility method for administrators to generate hashes
    /// </summary>
    public static string GeneratePasswordHash(string password, int iterations = 100000)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(32);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);
        
        return $"pbkdf2${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private string MaskIpAddress(string ipAddress)
    {
        // Mask IP address for privacy (show only first two octets for IPv4)
        var parts = ipAddress.Split('.');
        if (parts.Length == 4)
        {
            return $"{parts[0]}.{parts[1]}.xxx.xxx";
        }
        // For IPv6 or other formats, show only first segment
        var segments = ipAddress.Split(':');
        if (segments.Length > 1)
        {
            return $"{segments[0]}:xxxx:xxxx:...";
        }
        return "xxx.xxx.xxx.xxx";
    }

    private class LoginAttemptInfo
    {
        public int FailedAttempts { get; set; }
        public DateTime LastAttempt { get; set; }
    }
}
