using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>Where every node of a graph sits on the grid, as computed by <see cref="LayeredLayout"/>.</summary>
    internal sealed class GraphLayout
    {
        private readonly Dictionary<string, GridPosition> _positions;
        private readonly int[] _rowsPerColumn;

        internal GraphLayout(Dictionary<string, GridPosition> positions, int[] rowsPerColumn, int backEdgesIgnored)
        {
            _positions = positions ?? throw new ArgumentNullException(nameof(positions));
            _rowsPerColumn = rowsPerColumn ?? throw new ArgumentNullException(nameof(rowsPerColumn));
            BackEdgesIgnored = backEdgesIgnored;
        }

        /// <summary>Number of columns; 0 for an empty graph.</summary>
        public int Columns
        {
            get { return _rowsPerColumn.Length; }
        }

        /// <summary>Edges that would have closed a cycle and were left out of the layering. Zero for a dependency graph Unity accepts.</summary>
        public int BackEdgesIgnored { get; }

        public int Rows(int column)
        {
            return _rowsPerColumn[column];
        }

        public GridPosition Position(string id)
        {
            GridPosition position;
            if (!TryGetPosition(id, out position))
            {
                throw new KeyNotFoundException("The layout has no node '" + id + "'.");
            }

            return position;
        }

        public bool TryGetPosition(string id, out GridPosition position)
        {
            if (id == null)
            {
                position = default(GridPosition);
                return false;
            }

            return _positions.TryGetValue(id, out position);
        }
    }
}
