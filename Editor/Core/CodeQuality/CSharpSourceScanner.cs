using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ClarityGameOptimizer.Core
{
    /// <summary>A script handed to the Code Quality report: its project path and its text.</summary>
    internal sealed class SourceFileDescription
    {
        public SourceFileDescription(string path, string text)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A source file needs a path.", nameof(path));
            }

            Path = path.Replace('\\', '/');
            Text = text ?? "";
        }

        public string Path { get; }

        public string Text { get; }
    }

    /// <summary>What a pattern is: the costly call or construct a <see cref="CodeHit"/> found.</summary>
    internal enum CodePattern
    {
        GetComponent = 0,
        Find = 1,
        CameraMain = 2,
        Linq = 3,
        StringAllocation = 4,
        Log = 5,
        CollectionAllocation = 6,
        InstantiateOrDestroy = 7,
        AllocatingPhysicsQuery = 8,
        SendMessage = 9,
        ResourcesLoad = 10,
        TagCompare = 11,
        EmptyMessage = 12,
        PhysicsQueryWithoutMask = 13,
    }

    /// <summary>One place a pattern matched: the line, and the per-frame method it sits in (empty outside one).</summary>
    internal sealed class CodeHit
    {
        public CodeHit(CodePattern pattern, int line, string method, int methodLine)
        {
            Pattern = pattern;
            Line = line;
            Method = method ?? "";
            MethodLine = methodLine;
        }

        public CodePattern Pattern { get; }

        /// <summary>1-based.</summary>
        public int Line { get; }

        public string Method { get; }

        /// <summary>1-based line of the method's header; 0 outside a method.</summary>
        public int MethodLine { get; }
    }

    /// <summary>
    /// Brace-aware heuristics over C# text, no compiler: comments and the contents of string and character literals
    /// are blanked first (newlines kept, so line numbers hold), then the Unity messages that run every frame are found
    /// by their headers and their bodies by brace matching, and inside them the calls that cost on every frame are
    /// matched. A method called from Update but named otherwise is not followed; these are places to look, not proofs.
    /// </summary>
    internal static class CSharpSourceScanner
    {
        /// <summary>Unity messages that run every frame, or every physics step, for every enabled component.</summary>
        public static readonly string[] PerFrameMessages =
        {
            "Update", "LateUpdate", "FixedUpdate", "OnGUI", "OnTriggerStay", "OnTriggerStay2D", "OnCollisionStay", "OnCollisionStay2D", "OnWillRenderObject", "OnRenderObject",
        };

        /// <summary>The per-frame messages whose empty body still costs a native-to-managed call per object per frame.</summary>
        public static readonly string[] EmptyCheckedMessages = { "Update", "LateUpdate", "FixedUpdate", "OnGUI" };

        private static readonly Regex MessageHeader = new Regex(@"(?<![\w.])(?:void|IEnumerator)\s+(" + string.Join("|", PerFrameMessages) + @")\s*\(([^()]*)\)\s*\{", RegexOptions.Compiled);
        private static readonly Regex UsesLinq = new Regex(@"\busing\s+System\.Linq\s*;", RegexOptions.Compiled);

        private static readonly (CodePattern Pattern, Regex Regex)[] BodyPatterns =
        {
            (CodePattern.GetComponent, new Regex(@"\bGetComponents?(?:InChildren|InParent)?\s*[<(]", RegexOptions.Compiled)),
            (CodePattern.Find, new Regex(@"\b(?:GameObject\s*\.\s*Find(?:WithTag|GameObjectsWithTag)?|FindObjectsOfType|FindObjectOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType)\s*[<(]", RegexOptions.Compiled)),
            (CodePattern.CameraMain, new Regex(@"\bCamera\s*\.\s*main\b", RegexOptions.Compiled)),
            (CodePattern.CollectionAllocation, new Regex(@"\bnew\s+(?:List|Dictionary|HashSet|Queue|Stack|LinkedList|SortedList|SortedDictionary)\s*<|\bnew\s+[A-Za-z_][\w.]*\s*\[|\bnew\s*\[\s*\]", RegexOptions.Compiled)),
            (CodePattern.InstantiateOrDestroy, new Regex(@"(?<![\w.])(?:Object\s*\.\s*)?(?:Instantiate|Destroy|DestroyImmediate)\s*[<(]", RegexOptions.Compiled)),
            (CodePattern.AllocatingPhysicsQuery, new Regex(@"\bPhysics(?:2D)?\s*\.\s*(?:RaycastAll|SphereCastAll|CapsuleCastAll|BoxCastAll|OverlapSphere|OverlapBox|OverlapCapsule|OverlapCircleAll|OverlapAreaAll|OverlapPointAll|LinecastAll|CircleCastAll)\s*\(", RegexOptions.Compiled)),
            (CodePattern.SendMessage, new Regex(@"(?<![\w])(?:SendMessage|SendMessageUpwards|BroadcastMessage)\s*\(", RegexOptions.Compiled)),
            (CodePattern.ResourcesLoad, new Regex(@"\bResources\s*\.\s*Load(?:All|Async)?\s*[<(]", RegexOptions.Compiled)),
            (CodePattern.TagCompare, new Regex(@"\.\s*tag\s*[!=]=|[!=]=\s*[\w.]+\.tag\b", RegexOptions.Compiled)),
            (CodePattern.Log, new Regex(@"\bDebug\s*\.\s*(?:Log|LogWarning|LogFormat|LogWarningFormat)\s*\(|(?<![\w.])print\s*\(", RegexOptions.Compiled)),
            (CodePattern.StringAllocation, new Regex(@"\b[Ss]tring\s*\.\s*(?:Format|Concat|Join)\s*\(|\.\s*ToString\s*\(", RegexOptions.Compiled)),
        };

        private static readonly Regex LinqCall = new Regex(@"\.\s*(?:Where|Select|SelectMany|OrderBy|OrderByDescending|ThenBy|ThenByDescending|GroupBy|ToList|ToArray|ToDictionary|ToHashSet|Any|All|Count|First|FirstOrDefault|Last|LastOrDefault|Single|SingleOrDefault|Sum|Min|Max|Average|Distinct|Concat|Except|Intersect|Union|Skip|Take|Reverse|Aggregate|Cast|OfType)\s*\(", RegexOptions.Compiled);

        // Overlap queries and the argument count from which the layer mask is passed.
        private static readonly Regex OverlapCall = new Regex(@"\bPhysics\s*\.\s*(OverlapSphere|OverlapSphereNonAlloc|OverlapBox|OverlapBoxNonAlloc|OverlapCapsule|OverlapCapsuleNonAlloc)\s*\(", RegexOptions.Compiled);

        private static readonly Dictionary<string, int> MaskArgument = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "OverlapSphere", 3 },
            { "OverlapSphereNonAlloc", 4 },
            { "OverlapBox", 4 },
            { "OverlapBoxNonAlloc", 5 },
            { "OverlapCapsule", 4 },
            { "OverlapCapsuleNonAlloc", 5 },
        };

        /// <summary>A literal's span in the source: where it starts and ends, and whether it was interpolated.</summary>
        internal readonly struct LiteralSpan
        {
            public LiteralSpan(int start, int end, bool interpolated, bool character)
            {
                Start = start;
                End = end;
                Interpolated = interpolated;
                Character = character;
            }

            public int Start { get; }

            /// <summary>Exclusive.</summary>
            public int End { get; }

            public bool Interpolated { get; }

            public bool Character { get; }
        }

        /// <summary>Every hit in one file: patterns inside per-frame messages, empty messages, and overlap queries without a mask anywhere.</summary>
        public static List<CodeHit> Scan(string source)
        {
            source = source ?? "";
            var literals = new List<LiteralSpan>();
            string masked = Mask(source, literals);
            int[] lineStarts = LineStarts(source);
            bool linq = UsesLinq.IsMatch(masked);
            var hits = new List<CodeHit>();

            foreach (Match header in MessageHeader.Matches(masked))
            {
                string method = header.Groups[1].Value;
                int open = header.Index + header.Length - 1;
                int close = MatchingBrace(masked, open);
                if (close < 0)
                {
                    continue;
                }

                int methodLine = LineOf(lineStarts, header.Groups[1].Index);
                string body = masked.Substring(open + 1, close - open - 1);
                if (body.Trim().Length == 0)
                {
                    if (Array.IndexOf(EmptyCheckedMessages, method) >= 0)
                    {
                        hits.Add(new CodeHit(CodePattern.EmptyMessage, methodLine, method, methodLine));
                    }

                    continue;
                }

                int bodyStart = open + 1;
                foreach ((CodePattern pattern, Regex regex) in BodyPatterns)
                {
                    foreach (Match match in regex.Matches(body))
                    {
                        hits.Add(new CodeHit(pattern, LineOf(lineStarts, bodyStart + match.Index), method, methodLine));
                    }
                }

                if (linq)
                {
                    foreach (Match match in LinqCall.Matches(body))
                    {
                        hits.Add(new CodeHit(CodePattern.Linq, LineOf(lineStarts, bodyStart + match.Index), method, methodLine));
                    }
                }

                foreach (LiteralSpan literal in literals)
                {
                    if (literal.Start < bodyStart || literal.End > close || literal.Character)
                    {
                        continue;
                    }

                    if (literal.Interpolated || IsConcatenated(masked, literal))
                    {
                        hits.Add(new CodeHit(CodePattern.StringAllocation, LineOf(lineStarts, literal.Start), method, methodLine));
                    }
                }
            }

            foreach (Match call in OverlapCall.Matches(masked))
            {
                int open = call.Index + call.Length - 1;
                int arguments = CountArguments(masked, open);
                int mask;
                if (arguments > 0 && MaskArgument.TryGetValue(call.Groups[1].Value, out mask) && arguments < mask)
                {
                    hits.Add(new CodeHit(CodePattern.PhysicsQueryWithoutMask, LineOf(lineStarts, call.Index), "", 0));
                }
            }

            hits.Sort((a, b) => a.Line != b.Line ? a.Line.CompareTo(b.Line) : a.Pattern.CompareTo(b.Pattern));
            return hits;
        }

        /// <summary>
        /// The source with comments and the contents of string and character literals replaced by spaces, newlines
        /// kept, so offsets and line numbers match the original. Each literal's span is added to <paramref name="literals"/>.
        /// </summary>
        public static string Mask(string source, List<LiteralSpan> literals = null)
        {
            source = source ?? "";
            var masked = new StringBuilder(source);
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    int end = source.IndexOf('\n', i);
                    end = end < 0 ? source.Length : end;
                    Blank(masked, i, end);
                    i = end;
                }
                else if (c == '/' && next == '*')
                {
                    int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    end = end < 0 ? source.Length : end + 2;
                    Blank(masked, i, end);
                    i = end;
                }
                else if (c == '"' || ((c == '@' || c == '$') && (next == '"' || ((next == '@' || next == '$') && i + 2 < source.Length && source[i + 2] == '"'))))
                {
                    int prefix = c == '"' ? 0 : (next == '"' ? 1 : 2);
                    bool verbatim = source.Substring(i, prefix).IndexOf('@') >= 0;
                    bool interpolated = source.Substring(i, prefix).IndexOf('$') >= 0;
                    int start = i;
                    int end = EndOfString(source, i + prefix + 1, verbatim, interpolated);
                    Blank(masked, i + prefix + 1, Math.Max(i + prefix + 1, end - 1));
                    if (literals != null)
                    {
                        literals.Add(new LiteralSpan(start, end, interpolated, false));
                    }

                    i = end;
                }
                else if (c == '\'')
                {
                    int end = i + 1;
                    while (end < source.Length && source[end] != '\'' && source[end] != '\n')
                    {
                        end += source[end] == '\\' ? 2 : 1;
                    }

                    end = Math.Min(source.Length, end + 1);
                    Blank(masked, i + 1, Math.Max(i + 1, end - 1));
                    if (literals != null)
                    {
                        literals.Add(new LiteralSpan(i, end, false, true));
                    }

                    i = end;
                }
                else
                {
                    i++;
                }
            }

            return masked.ToString();
        }

        /// <summary>The index of the brace that closes the one at <paramref name="open"/> in masked text, or -1.</summary>
        public static int MatchingBrace(string masked, int open)
        {
            int depth = 0;
            for (int i = open; i < masked.Length; i++)
            {
                if (masked[i] == '{')
                {
                    depth++;
                }
                else if (masked[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>Top-level arguments of the call whose opening parenthesis is at <paramref name="open"/>; 0 for an empty list or no closing parenthesis.</summary>
        public static int CountArguments(string masked, int open)
        {
            int depth = 0;
            int commas = 0;
            bool any = false;
            for (int i = open; i < masked.Length; i++)
            {
                char c = masked[i];
                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return any ? commas + 1 : 0;
                    }
                }
                else if (c == ',' && depth == 1)
                {
                    commas++;
                }
                else if (depth >= 1 && !char.IsWhiteSpace(c))
                {
                    any = true;
                }
            }

            return 0;
        }

        private static int EndOfString(string source, int i, bool verbatim, bool interpolated)
        {
            int braces = 0;
            while (i < source.Length)
            {
                char c = source[i];
                if (interpolated && c == '{')
                {
                    if (i + 1 < source.Length && source[i + 1] == '{')
                    {
                        i += 2;
                        continue;
                    }

                    braces++;
                }
                else if (interpolated && c == '}' && braces > 0)
                {
                    braces--;
                }
                else if (!verbatim && c == '\\')
                {
                    i += 2;
                    continue;
                }
                else if (c == '"' && braces == 0)
                {
                    if (verbatim && i + 1 < source.Length && source[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }
                else if (!verbatim && c == '\n')
                {
                    return i;
                }

                i++;
            }

            return source.Length;
        }

        private static bool IsConcatenated(string masked, LiteralSpan literal)
        {
            int before = literal.Start - 1;
            while (before >= 0 && char.IsWhiteSpace(masked[before]))
            {
                before--;
            }

            int after = literal.End;
            while (after < masked.Length && char.IsWhiteSpace(masked[after]))
            {
                after++;
            }

            bool plusBefore = before >= 0 && masked[before] == '+' && (before == 0 || masked[before - 1] != '+');
            bool plusAfter = after < masked.Length && masked[after] == '+' && (after + 1 >= masked.Length || (masked[after + 1] != '+' && masked[after + 1] != '='));
            return plusBefore || plusAfter;
        }

        private static void Blank(StringBuilder masked, int start, int end)
        {
            for (int i = start; i < end && i < masked.Length; i++)
            {
                if (masked[i] != '\n' && masked[i] != '\r')
                {
                    masked[i] = ' ';
                }
            }
        }

        private static int[] LineStarts(string source)
        {
            var starts = new List<int> { 0 };
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            return starts.ToArray();
        }

        private static int LineOf(int[] lineStarts, int index)
        {
            int position = Array.BinarySearch(lineStarts, index);
            return (position >= 0 ? position : ~position - 1) + 1;
        }
    }
}
