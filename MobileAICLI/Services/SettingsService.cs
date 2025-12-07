using Microsoft.Extensions.Options;
using MobileAICLI.Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace MobileAICLI.Services;

public class SettingsService
{
    private readonly IOptionsSnapshot<MobileAICLISettings> _settings;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsFilePath;

    public SettingsService(
        IOptionsSnapshot<MobileAICLISettings> settings, 
        IConfiguration configuration,
        ILogger<SettingsService> logger,
        IWebHostEnvironment env)
    {
        _settings = settings;
        _configuration = configuration;
        _logger = logger;
        _settingsFilePath = Path.Combine(env.ContentRootPath, "appsettings.json");
    }

    public MobileAICLISettings GetCurrentSettings()
    {
        return _settings.Value;
    }

    public async Task<SettingsUpdateResult> UpdateSettingsAsync(SettingsUpdateRequest request)
    {
        var result = new SettingsUpdateResult();
        var validationErrors = new List<string>();

        try
        {
            // Validate settings
            if (request.RepositoryPath != null)
            {
                if (!Directory.Exists(request.RepositoryPath))
                {
                    validationErrors.Add($"Repository path does not exist: {request.RepositoryPath}");
                }
            }

            if (request.GitHubCopilotCommand != null)
            {
                if (string.IsNullOrWhiteSpace(request.GitHubCopilotCommand))
                {
                    validationErrors.Add("GitHub Copilot command cannot be empty");
                }
            }

            if (request.GitHubCliPath != null)
            {
                if (!File.Exists(request.GitHubCliPath) && !IsCommandInPath(request.GitHubCliPath))
                {
                    validationErrors.Add($"GitHub CLI path not found: {request.GitHubCliPath}");
                }
            }

            if (request.GitCliPath != null)
            {
                if (!File.Exists(request.GitCliPath) && !IsCommandInPath(request.GitCliPath))
                {
                    validationErrors.Add($"Git CLI path not found: {request.GitCliPath}");
                }
            }

            if (request.AllowedShellCommands != null)
            {
                var dangerousCommands = new[] { "rm -rf", "dd", "mkfs", ":(){ :|:& };:" };
                foreach (var cmd in request.AllowedShellCommands)
                {
                    if (dangerousCommands.Any(d => cmd.Contains(d, StringComparison.OrdinalIgnoreCase)))
                    {
                        validationErrors.Add($"Dangerous command not allowed: {cmd}");
                    }
                }
            }

            if (request.AllowedWorkRoots != null)
            {
                foreach (var root in request.AllowedWorkRoots)
                {
                    if (!Directory.Exists(root.Replace("*", "")))
                    {
                        _logger.LogWarning("Allowed work root may not exist: {Root}", root);
                    }
                }
            }

            if (validationErrors.Any())
            {
                result.Success = false;
                result.ValidationErrors = validationErrors;
                result.Message = "Validation failed";
                return result;
            }

            // Read current settings file
            var settingsJson = await File.ReadAllTextAsync(_settingsFilePath);
            var settingsDoc = JsonDocument.Parse(settingsJson);
            
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                
                foreach (var property in settingsDoc.RootElement.EnumerateObject())
                {
                    if (property.Name == "MobileAICLI")
                    {
                        writer.WritePropertyName("MobileAICLI");
                        writer.WriteStartObject();
                        
                        foreach (var settingProp in property.Value.EnumerateObject())
                        {
                            var propName = settingProp.Name;
                            
                            if (propName == "RepositoryPath" && request.RepositoryPath != null)
                            {
                                writer.WriteString(propName, request.RepositoryPath);
                            }
                            else if (propName == "GitHubCopilotCommand" && request.GitHubCopilotCommand != null)
                            {
                                writer.WriteString(propName, request.GitHubCopilotCommand);
                            }
                            else if (propName == "GitHubCliPath" && request.GitHubCliPath != null)
                            {
                                writer.WriteString(propName, request.GitHubCliPath);
                            }
                            else if (propName == "GitCliPath" && request.GitCliPath != null)
                            {
                                writer.WriteString(propName, request.GitCliPath);
                            }
                            else if (propName == "AllowedShellCommands" && request.AllowedShellCommands != null)
                            {
                                writer.WritePropertyName(propName);
                                writer.WriteStartArray();
                                foreach (var cmd in request.AllowedShellCommands)
                                {
                                    writer.WriteStringValue(cmd);
                                }
                                writer.WriteEndArray();
                            }
                            else if (propName == "AllowedWorkRoots" && request.AllowedWorkRoots != null)
                            {
                                writer.WritePropertyName(propName);
                                writer.WriteStartArray();
                                foreach (var root in request.AllowedWorkRoots)
                                {
                                    writer.WriteStringValue(root);
                                }
                                writer.WriteEndArray();
                            }
                            else
                            {
                                // Copy existing value
                                settingProp.WriteTo(writer);
                            }
                        }
                        
                        // Add new properties if they don't exist
                        if (request.GitCliPath != null && !property.Value.TryGetProperty("GitCliPath", out _))
                        {
                            writer.WriteString("GitCliPath", request.GitCliPath);
                        }
                        if (request.AllowedWorkRoots != null && !property.Value.TryGetProperty("AllowedWorkRoots", out _))
                        {
                            writer.WritePropertyName("AllowedWorkRoots");
                            writer.WriteStartArray();
                            foreach (var root in request.AllowedWorkRoots)
                            {
                                writer.WriteStringValue(root);
                            }
                            writer.WriteEndArray();
                        }
                        
                        writer.WriteEndObject();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }
                
                writer.WriteEndObject();
            }
            
            var updatedJson = Encoding.UTF8.GetString(stream.ToArray());
            await File.WriteAllTextAsync(_settingsFilePath, updatedJson);
            
            _logger.LogInformation("Settings updated successfully");
            
            result.Success = true;
            result.Message = "Settings updated successfully. Restart may be required for changes to take effect.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings");
            result.Success = false;
            result.Message = $"Error updating settings: {ex.Message}";
            return result;
        }
    }

    public async Task<SettingsUpdateResult> ChangePasswordAsync(PasswordChangeRequest request)
    {
        var result = new SettingsUpdateResult();

        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || 
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                result.Success = false;
                result.Message = "Current and new passwords are required";
                return result;
            }

            if (request.NewPassword.Length < 8)
            {
                result.Success = false;
                result.Message = "New password must be at least 8 characters";
                return result;
            }

            var currentSettings = _settings.Value;
            
            // Verify current password
            if (!string.IsNullOrEmpty(currentSettings.PasswordHash))
            {
                if (!VerifyPassword(request.CurrentPassword, currentSettings.PasswordHash))
                {
                    _logger.LogWarning("Failed password change attempt");
                    result.Success = false;
                    result.Message = "Current password is incorrect";
                    return result;
                }
            }

            // Generate new password hash
            var newHash = HashPassword(request.NewPassword);

            // Update settings with new hash
            var updateRequest = new SettingsUpdateRequest();
            
            // Read and update the settings file with new password hash
            var settingsJson = await File.ReadAllTextAsync(_settingsFilePath);
            var settingsDoc = JsonDocument.Parse(settingsJson);
            
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                
                foreach (var property in settingsDoc.RootElement.EnumerateObject())
                {
                    if (property.Name == "MobileAICLI")
                    {
                        writer.WritePropertyName("MobileAICLI");
                        writer.WriteStartObject();
                        
                        foreach (var settingProp in property.Value.EnumerateObject())
                        {
                            if (settingProp.Name == "PasswordHash")
                            {
                                writer.WriteString("PasswordHash", newHash);
                            }
                            else
                            {
                                settingProp.WriteTo(writer);
                            }
                        }
                        
                        // Add PasswordHash if it doesn't exist
                        if (!property.Value.TryGetProperty("PasswordHash", out _))
                        {
                            writer.WriteString("PasswordHash", newHash);
                        }
                        
                        writer.WriteEndObject();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }
                
                writer.WriteEndObject();
            }
            
            var updatedJson = Encoding.UTF8.GetString(stream.ToArray());
            await File.WriteAllTextAsync(_settingsFilePath, updatedJson);
            
            _logger.LogInformation("Password changed successfully");
            
            result.Success = true;
            result.Message = "Password changed successfully";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            result.Success = false;
            result.Message = $"Error changing password: {ex.Message}";
            return result;
        }
    }

    private string HashPassword(string password)
    {
        // Use PBKDF2 with HMACSHA256
        const int saltSize = 16;
        const int hashSize = 32;
        const int iterations = 100000;

        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[saltSize];
        rng.GetBytes(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(hashSize);

        var hashBytes = new byte[saltSize + hashSize];
        Array.Copy(salt, 0, hashBytes, 0, saltSize);
        Array.Copy(hash, 0, hashBytes, saltSize, hashSize);

        return Convert.ToBase64String(hashBytes);
    }

    private bool VerifyPassword(string password, string passwordHash)
    {
        try
        {
            const int saltSize = 16;
            const int hashSize = 32;
            const int iterations = 100000;

            var hashBytes = Convert.FromBase64String(passwordHash);
            
            if (hashBytes.Length != saltSize + hashSize)
            {
                return false;
            }

            var salt = new byte[saltSize];
            Array.Copy(hashBytes, 0, salt, 0, saltSize);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(hashSize);

            for (int i = 0; i < hashSize; i++)
            {
                if (hashBytes[i + saltSize] != hash[i])
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsCommandInPath(string command)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
        {
            return false;
        }

        var paths = pathVar.Split(Path.PathSeparator);
        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path, command);
            if (File.Exists(fullPath))
            {
                return true;
            }
        }

        return false;
    }
}
