using System;
using System.Collections.Generic;
using System.Text;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Builds a process argument string the way the C runtime and Node parse it on every platform: an
    /// argument with spaces or quotes is wrapped in quotes, quotes inside are escaped, and backslashes
    /// are doubled only where they precede a quote. Written here so no command ever goes through a shell.
    /// </summary>
    internal static class CommandLine
    {
        public static string Join(IEnumerable<string> arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            var text = new StringBuilder();
            foreach (string argument in arguments)
            {
                if (text.Length > 0)
                {
                    text.Append(' ');
                }

                text.Append(Quote(argument));
            }

            return text.ToString();
        }

        public static string Quote(string argument)
        {
            if (argument == null)
            {
                throw new ArgumentNullException(nameof(argument));
            }

            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '\n', '"' }) < 0)
            {
                return argument;
            }

            var text = new StringBuilder(argument.Length + 2);
            text.Append('"');
            int backslashes = 0;
            foreach (char c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    text.Append('\\', backslashes * 2 + 1);
                    text.Append('"');
                    backslashes = 0;
                    continue;
                }

                text.Append('\\', backslashes);
                text.Append(c);
                backslashes = 0;
            }

            text.Append('\\', backslashes * 2);
            text.Append('"');
            return text.ToString();
        }
    }
}
