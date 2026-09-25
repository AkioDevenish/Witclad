using System.Collections.Generic;

namespace Wildtide.Core
{
    /// <summary>
    /// What the player can do, in grid terms, for a cautious check that every level can be finished.
    /// The Unity player is tuned to do at least this much (see PlayerController's jump numbers).
    /// </summary>
    public static class Moves
    {
        /// <summary>World units per grid cell, sideways.</summary>
        public const float Tile = 2f;
        /// <summary>World units per height step.</summary>
        public const float Step = 1f;

        /// <summary>Steps the player can climb when jumping <paramref name="distance"/> cells straight ahead (1 = next cell).</summary>
        public static int Rise(int distance, bool bounce)
        {
            if (bounce) return distance <= 2 ? 6 : 3;
            switch (distance)
            {
                case 1: return 2;
                case 2: return 1;
                case 3: return 0;
                default: return int.MinValue;
            }
        }

        public const int MaxJumpCells = 3;
    }

    /// <summary>Flood-fills the cells the player can stand on from the start, using only straight jumps.</summary>
    public static class Reach
    {
        static readonly (int dx, int dz)[] Directions = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>Height the player stands at on this cell, or -1 if it can't be stood on.</summary>
        public static int Standing(Level level, Cell c)
        {
            if (!level.InBounds(c) || level.IsSpike(c)) return -1;
            int h = level.Column(c);
            if (h >= 0) return h;
            h = level.Platform(c);
            if (h >= 0) return h;
            foreach (var m in level.Movers)
                if (OnTrack(m, c)) return m.Height;
            return -1;
        }

        static bool OnTrack(Track t, Cell c)
        {
            int along = c.Along(t.Axis), across = t.Axis == Axis.X ? c.Z : c.X;
            int home = t.Axis == Axis.X ? t.Cell.Z : t.Cell.X;
            return across == home && along >= t.From && along <= t.To;
        }

        /// <summary>Top of whatever occupies the cell (column, spike, platform), or -1 for open air.</summary>
        static int Top(Level level, Cell c)
        {
            int h = level.Column(c);
            return h >= 0 ? h : Standing(level, c);
        }

        public static HashSet<Cell> Solve(Level level)
        {
            var bouncers = new HashSet<Cell>(level.Bouncers);
            var seen = new HashSet<Cell> { level.Start };
            var queue = new Queue<Cell>();
            queue.Enqueue(level.Start);
            while (queue.Count > 0)
            {
                var from = queue.Dequeue();
                int h = Standing(level, from);
                bool bounce = bouncers.Contains(from);
                foreach (var (dx, dz) in Directions)
                {
                    for (int d = 1; d <= Moves.MaxJumpCells; d++)
                    {
                        var to = new Cell(from.X + dx * d, from.Z + dz * d);
                        if (!level.InBounds(to)) break;
                        int stand = Standing(level, to);
                        if (stand >= 0 && stand - h <= Moves.Rise(d, bounce) && seen.Add(to)) queue.Enqueue(to);
                        // Anything taller than the jump's arc blocks a longer jump.
                        if (Top(level, to) > h + (bounce ? Moves.Rise(1, true) : 0)) break;
                    }
                }
            }
            return seen;
        }
    }
}
