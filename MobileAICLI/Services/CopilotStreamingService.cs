using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

/// <summary>
/// Copilot CLI를 실행하고 출력을 실시간으로 스트리밍하는 서비스
/// Phase 1.1.2: Copilot CLI 통합
/// </summary>
public class CopilotStreamingService
{
    private readonly MobileAICLISettings _settings;
    private readonly RepositoryContext _context;
    private readonly ILogger<CopilotStreamingService> _logger;

    public CopilotStreamingService(IOptionsSnapshot<MobileAICLISettings> settings, RepositoryContext context, ILogger<CopilotStreamingService> logger)
    {
        _settings = settings.Value;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Copilot CLI 설치 여부 확인
    /// </summary>
    public async Task<(bool Installed, string Version, string Error)> CheckInstallationAsync()
    {
        if (_settings.EnableCopilotMock)
        {
            return (true, "0.0.0-mock", string.Empty);
        }

        try
        {
            var executable = GetCopilotExecutable();
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = GetSafeWorkingDirectory()
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                // 출력에서 버전 정보 추출 (예: "0.0.367")
                var version = output.Trim().Split('\n')[0];
                return (true, version, string.Empty);
            }

            return (false, string.Empty, string.IsNullOrEmpty(error) ? "Copilot CLI not found" : error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check Copilot CLI installation");
            return (false, string.Empty, $"Copilot CLI not installed. Run: npm install -g @github/copilot");
        }
    }

    /// <summary>
    /// GitHub 인증 상태 확인
    /// </summary>
    public async Task<(bool Authenticated, string User, string Error)> CheckAuthStatusAsync()
    {
        if (_settings.EnableCopilotMock)
        {
            return (true, "mock-user", string.Empty);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _settings.GitHubCliPath,
                Arguments = "auth status",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = GetSafeWorkingDirectory()
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // gh auth status는 성공해도 stderr에 출력함
            var combined = output + error;
            
            if (process.ExitCode == 0 || combined.Contains("Logged in"))
            {
                // "Logged in to github.com account username" 형태에서 사용자명 추출
                var userMatch = System.Text.RegularExpressions.Regex.Match(combined, @"account\s+(\S+)");
                var user = userMatch.Success ? userMatch.Groups[1].Value : "authenticated";
                return (true, user, string.Empty);
            }

            return (false, string.Empty, "Not authenticated. Run 'gh auth login' first.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check GitHub auth status");
            return (false, string.Empty, $"Error checking auth status: {ex.Message}");
        }
    }

    /// <summary>
    /// Send prompt to Copilot and stream response in real-time
    /// </summary>
    public async IAsyncEnumerable<CopilotOutput> SendPromptStreamingAsync(
        string prompt,
        CopilotToolSettings? toolSettings = null,
        string? model = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        int timeoutSeconds = 120)
    {
        // Prompt validation
        if (string.IsNullOrWhiteSpace(prompt))
        {
            yield return CopilotOutput.Error("Prompt cannot be empty");
            yield return CopilotOutput.Complete(false, "Invalid prompt");
            yield break;
        }

        _logger.LogInformation("Sending prompt to Copilot: {Prompt}", TruncateForLog(prompt));

        var channel = Channel.CreateUnbounded<CopilotOutput>();

        // Model validation and default value setting
        var validatedModel = ValidateAndGetModel(model);

        if (_settings.EnableCopilotMock)
        {
            _ = ExecuteMockProcessAsync(prompt, validatedModel, channel.Writer, cancellationToken);
        }
        else
        {
            // Execute process in background
            _ = ExecuteCopilotProcessAsync(prompt, toolSettings, validatedModel, channel.Writer, timeoutSeconds, cancellationToken);
        }

        // Read results from channel
        await foreach (var output in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return output;
        }
    }

    private async Task ExecuteMockProcessAsync(
        string prompt,
        string model,
        ChannelWriter<CopilotOutput> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken); // Thinking time
            
            var mockResponse = $"[MOCK] Copilot response (Model: {model}) for: {prompt}\n\nThis is a simulated response because EnableCopilotMock is set to true.\n\n- Item 1\n- Item 2\n- Item 3";
            
            // Simulate streaming
            foreach (var word in mockResponse.Split(' '))
            {
                await writer.WriteAsync(CopilotOutput.Output(word + " "), cancellationToken);
                await Task.Delay(50, cancellationToken);
            }
            
            await writer.WriteAsync(CopilotOutput.Complete(true), cancellationToken);
        }
        catch (Exception ex)
        {
            await writer.WriteAsync(CopilotOutput.Error(ex.Message), cancellationToken);
            await writer.WriteAsync(CopilotOutput.Complete(false, ex.Message), cancellationToken);
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task ExecuteCopilotProcessAsync(
        string prompt,
        CopilotToolSettings? toolSettings,
        string model,
        ChannelWriter<CopilotOutput> writer,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            var startInfo = CreateCopilotProcessStartInfo(prompt, toolSettings, model);
            process = new Process { StartInfo = startInfo };

            process.Start();

            // 타임아웃 설정
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // stdout과 stderr를 병렬로 읽기
            var outputTask = ReadStreamToChannelAsync(process.StandardOutput, false, writer, linkedCts.Token);
            var errorTask = ReadStreamToChannelAsync(process.StandardError, true, writer, linkedCts.Token);

            await Task.WhenAll(outputTask, errorTask);

            // 프로세스 종료 대기
            await process.WaitForExitAsync(linkedCts.Token);

            var success = process.ExitCode == 0;
            await writer.WriteAsync(CopilotOutput.Complete(success, success ? null : $"Exit code: {process.ExitCode}"), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Copilot command cancelled or timed out");

            if (process != null && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to kill Copilot process");
                }
            }

            await writer.WriteAsync(CopilotOutput.Error("Request timed out or was cancelled"), CancellationToken.None);
            await writer.WriteAsync(CopilotOutput.Complete(false, "Timeout"), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Copilot command");
            await writer.WriteAsync(CopilotOutput.Error($"Error: {ex.Message}"), CancellationToken.None);
            await writer.WriteAsync(CopilotOutput.Complete(false, ex.Message), CancellationToken.None);
        }
        finally
        {
            process?.Dispose();
            writer.Complete();
        }
    }

    private async Task ReadStreamToChannelAsync(
        StreamReader reader,
        bool isError,
        ChannelWriter<CopilotOutput> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            // 라인 단위가 아닌 청크 단위로 읽기 (더 실시간성 있는 스트리밍)
            var buffer = new char[256];
            int charsRead;
            
            while ((charsRead = await reader.ReadAsync(buffer, cancellationToken)) > 0)
            {
                var content = new string(buffer, 0, charsRead);
                var output = isError ? CopilotOutput.Error(content) : CopilotOutput.Output(content);
                await writer.WriteAsync(output, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 취소됨 - 정상 처리
        }
    }

    private ProcessStartInfo CreateCopilotProcessStartInfo(string prompt, CopilotToolSettings? toolSettings, string model)
    {
        var executable = GetCopilotExecutable();
        
        // 작업 디렉토리 결정
        var workingDir = toolSettings?.CopilotSettings?.WorkingDirectory;
        var finalWorkingDir = GetSafeWorkingDirectory(workingDir);
        
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = finalWorkingDir
        };

        // Programmatic mode: copilot -p "prompt" --silent
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(prompt);
        startInfo.ArgumentList.Add("--silent"); // Scripting output (response only)
        
        // Model selection (only add if not default)
        if (!string.IsNullOrEmpty(model) && model != "default")
        {
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(model);
        }
        
        // Allow working directory access
        startInfo.ArgumentList.Add("--add-dir");
        startInfo.ArgumentList.Add(finalWorkingDir);

        // Apply tool settings
        if (toolSettings != null)
        {
            ApplyToolSettings(startInfo, toolSettings);
        }
        else
        {
            // Default: safe mode (read-only)
            // Without auto-approval, tools will prompt interactively
            // In non-interactive mode, tools are disabled or require explicit approval
        }

        return startInfo;
    }

    private void ApplyToolSettings(ProcessStartInfo startInfo, CopilotToolSettings settings)
    {
        // Phase 1.2: 상세 설정 적용
        if (settings.CopilotSettings != null)
        {
            var copilotSettings = settings.CopilotSettings;
            
            // CLI 옵션 추가
            var cliOptions = copilotSettings.BuildCliOptions();
            if (!string.IsNullOrWhiteSpace(cliOptions))
            {
                // 공백으로 분리된 옵션들을 개별 인자로 추가
                foreach (var option in SplitCliOptions(cliOptions))
                {
                    startInfo.ArgumentList.Add(option);
                }
            }
            
            // GitHub 토큰 환경변수 설정
            var (tokenKey, tokenValue) = copilotSettings.GetGithubTokenEnv();
            if (tokenKey != null)
            {
                startInfo.Environment[tokenKey] = tokenValue ?? string.Empty;
            }
            
            return;
        }
        // Apply options based on tool approval preset
        // Detailed implementation in Phase 1.2
        // Example: --allow-tool, --deny-tool, etc.
        
        // 레거시: 프리셋 기반 설정
        if (!string.IsNullOrEmpty(settings.Preset))
        {
            switch (settings.Preset.ToLowerInvariant())
            {
                case "safe":
                    // Read-only - default, no additional options
                    break;
                case "moderate":
                    // 로컬 수정 허용
                    break;
                case "full":
                    // 모든 도구 허용
                    startInfo.ArgumentList.Add("--allow-all-tools");
                    // Allow local modifications
                    // startInfo.ArgumentList.Add("--allow-local-changes");
                    break;
            }
        }
    }
    
    private static string[] SplitCliOptions(string options)
    {
        // --allow-tool 'shell(cat)' --allow-tool 'write' 형태를 파싱
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        
        foreach (var ch in options)
        {
            if (ch == '\'')
            {
                inQuotes = !inQuotes;
                current.Append(ch);
            }
            else if (ch == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }
        
        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }
        
        return result.ToArray();
    }

    private string GetCopilotExecutable()
    {
        // 설정된 명령 사용 (기본값: "gh copilot" 또는 "copilot")
        var command = _settings.GitHubCopilotCommand;
        
        if (string.IsNullOrWhiteSpace(command))
        {
            return "copilot"; // 기본 실행 파일
        }

        // "gh copilot" 형태인 경우 첫 번째 단어만 반환
        // 나머지는 인자로 처리해야 함
        return command.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private string GetSafeWorkingDirectory(string? customPath = null)
    {
        try
        {
            var workingDir = _context.GetAbsolutePath();
            
            if (Directory.Exists(workingDir))
            // 커스텀 경로가 지정되었으면 우선 사용
            if (!string.IsNullOrWhiteSpace(customPath) && Directory.Exists(customPath))
            {
                return customPath;
            }
            
            if (!string.IsNullOrWhiteSpace(_settings.RepositoryPath) && Directory.Exists(_settings.RepositoryPath))
            {
                return workingDir;
            }

            // OS별 기본 Documents 디렉토리
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documentsPath) && Directory.Exists(documentsPath))
            {
                return documentsPath;
            }

            var fallback = Environment.CurrentDirectory;
            _logger.LogWarning("Context working directory does not exist. Using fallback: {WorkingDirectory}", fallback);
            return fallback;
        }
        catch
        {
            return Environment.CurrentDirectory;
        }
    }

