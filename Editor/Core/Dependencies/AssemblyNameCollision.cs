using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>One assembly name that two or more files claim: an .asmdef and a .dll, or two copies of a .dll.</summary>
    internal sealed class AssemblyNameCollision
    {
        public AssemblyNameCollision(string name, IEnumerable<string> paths)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A collision needs the assembly name.", nameof(name));
            }

            Name = name;
            Paths = new List<string>(paths ?? Array.Empty<string>());
            if (Paths.Count < 2)
            {
                throw new ArgumentException("A collision needs at least two paths.", nameof(paths));
            }
        }

        public string Name { get; }

        public List<string> Paths { get; }
    }
}
