using System;
using System.Collections.Generic;

namespace Wildtide.Core
{
    public enum Axis { X, Z }

    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X, Z;
        public Cell(int x, int z) { X = x; Z = z; }
        public Cell Step(Axis axis, int n) => axis == Axis.X ? new Cell(X + n, Z) : new Cell(X, Z + n);
        public int Along(Axis axis) => axis == Axis.X ? X : Z;
        public bool Equals(Cell o) => X == o.X && Z == o.Z;
        public override bool Equals(object o) => o is Cell c && Equals(c);
        public override int GetHashCode() => X * 7919 + Z;
        public override string ToString() => $"({X},{Z})";
    }

    /// <summary>
    /// An island as authored in <see cref="Levels"/>: two same-sized text grids, north at the top.
    /// <para><b>Heights:</b> '1'-'9' is a solid column that many steps tall, '.' is open sea.
    /// For a moving or crumbling platform the digit is the platform's height and no column is built.</para>
    /// <para><b>Things</b> (sit on top of the cell): S start, G goal, o pearl, c checkpoint, ^ sea-urchin spikes,
    /// b bounce pad, e crab walking east-west, E crab walking north-south, x platform drifting east-west,
    /// z platform drifting north-south, f crumbling platform. '.' is nothing.</para>
    /// </summary>
    public sealed class LevelDef
    {
        public string Id;
        public string Name;
        /// <summary>One line shown when the level starts.</summary>
        public string Hint;
        public string[] Heights;
        public string[] Things;
    }

    /// <summary>Something that goes back and forth along one axis between cells <see cref="From"/> and <see cref="To"/>.</summary>
    public sealed class Track
    {
        public Cell Cell;
        public Axis Axis;
        public int Height;
        public int From, To;
    }

    public sealed class Level
    {
        public LevelDef Def { get; }
        public int Width { get; }
        public int Depth { get; }
        public Cell Start { get; private set; }
        public Cell Goal { get; private set; }
        public readonly List<Cell> Pearls = new List<Cell>();
        public readonly List<Cell> Checkpoints = new List<Cell>();
        public readonly List<Cell> Spikes = new List<Cell>();
        public readonly List<Cell> Bouncers = new List<Cell>();
        public readonly List<Cell> Crumbles = new List<Cell>();
        public readonly List<Track> Movers = new List<Track>();
        public readonly List<Track> Crabs = new List<Track>();

        readonly int[,] column;   // solid column height in steps, -1 for none
        readonly int[,] platform; // height of a crumbling or moving platform authored here, -1 for none
        readonly char[,] thing;

        Level(LevelDef def, int width, int depth)
        {
            Def = def;
            Width = width;
            Depth = depth;
            column = new int[width, depth];
            platform = new int[width, depth];
            thing = new char[width, depth];
        }

        public bool InBounds(Cell c) => c.X >= 0 && c.Z >= 0 && c.X < Width && c.Z < Depth;
        /// <summary>Height of the solid column in steps, or -1 over open sea (and outside the level).</summary>
        public int Column(Cell c) => InBounds(c) ? column[c.X, c.Z] : -1;
        public int Platform(Cell c) => InBounds(c) ? platform[c.X, c.Z] : -1;
        public char Thing(Cell c) => InBounds(c) ? thing[c.X, c.Z] : '.';
        public bool IsSpike(Cell c) => Thing(c) == '^';

        public static Level Parse(LevelDef def)
        {
            if (def.Heights == null || def.Things == null || def.Heights.Length == 0)
                throw new FormatException($"{def.Id}: needs Heights and Things");
            if (def.Heights.Length != def.Things.Length)
                throw new FormatException($"{def.Id}: Heights has {def.Heights.Length} rows but Things has {def.Things.Length}");
            int depth = def.Heights.Length, width = def.Heights[0].Length;
            var level = new Level(def, width, depth);
            bool hasStart = false, hasGoal = false;

            for (int row = 0; row < depth; row++)
            {
                string h = def.Heights[row], t = def.Things[row];
                if (h.Length != width || t.Length != width)
                    throw new FormatException($"{def.Id}: row {row} must be {width} wide (Heights '{h}', Things '{t}')");
                int z = depth - 1 - row; // text is drawn north-up
                for (int x = 0; x < width; x++)
                {
                    char hc = h[x], tc = t[x];
                    int height;
                    if (hc == '.') height = -1;
                    else if (hc >= '1' && hc <= '9') height = hc - '0';
                    else throw new FormatException($"{def.Id}: unknown height '{hc}' at row {row}, column {x}");

                    var cell = new Cell(x, z);
                    level.thing[x, z] = tc;
                    level.column[x, z] = height;
                    level.platform[x, z] = -1;
                    bool needsGround = true;
                    switch (tc)
                    {
                        case '.': needsGround = false; break;
                        case 'S': if (hasStart) throw Dup(def, "S"); level.Start = cell; hasStart = true; break;
                        case 'G': if (hasGoal) throw Dup(def, "G"); level.Goal = cell; hasGoal = true; break;
                        case 'o': level.Pearls.Add(cell); break;
                        case 'c': level.Checkpoints.Add(cell); break;
                        case '^': level.Spikes.Add(cell); break;
                        case 'b': level.Bouncers.Add(cell); break;
                        case 'e': case 'E': break; // tracks are resolved once every column is known
                        case 'x': case 'z': case 'f':
                            if (height < 0) throw new FormatException($"{def.Id}: platform '{tc}' at row {row}, column {x} needs a height");
                            level.platform[x, z] = height;
                            level.column[x, z] = -1;
                            if (tc == 'f') level.Crumbles.Add(cell);
                            break;
                        default: throw new FormatException($"{def.Id}: unknown thing '{tc}' at row {row}, column {x}");
                    }
                    if (needsGround && height < 0)
                        throw new FormatException($"{def.Id}: '{tc}' at row {row}, column {x} is over open sea");
                }
            }
            if (!hasStart) throw new FormatException($"{def.Id}: no start (S)");
            if (!hasGoal) throw new FormatException($"{def.Id}: no goal (G)");

            for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                var cell = new Cell(x, z);
                char tc = level.thing[x, z];
                if (tc == 'x' || tc == 'z') level.Movers.Add(level.MoverTrack(cell, tc == 'x' ? Axis.X : Axis.Z));
                if (tc == 'e' || tc == 'E') level.Crabs.Add(level.CrabTrack(cell, tc == 'e' ? Axis.X : Axis.Z));
            }
            return level;
        }

        static FormatException Dup(LevelDef def, string what) => new FormatException($"{def.Id}: more than one {what}");

        /// <summary>A drifting platform spans the open sea around it on its axis, touching the land at each end.</summary>
        Track MoverTrack(Cell cell, Axis axis)
        {
            bool Open(Cell c) => InBounds(c) && Column(c) < 0 && (c.Equals(cell) || Thing(c) == '.');
            int from = cell.Along(axis), to = from;
            while (Open(cell.Step(axis, from - cell.Along(axis) - 1))) from--;
            while (Open(cell.Step(axis, to - cell.Along(axis) + 1))) to++;
            if (to - from < 1) throw new FormatException($"{Def.Id}: platform at {cell} has no open sea to drift across");
            return new Track { Cell = cell, Axis = axis, Height = platform[cell.X, cell.Z], From = from, To = to };
        }

        /// <summary>A crab walks the flat, spike-free ground on its axis.</summary>
        Track CrabTrack(Cell cell, Axis axis)
        {
            int h = Column(cell);
            bool Walkable(Cell c) => Column(c) == h && !IsSpike(c);
            int from = cell.Along(axis), to = from;
            while (Walkable(cell.Step(axis, from - cell.Along(axis) - 1))) from--;
            while (Walkable(cell.Step(axis, to - cell.Along(axis) + 1))) to++;
            return new Track { Cell = cell, Axis = axis, Height = h, From = from, To = to };
        }
    }
}
