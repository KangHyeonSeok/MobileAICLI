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
    private readonly ILogger<CopilotStreamingService> _logger;

    public CopilotStreamingService(IOptions<MobileAICLISettings> settings, ILogger<CopilotStreamingService> logger)
    {
        _settings = settings.Value;
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
    /// Copilot에 프롬프트를 보내고 응답을 실시간으로 스트리밍
    /// </summary>
    public async IAsyncEnumerable<CopilotOutput> SendPromptStreamingAsync(
        string prompt,
        CopilotToolSettings? toolSettings = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        int timeoutSeconds = 120)
    {
        // 프롬프트 검증
        if (string.IsNullOrWhiteSpace(prompt))
        {
            yield return CopilotOutput.Error("Prompt cannot be empty");
            yield return CopilotOutput.Complete(false, "Invalid prompt");
            yield break;
        }

        _logger.LogInformation("Sending prompt to Copilot: {Prompt}", TruncateForLog(prompt));

        var channel = Channel.CreateUnbounded<CopilotOutput>();

        if (_settings.EnableCopilotMock)
        {
            _ = ExecuteMockProcessAsync(prompt, channel.Writer, cancellationToken);
        }
        else
        {
            // 백그라운드에서 프로세스 실행
            _ = ExecuteCopilotProcessAsync(prompt, toolSettings, channel.Writer, timeoutSeconds, cancellationToken);
        }

        // 채널에서 결과 읽기
        await foreach (var output in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return output;
        }
    }

    private async Task ExecuteMockProcessAsync(
        string prompt,
        ChannelWriter<CopilotOutput> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken); // Thinking time
            
            var mockResponse = $"[MOCK] Copilot response for: {prompt}\n\nThis is a simulated response because EnableCopilotMock is set to true.\n\n- Item 1\n- Item 2\n- Item 3";
            
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
        ChannelWriter<CopilotOutput> writer,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            var startInfo = CreateCopilotProcessStartInfo(prompt, toolSettings);
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

    private ProcessStartInfo CreateCopilotProcessStartInfo(string prompt, CopilotToolSettings? toolSettings)
    {
        var executable = GetCopilotExecutable();
        
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = GetSafeWorkingDirectory()
        };

        // Programmatic 모드: copilot -p "prompt" --silent
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(prompt);
        startInfo.ArgumentList.Add("--silent"); // 스크립팅용 출력 (응답만)
        
        // 작업 디렉토리 접근 허용
        startInfo.ArgumentList.Add("--add-dir");
        startInfo.ArgumentList.Add(GetSafeWorkingDirectory());

        // 도구 설정 적용
        if (toolSettings != null)
        {
            ApplyToolSettings(startInfo, toolSettings);
        }
        else
        {
            // 기본값: 안전 모드 (읽기만 허용)
            // 도구 자동 승인 없이 실행하면 대화형으로 물어봄
            // Non-interactive 모드에서는 도구 사용 불가하거나 명시적 허용 필요
        }

        return startInfo;
    }

    private void ApplyToolSettings(ProcessStartInfo startInfo, CopilotToolSettings settings)
    {
        // 도구 승인 프리셋에 따른 옵션 추가
        // Phase 1.2에서 상세 구현
        // 예: --allow-tool, --deny-tool 등
        
        if (!string.IsNullOrEmpty(settings.Preset))
        {
            switch (settings.Preset.ToLowerInvariant())
            {
                case "safe":
                    // 읽기 전용 - 기본값, 추가 옵션 없음
                    break;
                case "moderate":
                    // 로컬 수정 허용
                    // startInfo.ArgumentList.Add("--allow-local-changes");
                    break;
                case "full":
                    // 모든 도구 허용
                    // startInfo.ArgumentList.Add("--allow-all-tools");
                    break;
            }
        }
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

    private string GetSafeWorkingDirectory()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_settings.RepositoryPath) && Directory.Exists(_settings.RepositoryPath))
            {
                return _settings.RepositoryPath;
            }

            var fallback = Environment.CurrentDirectory;
            _logger.LogWarning("RepositoryPath does not exist. Using fallback working directory: {WorkingDirectory}", fallback);
            return fallback;
        }
        catch
        {
            return Environment.CurrentDirectory;
        }
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
}
