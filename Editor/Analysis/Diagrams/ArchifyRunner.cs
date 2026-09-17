using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ClarityGameOptimizer.Core;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Runs the archify command line through Node without a shell, waits with a timeout, and reads the
    /// verdict out of its JSON output. Every call is one process: <c>node archify.mjs &lt;args&gt;</c>.
    /// </summary>
    internal static class ArchifyRunner
    {
        public const string NodeExecutable = "node";
        public const int DefaultTimeoutMilliseconds = 120000;

        private static readonly Regex OkPattern = new Regex("\"ok\"\\s*:\\s*(true|false)", RegexOptions.Compiled);
        private static readonly Regex StatusPattern = new Regex("\"status\"\\s*:\\s*\"([a-z]+)\"", RegexOptions.Compiled);

        /// <summary>Validates a diagram file at the profile the exporters write for. Passes when archify reports ok and a passing composition.</summary>
        public static ArchifyResult Validate(ArchifyLocation archify, string diagramType, string inputPath)
        {
            return Run(archify, null, "validate", diagramType, inputPath, "--quality", ArchifyArchitectureExporter.QualityProfile, "--json");
        }

        /// <summary>Renders a diagram file to HTML. archify replaces the output atomically and keeps the last good one on failure.</summary>
        public static ArchifyResult Deliver(ArchifyLocation archify, string diagramType, string inputPath, string outputPath)
        {
            return Run(archify, null, "deliver", diagramType, inputPath, outputPath, "--quality", ArchifyArchitectureExporter.QualityProfile, "--json");
        }

        /// <summary>The Node version on the path, or null when Node cannot be started.</summary>
        public static string NodeVersion()
        {
            ArchifyResult result = Execute(NodeExecutable, new[] { "--version" }, null, 15000);
            return result.Started && result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
        }

        public static ArchifyResult Run(ArchifyLocation archify, string workingDirectory, params string[] arguments)
        {
            if (archify == null)
            {
                throw new ArgumentNullException(nameof(archify));
            }

            var all = new List<string> { archify.Script };
            all.AddRange(arguments);
            return Execute(NodeExecutable, all, workingDirectory, DefaultTimeoutMilliseconds);
        }

        private static ArchifyResult Execute(string executable, IReadOnlyList<string> arguments, string workingDirectory, int timeoutMilliseconds)
        {
            string commandLine = executable + " " + CommandLine.Join(arguments);
            var info = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = CommandLine.Join(arguments),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                info.WorkingDirectory = workingDirectory;
            }

            var output = new StringBuilder();
            var error = new StringBuilder();
            try
            {
                using (var process = new Process { StartInfo = info })
                {
                    process.OutputDataReceived += (sender, line) => Append(output, line.Data);
                    process.ErrorDataReceived += (sender, line) => Append(error, line.Data);
                    if (!process.Start())
                    {
                        return ArchifyResult.NotStarted(commandLine, "The process did not start.");
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        return new ArchifyResult(commandLine, true, -1, output.ToString(), error.ToString(), true);
                    }

                    process.WaitForExit();
                    return new ArchifyResult(commandLine, true, process.ExitCode, output.ToString(), error.ToString(), false);
                }
            }
            catch (Win32Exception exception)
            {
                return ArchifyResult.NotStarted(commandLine, exception.Message);
            }
        }

        private static void Append(StringBuilder text, string line)
        {
            if (line == null)
            {
                return;
            }

            lock (text)
            {
                text.Append(line).Append('\n');
            }
        }

        internal static bool? ParseOk(string json)
        {
            Match match = OkPattern.Match(json ?? "");
            if (!match.Success)
            {
                return null;
            }

            return match.Groups[1].Value == "true";
        }

        internal static string ParseStatus(string json)
        {
            Match match = StatusPattern.Match(json ?? "");
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    /// <summary>What one archify call produced.</summary>
    internal sealed class ArchifyResult
    {
        public ArchifyResult(string commandLine, bool started, int exitCode, string standardOutput, string standardError, bool timedOut)
        {
            CommandLine = commandLine ?? "";
            Started = started;
            ExitCode = exitCode;
            StandardOutput = standardOutput ?? "";
            StandardError = standardError ?? "";
            TimedOut = timedOut;
        }

        public static ArchifyResult NotStarted(string commandLine, string reason)
        {
            return new ArchifyResult(commandLine, false, -1, "", reason, false);
        }

        public string CommandLine { get; }

        public bool Started { get; }

        public int ExitCode { get; }

        public string StandardOutput { get; }

        public string StandardError { get; }

        public bool TimedOut { get; }

        /// <summary>True when the process ran, exited with 0, and its JSON says ok with no failing composition.</summary>
        public bool Succeeded
        {
            get
            {
                if (!Started || TimedOut || ExitCode != 0)
                {
                    return false;
                }

                bool? ok = ArchifyRunner.ParseOk(StandardOutput);
                string status = ArchifyRunner.ParseStatus(StandardOutput);
                return ok != false && status != "fail";
            }
        }

        /// <summary>One line for the console: what ran and how it ended.</summary>
        public string Summary
        {
            get
            {
                if (!Started)
                {
                    return "could not start: " + StandardError.Trim();
                }

                if (TimedOut)
                {
                    return "timed out";
                }

                if (ExitCode != 0)
                {
                    string detail = StandardError.Trim().Length > 0 ? StandardError.Trim() : StandardOutput.Trim();
                    return "exit code " + ExitCode + (detail.Length > 0 ? ": " + FirstLine(detail) : "");
                }

                return Succeeded ? "ok" : "reported problems: " + FirstLine(StandardOutput.Trim());
            }
        }

        private static string FirstLine(string text)
        {
            int index = text.IndexOfAny(new[] { '\r', '\n' });
            return index < 0 ? text : text.Substring(0, index);
        }
    }
}
