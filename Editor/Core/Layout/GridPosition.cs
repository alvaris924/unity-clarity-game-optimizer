namespace ClarityGameOptimizer.Core
{
    /// <summary>A cell in the diagram grid: column from the left, row from the top, both 0-based.</summary>
    internal readonly struct GridPosition
    {
        public GridPosition(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public int Column { get; }

        public int Row { get; }

        public override string ToString()
        {
            return "(" + Column + ", " + Row + ")";
        }
    }
}