    /// <summary>
    /// Validates model name and returns default value if model is not allowed
    /// </summary>
    private string ValidateAndGetModel(string? model)
    {
        var validatedModel = _settings.ValidateModel(model);
        
        // Log warning if model was not allowed and fallback occurred
        if (!string.IsNullOrWhiteSpace(model) && validatedModel != model)
        {
            _logger.LogWarning("Requested model '{Model}' is not in the allowed list. Falling back to default model '{DefaultModel}'",
                model, _settings.CopilotModel);
        }
        
        return validatedModel;
    }

    private static string TruncateForLog(string text, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }
}

/// <summary>
/// Copilot 출력 타입
/// </summary>
public record CopilotOutput
{
    public CopilotOutputType Type { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool? Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static CopilotOutput Output(string content) => new() { Type = CopilotOutputType.Output, Content = content };
    public static CopilotOutput Error(string content) => new() { Type = CopilotOutputType.Error, Content = content };
    public static CopilotOutput Complete(bool success, string? error = null) => new() { Type = CopilotOutputType.Complete, Success = success, ErrorMessage = error };
}

public enum CopilotOutputType
{
    Output,
    Error,
    Complete
}

/// <summary>
/// Copilot 도구 설정 (Phase 1.2에서 확장)
/// </summary>
public class CopilotToolSettings
{
    /// <summary>
    /// 프리셋: "safe", "moderate", "full"
    /// </summary>
    public string Preset { get; set; } = "safe";

    /// <summary>
    /// 허용된 도구 목록 (세분화 제어용)
    /// </summary>
    public List<string> AllowedTools { get; set; } = new();

    /// <summary>
    /// 차단된 도구 목록
    /// </summary>
    public List<string> DeniedTools { get; set; } = new();

    /// <summary>
    /// 상세 설정 (Phase 1.2)
    /// </summary>
    public MobileAICLI.Models.CopilotSettings? CopilotSettings { get; set; }
}
