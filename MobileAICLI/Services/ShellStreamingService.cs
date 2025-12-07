using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

/// <summary>
/// 쉘 명령을 실행하고 출력을 실시간으로 스트리밍하는 서비스
/// Phase 1.1.1: 기본 구조 검증용
/// </summary>
public class ShellStreamingService
{
    private readonly RepositoryContext _context;
    private readonly ILogger<ShellStreamingService> _logger;

    public ShellStreamingService(RepositoryContext context, ILogger<ShellStreamingService> logger)
    public ShellStreamingService(IOptionsSnapshot<MobileAICLISettings> settings, ILogger<ShellStreamingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 명령을 실행하고 출력을 실시간으로 스트리밍
    /// </summary>
    public async IAsyncEnumerable<ShellOutput> ExecuteStreamingAsync(
        string command,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        int timeoutSeconds = 30)
    {
        // 명령 검증
        if (string.IsNullOrWhiteSpace(command))
        {
            yield return ShellOutput.Error("Command cannot be empty");
            yield return ShellOutput.Complete(-1);
            yield break;
        }

        _logger.LogInformation("Executing streaming command: {Command}", command);

        var channel = System.Threading.Channels.Channel.CreateUnbounded<ShellOutput>();
        
        // 백그라운드에서 프로세스 실행
        _ = ExecuteProcessAsync(command, channel.Writer, timeoutSeconds, cancellationToken);

        // 채널에서 결과 읽기
        await foreach (var output in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return output;
        }
    }

    private async Task ExecuteProcessAsync(
        string command,
        System.Threading.Channels.ChannelWriter<ShellOutput> writer,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            var startInfo = CreateProcessStartInfo(command);
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
            
            await writer.WriteAsync(ShellOutput.Complete(process.ExitCode), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Command execution cancelled or timed out: {Command}", command);
            
            if (process != null && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to kill process");
                }
            }
            
            await writer.WriteAsync(ShellOutput.Error("Command execution timed out or was cancelled"), CancellationToken.None);
            await writer.WriteAsync(ShellOutput.Complete(-1), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Command}", command);
            await writer.WriteAsync(ShellOutput.Error($"Error: {ex.Message}"), CancellationToken.None);
            await writer.WriteAsync(ShellOutput.Complete(-1), CancellationToken.None);
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
        System.Threading.Channels.ChannelWriter<ShellOutput> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                var output = isError ? ShellOutput.Error(line) : ShellOutput.Output(line);
                await writer.WriteAsync(output, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 취소됨 - 정상 처리
        }
    }

    private ProcessStartInfo CreateProcessStartInfo(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _context.GetAbsolutePath()
        };

        // OS에 따라 쉘 설정
        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command);
        }
        else
        {
            startInfo.FileName = "/bin/bash";
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(command);
        }

        return startInfo;
    }
}

/// <summary>
/// 쉘 출력 타입
/// </summary>
public record ShellOutput
{
    public ShellOutputType Type { get; init; }
    public string Content { get; init; } = string.Empty;
    public int? ExitCode { get; init; }

    public static ShellOutput Output(string content) => new() { Type = ShellOutputType.Output, Content = content };
    public static ShellOutput Error(string content) => new() { Type = ShellOutputType.Error, Content = content };
    public static ShellOutput Complete(int exitCode) => new() { Type = ShellOutputType.Complete, ExitCode = exitCode };
}

public enum ShellOutputType
{
    Output,
    Error,
    Complete
}
