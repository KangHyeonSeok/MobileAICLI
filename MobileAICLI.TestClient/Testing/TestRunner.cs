using System.Text.Json;
using System.Text.Json.Serialization;
using MobileAICLI.TestClient.Services;

namespace MobileAICLI.TestClient.Testing;

/// <summary>
/// 테스트 스크립트 실행기
/// </summary>
public class TestRunner
{
    private readonly HubConnectionService _hubService;
    private readonly CommandExecutor _executor;
    private readonly bool _jsonOutput;

    public TestRunner(HubConnectionService hubService, bool jsonOutput)
    {
        _hubService = hubService;
        _executor = new CommandExecutor(hubService);
        _jsonOutput = jsonOutput;
    }

    /// <summary>
    /// 단일 명령어 실행
    /// </summary>
    public async Task<int> RunSingleCommandAsync(string command)
    {
        var result = await _executor.ExecuteAsync(command);

        if (_jsonOutput)
        {
            var output = new SingleCommandOutput
            {
                Command = command,
                Success = result.Success,
                ExitCode = result.ExitCode,
                Stdout = result.Stdout,
                Stderr = result.Stderr,
                Error = result.Error
            };
            Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
        }

        return result.Success ? 0 : 1;
    }

    /// <summary>
    /// 명령어 목록 실행 (stdin 모드)
    /// </summary>
    public async Task<int> RunCommandsAsync(List<string> commands)
    {
        var hasFailure = false;

        foreach (var command in commands)
        {
            if (string.IsNullOrWhiteSpace(command) || command.TrimStart().StartsWith("#"))
                continue;

            var result = await _executor.ExecuteAsync(command);
            if (!result.Success)
                hasFailure = true;
        }

        return hasFailure ? 1 : 0;
    }

    /// <summary>
    /// 테스트 스크립트 파일 실행
    /// </summary>
    public async Task<int> RunScriptAsync(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine($"Script file not found: {scriptPath}");
            return 3;
        }

        var lines = await File.ReadAllLinesAsync(scriptPath);
        var testCases = ParseTestCases(lines);

        if (testCases.Count == 0)
        {
            Console.Error.WriteLine("No test cases found in script");
            return 3;
        }

        var results = new List<TestCaseResult>();
        var startTime = DateTime.UtcNow;

        if (!_jsonOutput)
        {
            PrintHeader(scriptPath);
        }

        int index = 0;
        foreach (var testCase in testCases)
        {
            index++;
            var result = await RunTestCaseAsync(testCase, index, testCases.Count);
            results.Add(result);
        }

        var endTime = DateTime.UtcNow;

        if (_jsonOutput)
        {
            PrintJsonReport(scriptPath, startTime, endTime, results);
        }
        else
        {
            PrintSummary(results, endTime - startTime);
        }

