using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SpaceMonopoly.Core.Ai;

public sealed record ClaudeCodeSettings
{
    /// <summary>Full path to claude.exe. Empty means search PATH and the default install folder.</summary>
    public string ExecutablePath { get; init; } = "";

    /// <summary>Model alias passed to --model (sonnet, opus, haiku) or a full model name.</summary>
    public string Model { get; init; } = "sonnet";

    /// <summary>Passed to --effort. Lower is faster.</summary>
    public string Effort { get; init; } = "low";

    public int TimeoutSeconds { get; init; } = 180;

    /// <summary>Ask Claude only when a merger or a new company is possible; Hard plays the other turns (S9).</summary>
    public bool KeyTurnsOnly { get; init; }
}

/// <summary>Sends one prompt to Claude and returns its structured JSON answer.</summary>
public interface IClaudeRunner
{
    Task<JsonElement> RunAsync(string systemPrompt, string prompt, string jsonSchema, CancellationToken cancellationToken);
}

public sealed class ClaudeCodeException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Runs Claude Code in headless mode (<c>claude -p</c>), so moves are made with the user's Claude
/// subscription login rather than an API key. Tools are disabled; Claude only reads the prompt and answers.
/// </summary>
public sealed class ClaudeCodeRunner(ClaudeCodeSettings settings) : IClaudeRunner
{
    public async Task<JsonElement> RunAsync(string systemPrompt, string prompt, string jsonSchema, CancellationToken cancellationToken)
    {
        string exe = ResolveExecutable(settings.ExecutablePath)
            ?? throw new ClaudeCodeException("Couldn't find Claude Code (claude.exe). Install it or set its path in Preferences.");

        // Run outside any project folder so no CLAUDE.md or project settings get loaded.
        string workDir = Path.Combine(Path.GetTempPath(), "SpaceMonopolyClaude");
        Directory.CreateDirectory(workDir);

        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string arg in new[]
        {
            "-p",
            "--output-format", "json",
            "--no-session-persistence",
            "--tools", "",
            "--model", settings.Model,
            "--effort", settings.Effort,
            "--system-prompt", systemPrompt,
            "--json-schema", jsonSchema,
        })
        {
            psi.ArgumentList.Add(arg);
        }
        // An API key in the environment would take priority over the subscription login.
        psi.Environment.Remove("ANTHROPIC_API_KEY");

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new ClaudeCodeException($"Couldn't start Claude Code at {exe}: {ex.Message}", ex);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        string stdout, stderr;
        try
        {
            await process.StandardInput.WriteAsync(prompt.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            stdout = await stdoutTask;
            stderr = await stderrTask;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (cancellationToken.IsCancellationRequested) throw;
            throw new ClaudeCodeException($"Claude didn't answer within {settings.TimeoutSeconds} seconds.");
        }

        return ParseResult(stdout, stderr, process.ExitCode);
    }

    internal static JsonElement ParseResult(string stdout, string stderr, int exitCode)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(stdout);
        }
        catch (JsonException)
        {
            string detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new ClaudeCodeException($"Claude Code exited with code {exitCode}: {Truncate(detail)}");
        }

        var root = doc.RootElement;
        if (root.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True)
        {
            string message = root.TryGetProperty("result", out var r) ? r.ToString() : stdout;
            throw new ClaudeCodeException($"Claude Code reported an error: {Truncate(message)}");
        }

        if (root.TryGetProperty("structured_output", out var structured) && structured.ValueKind == JsonValueKind.Object)
            return structured.Clone();

        // Fall back to parsing the text result as JSON.
        if (root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String)
        {
            try
            {
                return JsonDocument.Parse(result.GetString()!).RootElement.Clone();
            }
            catch (JsonException) { }
        }
        throw new ClaudeCodeException("Claude's answer didn't contain a move.");
    }

    public static string? ResolveExecutable(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? configured : null;

        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string name in new[] { "claude.exe", "claude" })
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim('"'), name);
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException) { }
            }
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string fallback = Path.Combine(home, ".local", "bin", OperatingSystem.IsWindows() ? "claude.exe" : "claude");
        return File.Exists(fallback) ? fallback : null;
    }

    private static string Truncate(string s) => s.Length > 400 ? s[..400] + "…" : s.Trim();
}
