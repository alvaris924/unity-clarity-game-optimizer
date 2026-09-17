using System.Collections.Generic;
using System.Text;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Turns any display name into an identifier that every export format accepts.
    /// The strictest consumer is archify, whose ids must match ^[a-zA-Z][a-zA-Z0-9_-]*$;
    /// DOT and Mermaid ids are a superset of that, so one slug serves all three.
    /// </summary>
    internal static class IdentifierSlug
    {
        /// <summary>Prefix used when the name has no leading letter to start the id with.</summary>
        private const char Prefix = 'n';

        private const char Separator = '-';

        /// <summary>
        /// Slugs <paramref name="name"/>: letters, digits, '_' and '-' are kept, every other run of characters
        /// becomes one '-', leading and trailing separators are dropped, and a leading digit or an empty result
        /// gets the 'n' prefix. Never returns null or an invalid id.
        /// </summary>
        public static string From(string name)
        {
            var builder = new StringBuilder(name == null ? 1 : name.Length + 1);
            bool pendingSeparator = false;

            if (name != null)
            {
                foreach (char c in name)
                {
                    if (IsIdentifierChar(c))
                    {
                        if (pendingSeparator && builder.Length > 0)
                        {
                            builder.Append(Separator);
                        }

                        pendingSeparator = false;
                        builder.Append(c);
                    }
                    else
                    {
                        pendingSeparator = true;
                    }
                }
            }

            // Trailing '-' or '_' came from the source name itself, not from a replaced run; drop them too.
            while (builder.Length > 0 && IsSeparator(builder[builder.Length - 1]))
            {
                builder.Length--;
            }

            int start = 0;
            while (start < builder.Length && IsSeparator(builder[start]))
            {
                start++;
            }

            string slug = builder.ToString(start, builder.Length - start);
            if (slug.Length == 0 || !IsAsciiLetter(slug[0]))
            {
                slug = Prefix + slug;
            }

            return slug;
        }

        /// <summary>
        /// Returns <paramref name="slug"/> unchanged when it is not in <paramref name="taken"/>, otherwise the first
        /// "slug-2", "slug-3", ... that is free. The chosen id is added to <paramref name="taken"/>.
        /// </summary>
        public static string Unique(string slug, ISet<string> taken)
        {
            string candidate = slug;
            int suffix = 2;
            while (taken.Contains(candidate))
            {
                candidate = slug + Separator + suffix;
                suffix++;
            }

            taken.Add(candidate);
            return candidate;
        }

        /// <summary>True when <paramref name="id"/> matches ^[a-zA-Z][a-zA-Z0-9_-]*$.</summary>
        public static bool IsValid(string id)
        {
            if (string.IsNullOrEmpty(id) || !IsAsciiLetter(id[0]))
            {
                return false;
            }

            for (int i = 1; i < id.Length; i++)
            {
                if (!IsIdentifierChar(id[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsIdentifierChar(char c)
        {
            return IsAsciiLetter(c) || (c >= '0' && c <= '9') || IsSeparator(c);
        }

        private static bool IsSeparator(char c)
        {
            return c == Separator || c == '_';
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }
    }
}