        return results.Any(r => r.Status == TestStatus.Failed) ? 1 : 0;
    }

    private List<TestCase> ParseTestCases(string[] lines)
    {
        var testCases = new List<TestCase>();
        TestCase? current = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // 빈 줄이나 주석
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;

            // 새 테스트 케이스 시작
            if (trimmed.StartsWith("TEST "))
            {
                if (current != null)
                    testCases.Add(current);

                current = new TestCase { Name = trimmed[5..].Trim() };
                continue;
            }

            if (current == null)
                continue;

            // 지시문 파싱
            if (trimmed.StartsWith("EXPECT_EXIT "))
            {
                if (int.TryParse(trimmed[12..].Trim(), out var exitCode))
                    current.Assertions.Add(new Assertion(AssertionType.ExpectExit, exitCode.ToString()));
            }
            else if (trimmed.StartsWith("EXPECT_CONTAINS "))
            {
                current.Assertions.Add(new Assertion(AssertionType.ExpectContains, trimmed[16..].Trim()));
            }
            else if (trimmed.StartsWith("EXPECT_NOT_CONTAINS "))
            {
                current.Assertions.Add(new Assertion(AssertionType.ExpectNotContains, trimmed[20..].Trim()));
            }
            else if (trimmed.StartsWith("EXPECT_ERROR "))
            {
                current.Assertions.Add(new Assertion(AssertionType.ExpectError, trimmed[13..].Trim()));
            }
            else if (trimmed.StartsWith("EXPECT_REGEX "))
            {
                current.Assertions.Add(new Assertion(AssertionType.ExpectRegex, trimmed[13..].Trim()));
            }
            else if (trimmed.StartsWith("TIMEOUT "))
            {
                if (int.TryParse(trimmed[8..].Trim(), out var timeout))
                    current.TimeoutSeconds = timeout;
            }
            else if (trimmed == "SKIP")
            {
                current.Skip = true;
            }
            else if (string.IsNullOrEmpty(current.Command))
            {
                // 첫 번째 비-지시문은 명령어
                current.Command = trimmed;
            }
        }

        if (current != null)
            testCases.Add(current);

        return testCases;
    }

    private async Task<TestCaseResult> RunTestCaseAsync(TestCase testCase, int index, int total)
    {
        var result = new TestCaseResult
        {
            Name = testCase.Name,
            Command = testCase.Command
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // SKIP 처리
        if (testCase.Skip)
        {
            result.Status = TestStatus.Skipped;
            sw.Stop();
            result.Duration = sw.ElapsedMilliseconds;

            if (!_jsonOutput)
                PrintTestResult(index, total, testCase.Name, result);

            return result;
        }

        // 명령어 실행
        var commandResult = await _executor.ExecuteAsync(testCase.Command);
        sw.Stop();
        result.Duration = sw.ElapsedMilliseconds;
        result.ExitCode = commandResult.ExitCode;
        result.Stdout = commandResult.Stdout;
        result.Stderr = commandResult.Stderr;

        // Assertion 검증
        result.Status = TestStatus.Passed;
        foreach (var assertion in testCase.Assertions)
        {
            var assertionResult = ValidateAssertion(assertion, commandResult);
            result.Assertions.Add(assertionResult);

            if (!assertionResult.Passed)
            {
                result.Status = TestStatus.Failed;
                result.FailureReason = $"{assertion.Type} failed: expected '{assertion.Expected}' but got '{assertionResult.Actual}'";
            }
        }

        if (!_jsonOutput)
            PrintTestResult(index, total, testCase.Name, result);

        return result;
    }

    private AssertionResult ValidateAssertion(Assertion assertion, CommandResult commandResult)
    {
        var result = new AssertionResult
        {
            Type = assertion.Type.ToString(),
            Expected = assertion.Expected
        };

        switch (assertion.Type)
        {
            case AssertionType.ExpectExit:
                result.Actual = commandResult.ExitCode.ToString();
                result.Passed = commandResult.ExitCode.ToString() == assertion.Expected;
                break;

            case AssertionType.ExpectContains:
                var fullOutput = commandResult.Stdout + commandResult.Stderr;
                result.Actual = fullOutput.Length > 100 ? fullOutput[..100] + "..." : fullOutput;
                result.Passed = fullOutput.Contains(assertion.Expected, StringComparison.OrdinalIgnoreCase);
                break;

            case AssertionType.ExpectNotContains:
                var output = commandResult.Stdout + commandResult.Stderr;
                result.Actual = output.Length > 100 ? output[..100] + "..." : output;
                result.Passed = !output.Contains(assertion.Expected, StringComparison.OrdinalIgnoreCase);
                break;

            case AssertionType.ExpectError:
                var errorOutput = commandResult.Stderr + (commandResult.Error ?? "");
                result.Actual = errorOutput;
                result.Passed = errorOutput.Contains(assertion.Expected, StringComparison.OrdinalIgnoreCase);
                break;

            case AssertionType.ExpectRegex:
                var allOutput = commandResult.Stdout + commandResult.Stderr;
                result.Actual = allOutput.Length > 100 ? allOutput[..100] + "..." : allOutput;
                try
                {
                    result.Passed = System.Text.RegularExpressions.Regex.IsMatch(allOutput, assertion.Expected);
                }
                catch
                {
                    result.Passed = false;
                }
                break;
        }

        return result;
    }

    #region Output Formatting

    private void PrintHeader(string scriptPath)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  MobileAICLI Test Runner                                     ║");
        Console.WriteLine($"║  Server: {_hubService.IsConnected}                                              ║");
        Console.WriteLine($"║  Script: {Path.GetFileName(scriptPath),-50} ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private void PrintTestResult(int index, int total, string name, TestCaseResult result)
    {
        var status = result.Status switch
        {
            TestStatus.Passed => "✓ PASS",
            TestStatus.Failed => "✗ FAIL",
            TestStatus.Skipped => "○ SKIP",
            _ => "? UNKNOWN"
        };

        var padding = new string('.', Math.Max(1, 50 - name.Length));
        Console.WriteLine($"[{index}/{total}] {name} {padding} {status} ({result.Duration}ms)");

        if (result.Status == TestStatus.Failed && !string.IsNullOrEmpty(result.FailureReason))
        {
            Console.WriteLine($"      │ Command: {result.Command}");
            Console.WriteLine($"      │ {result.FailureReason}");
        }
    }

    private void PrintSummary(List<TestCaseResult> results, TimeSpan duration)
    {
        var passed = results.Count(r => r.Status == TestStatus.Passed);
        var failed = results.Count(r => r.Status == TestStatus.Failed);
        var skipped = results.Count(r => r.Status == TestStatus.Skipped);

        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine($" RESULTS: {passed} passed, {failed} failed, {skipped} skipped (Total: {duration.TotalSeconds:F1}s)");
        Console.WriteLine("══════════════════════════════════════════════════════════════");

        if (failed > 0)
            Console.WriteLine("\nExit code: 1 (has failures)");
        else
            Console.WriteLine("\nExit code: 0 (all passed)");
    }

    private void PrintJsonReport(string scriptPath, DateTime startTime, DateTime endTime, List<TestCaseResult> results)
    {
        var report = new TestReport
        {
            ServerUrl = "connected",
            ScriptFile = scriptPath,
            StartTime = startTime,
            EndTime = endTime,
            Summary = new TestSummary
            {
                Total = results.Count,
                Passed = results.Count(r => r.Status == TestStatus.Passed),
                Failed = results.Count(r => r.Status == TestStatus.Failed),
                Skipped = results.Count(r => r.Status == TestStatus.Skipped)
            },
            Tests = results
        };

        Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
    }

    private static JsonSerializerOptions JsonOptions => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    #endregion
}

#region Models

public class TestCase
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public List<Assertion> Assertions { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 30;
    public bool Skip { get; set; }
}

public class Assertion
{
    public AssertionType Type { get; set; }
    public string Expected { get; set; }

    public Assertion(AssertionType type, string expected)
    {
        Type = type;
        Expected = expected;
    }
}

public enum AssertionType
{
    ExpectExit,
    ExpectContains,
    ExpectNotContains,
    ExpectError,
    ExpectRegex
}

public enum TestStatus
{
    Passed,
    Failed,
    Skipped
}

public class TestCaseResult
{
    public string Name { get; set; } = "";
    public TestStatus Status { get; set; }
    public long Duration { get; set; }
    public string Command { get; set; } = "";
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
    public List<AssertionResult> Assertions { get; set; } = new();
    public string? FailureReason { get; set; }
}

public class AssertionResult
{
    public string Type { get; set; } = "";
    public string Expected { get; set; } = "";
    public string? Actual { get; set; }
    public bool Passed { get; set; }
}

public class TestReport
{
    public string ServerUrl { get; set; } = "";
    public string ScriptFile { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TestSummary Summary { get; set; } = new();
    public List<TestCaseResult> Tests { get; set; } = new();
}

public class TestSummary
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
}

public class SingleCommandOutput
{
    public string Command { get; set; } = "";
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
    public string? Error { get; set; }
}

#endregion
