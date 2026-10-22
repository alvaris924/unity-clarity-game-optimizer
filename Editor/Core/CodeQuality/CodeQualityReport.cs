using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Turns the project's scripts into the Code Quality report: the calls that cost on every frame inside the Unity
    /// messages that run every frame (component and object lookups, LINQ, string building, logging, collection and
    /// object churn, allocating physics queries, SendMessage, Resources.Load, tag strings), the per-frame messages
    /// left empty, and the overlap queries anywhere that collect every collider because no layer mask is passed. One
    /// row per pattern per method, with the count and every line as evidence. Frame time is the budget. Pure: the
    /// Editor side only supplies the files.
    /// </summary>
    internal static class CodeQualityReport
    {
        public const string Scope = "Project scripts";

        public const string EmptyMessageCheck = "code.empty-unity-message";
        public const string NoMaskCheck = "code.physics-query-no-mask";

        public static Report Build(IReadOnlyList<SourceFileDescription> files)
        {
            files = files ?? Array.Empty<SourceFileDescription>();
            var report = new Report(Area.CodeQuality, Scope);
            long lines = 0;
            int perFrameMethods = 0;
            int perFrameHits = 0;
            int empty = 0;
            int noMask = 0;

            foreach (SourceFileDescription file in files.OrderBy(f => f.Path, StringComparer.Ordinal))
            {
                lines += CountLines(file.Text);
                List<CodeHit> hits = CSharpSourceScanner.Scan(file.Text);
                perFrameMethods += CountMethods(file.Text);

                foreach (CodeHit hit in hits.Where(h => h.Pattern == CodePattern.EmptyMessage))
                {
                    empty++;
                    var finding = new Finding(EmptyMessageCheck, file.Path + ":" + hit.Line.ToString(CultureInfo.InvariantCulture))
                    {
                        Title = "An empty " + hit.Method + "(): Unity still calls it on every enabled instance, every frame",
                        Severity = Severity.Advice,
                        Budget = Budget.FrameTime,
                        Verdict = "Delete the empty " + hit.Method + " method",
                    };
                    finding.Evidence.Add(new Evidence(file.Path, hit.Line, 0, hit.Method));
                    report.Findings.Add(finding);
                }

                foreach (CodeHit hit in hits.Where(h => h.Pattern == CodePattern.PhysicsQueryWithoutMask).GroupBy(h => h.Line).Select(g => g.First()))
                {
                    noMask++;
                    var finding = new Finding(NoMaskCheck, file.Path + ":" + hit.Line.ToString(CultureInfo.InvariantCulture))
                    {
                        Title = "An overlap query without a layer mask: it collects every collider in range (ground, buildings, ragdolls, pickups) only for the loop to skip most of them",
                        Severity = Severity.Advice,
                        Budget = Budget.FrameTime,
                        Verdict = "Pass the layer mask of what the code keeps, and the NonAlloc overload with a reused buffer",
                    };
                    finding.Evidence.Add(new Evidence(file.Path, hit.Line, 0, "query"));
                    report.Findings.Add(finding);
                }

                foreach (IGrouping<string, CodeHit> method in hits.Where(h => h.MethodLine > 0 && h.Pattern != CodePattern.EmptyMessage && h.Pattern != CodePattern.PhysicsQueryWithoutMask)
                             .GroupBy(h => h.Method + "@" + h.MethodLine.ToString(CultureInfo.InvariantCulture), StringComparer.Ordinal))
                {
                    foreach (IGrouping<CodePattern, CodeHit> pattern in method.GroupBy(h => h.Pattern))
                    {
                        List<CodeHit> occurrences = pattern.ToList();
                        perFrameHits += occurrences.Count;
                        CodeHit first = occurrences[0];
                        var finding = new Finding(CheckOf(pattern.Key), file.Path + ":" + first.MethodLine.ToString(CultureInfo.InvariantCulture))
                        {
                            Title = first.Method + "() " + Describe(pattern.Key, occurrences.Count),
                            Severity = SeverityOf(pattern.Key),
                            Budget = Budget.FrameTime,
                            Measured = Measurement.Count(occurrences.Count),
                            Verdict = VerdictOf(pattern.Key),
                        };
                        foreach (int line in occurrences.Select(o => o.Line).Distinct().Take(8))
                        {
                            finding.Evidence.Add(new Evidence(file.Path, line, 0, first.Method));
                        }

                        report.Findings.Add(finding);
                    }
                }
            }

            report.Metrics.Add(new Metric("scripts.scanned", Measurement.Count(files.Count), Budget.None, "Scripts scanned"));
            report.Metrics.Add(new Metric("scripts.lines", Measurement.Count(lines), Budget.None, "Lines"));
            report.Metrics.Add(new Metric("methods.per-frame", Measurement.Count(perFrameMethods), Budget.None, "Per-frame Unity messages"));
            report.Metrics.Add(new Metric("hits.per-frame", Measurement.Count(perFrameHits), Budget.None, "Costly calls inside them"));
            report.Metrics.Add(new Metric("messages.empty", Measurement.Count(empty), Budget.None, "Empty per-frame messages"));
            report.Metrics.Add(new Metric("queries.no-mask", Measurement.Count(noMask), Budget.None, "Overlap queries without a layer mask"));

            report.Notes.Add("Heuristics over the source text, not a compiler: only the Unity messages that run every frame (" + string.Join(", ", CSharpSourceScanner.PerFrameMessages) + ") are searched, a method they call is not followed, and a call in a branch that rarely runs still counts. Treat the rows as places to look.");
            report.Notes.Add("Third-party folders from Project Settings, Editor folders and the Demo, Example and Sample folders of asset packs are left out. A cost inside a script attached to many objects (enemies, bullets, pickups) is paid once per instance.");
            return report;
        }

        public static string CheckOf(CodePattern pattern)
        {
            switch (pattern)
            {
                case CodePattern.GetComponent:
                    return "code.per-frame.get-component";
                case CodePattern.Find:
                    return "code.per-frame.find";
                case CodePattern.CameraMain:
                    return "code.per-frame.camera-main";
                case CodePattern.Linq:
                    return "code.per-frame.linq";
                case CodePattern.StringAllocation:
                    return "code.per-frame.string";
                case CodePattern.Log:
                    return "code.per-frame.log";
                case CodePattern.CollectionAllocation:
                    return "code.per-frame.allocation";
                case CodePattern.InstantiateOrDestroy:
                    return "code.per-frame.instantiate";
                case CodePattern.AllocatingPhysicsQuery:
                    return "code.per-frame.physics-allocation";
                case CodePattern.SendMessage:
                    return "code.per-frame.send-message";
                case CodePattern.ResourcesLoad:
                    return "code.per-frame.resources-load";
                case CodePattern.TagCompare:
                    return "code.per-frame.tag-compare";
                case CodePattern.EmptyMessage:
                    return EmptyMessageCheck;
                default:
                    return NoMaskCheck;
            }
        }

        private static Severity SeverityOf(CodePattern pattern)
        {
            switch (pattern)
            {
                case CodePattern.GetComponent:
                case CodePattern.Find:
                case CodePattern.Linq:
                case CodePattern.CollectionAllocation:
                case CodePattern.InstantiateOrDestroy:
                case CodePattern.AllocatingPhysicsQuery:
                case CodePattern.SendMessage:
                case CodePattern.ResourcesLoad:
                    return Severity.Warning;
                case CodePattern.CameraMain:
                    return Severity.Info;
                default:
                    return Severity.Advice;
            }
        }

        private static string Describe(CodePattern pattern, int count)
        {
            string times = count == 1 ? "once" : count.ToString(CultureInfo.InvariantCulture) + " times";
            switch (pattern)
            {
                case CodePattern.GetComponent:
                    return "looks up components " + times + " every frame";
                case CodePattern.Find:
                    return "searches the scene for objects " + times + " every frame";
                case CodePattern.CameraMain:
                    return "reads Camera.main " + times + " every frame";
                case CodePattern.Linq:
                    return "runs LINQ " + times + " every frame: each query allocates an enumerator and its delegates";
                case CodePattern.StringAllocation:
                    return "builds strings " + times + " every frame (interpolation, concatenation, ToString, Format)";
                case CodePattern.Log:
                    return "logs " + times + " every frame";
                case CodePattern.CollectionAllocation:
                    return "allocates a collection or array " + times + " every frame";
                case CodePattern.InstantiateOrDestroy:
                    return "instantiates or destroys objects " + times + " every frame";
                case CodePattern.AllocatingPhysicsQuery:
                    return "calls an allocating physics query " + times + " every frame";
                case CodePattern.SendMessage:
                    return "uses SendMessage " + times + " every frame";
                case CodePattern.ResourcesLoad:
                    return "calls Resources.Load " + times + " every frame";
                case CodePattern.TagCompare:
                    return "compares tags as strings " + times + " every frame";
                default:
                    return times;
            }
        }

        private static string VerdictOf(CodePattern pattern)
        {
            switch (pattern)
            {
                case CodePattern.GetComponent:
                    return "Look the component up once (Awake or when the reference changes) and keep it in a field";
                case CodePattern.Find:
                    return "Find the objects once, or have them register themselves with a manager";
                case CodePattern.CameraMain:
                    return "Cheap since Unity 2020.2, which caches it; keep a field if a profile still shows it";
                case CodePattern.Linq:
                    return "Replace it with a loop over a reused list";
                case CodePattern.StringAllocation:
                    return "Build the text only when the value changes, or use TMP_Text.SetText with a format and numbers";
                case CodePattern.Log:
                    return "Remove it, or keep it behind a define that release builds leave out: every call formats a string and, with stack traces on, walks the stack";
                case CodePattern.CollectionAllocation:
                    return "Allocate it once in a field and Clear() it each frame";
                case CodePattern.InstantiateOrDestroy:
                    return "Take the objects from a pool and return them to it";
                case CodePattern.AllocatingPhysicsQuery:
                    return "Use the NonAlloc overload with a buffer kept in a field, and pass a layer mask";
                case CodePattern.SendMessage:
                    return "Call the method on a cached reference, or raise an event";
                case CodePattern.ResourcesLoad:
                    return "Load it once and keep the reference";
                case CodePattern.TagCompare:
                    return "Use CompareTag, which does not allocate the tag string";
                default:
                    return "";
            }
        }

        private static int CountMethods(string text)
        {
            int count = 0;
            string masked = CSharpSourceScanner.Mask(text);
            foreach (string message in CSharpSourceScanner.PerFrameMessages)
            {
                int index = 0;
                string needle = message + "(";
                while ((index = masked.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
                {
                    int before = index - 1;
                    while (before >= 0 && char.IsWhiteSpace(masked[before]))
                    {
                        before--;
                    }

                    if (before >= 3 && masked.Substring(before - 3, 4) == "void" && (before - 4 < 0 || !char.IsLetterOrDigit(masked[before - 4])))
                    {
                        count++;
                    }

                    index += needle.Length;
                }
            }

            return count;
        }

        private static long CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            long count = 1;
            foreach (char c in text)
            {
                if (c == '\n')
                {
                    count++;
                }
            }

            return count;
        }
    }
}
