using System;
using System.Collections.Generic;
using System.Linq;
using Wildtide.Core;

static class Program
{
    static int passed, failed;

    static void Check(bool condition, string what)
    {
        if (condition) passed++;
        else { failed++; Console.WriteLine("FAIL: " + what); }
    }

    static int Main()
    {
        EveryLevelParses();
        EveryLevelCanBeFinished();
        TracksAreSensible();
        ParserRejectsBadLevels();
        SolverRespectsJumpLimits();
        ProgressUnlocksInOrder();
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    static Level TryParse(LevelDef def)
    {
        try { return Level.Parse(def); }
        catch (FormatException e) { Check(false, e.Message); return null; }
    }

    static void EveryLevelParses()
    {
        Check(Levels.All.Count >= 5, "at least five levels");
        Check(Levels.All.Select(l => l.Id).Distinct().Count() == Levels.All.Count, "level ids are unique");
        foreach (var def in Levels.All)
        {
            var level = TryParse(def);
            if (level == null) continue;
            Check(level.Pearls.Count >= 5, $"{def.Id}: has at least five pearls");
            Check(!string.IsNullOrEmpty(def.Name) && !string.IsNullOrEmpty(def.Hint), $"{def.Id}: has a name and hint");
            Check(level.Column(level.Start) >= 1, $"{def.Id}: start is on land");
        }
    }

    static void EveryLevelCanBeFinished()
    {
        foreach (var def in Levels.All)
        {
            var level = TryParse(def);
            if (level == null) continue;
            var reach = Reach.Solve(level);
            Check(reach.Contains(level.Goal), $"{def.Id}: goal {level.Goal} is reachable");
            foreach (var p in level.Pearls) Check(reach.Contains(p), $"{def.Id}: pearl {p} is reachable");
            foreach (var c in level.Checkpoints) Check(reach.Contains(c), $"{def.Id}: checkpoint {c} is reachable");
            foreach (var c in level.Crumbles) Check(reach.Contains(c), $"{def.Id}: crumbling stone {c} is reachable");
            foreach (var b in level.Bouncers) Check(reach.Contains(b), $"{def.Id}: bounce pad {b} is reachable");
        }
    }

    static void TracksAreSensible()
    {
        foreach (var def in Levels.All)
        {
            var level = TryParse(def);
            if (level == null) continue;
            foreach (var m in level.Movers)
            {
                Check(m.To - m.From >= 2, $"{def.Id}: raft at {m.Cell} drifts at least two cells");
                // Each end must touch land (or another platform) so the player can get on and off.
                Check(Reach.Standing(level, EndBeyond(m, m.From, -1)) >= 0, $"{def.Id}: raft at {m.Cell} starts next to land");
                Check(Reach.Standing(level, EndBeyond(m, m.To, 1)) >= 0, $"{def.Id}: raft at {m.Cell} ends next to land");
            }
            foreach (var c in level.Crabs)
            {
                Check(c.To - c.From >= 1, $"{def.Id}: crab at {c.Cell} has room to walk");
                Check(!c.Cell.Equals(level.Start), $"{def.Id}: no crab on the start");
            }
        }
    }

    static Cell EndBeyond(Track t, int along, int direction)
    {
        return t.Axis == Axis.X ? new Cell(along + direction, t.Cell.Z) : new Cell(t.Cell.X, along + direction);
    }

    static void ParserRejectsBadLevels()
    {
        void Rejects(string what, string[] heights, string[] things)
        {
            try
            {
                Level.Parse(new LevelDef { Id = "bad", Name = "Bad", Hint = "-", Heights = heights, Things = things });
                Check(false, "rejects " + what);
            }
            catch (FormatException) { Check(true, "rejects " + what); }
        }
        Rejects("no goal", new[] { "11" }, new[] { "S." });
        Rejects("two starts", new[] { "111" }, new[] { "SSG" });
        Rejects("ragged rows", new[] { "111", "11" }, new[] { "S.G", ".." });
        Rejects("row count mismatch", new[] { "111", "111" }, new[] { "S.G" });
        Rejects("pearl over the sea", new[] { "1.1" }, new[] { "SoG" });
        Rejects("unknown height", new[] { "1a1" }, new[] { "S.G" });
        Rejects("unknown thing", new[] { "111" }, new[] { "S?G" });
        Rejects("raft with no sea", new[] { "121" }, new[] { "SxG" });
    }

    static void SolverRespectsJumpLimits()
    {
        bool Reaches(string heights, string things)
        {
            var level = Level.Parse(new LevelDef { Id = "t", Name = "t", Hint = "t", Heights = new[] { heights }, Things = new[] { things } });
            return Reach.Solve(level).Contains(level.Goal);
        }
        Check(Reaches("13", "SG"), "climbs two steps");
        Check(!Reaches("14", "SG"), "can't climb three steps");
        Check(Reaches("1.2", "S.G"), "one-cell gap, one step up");
        Check(!Reaches("1.3", "S.G"), "one-cell gap, two steps up is too much");
        Check(Reaches("1..1", "S..G"), "two-cell gap on the level");
        Check(!Reaches("1...1", "S...G"), "three-cell gap is too far");
        Check(Reaches("117", "SbG"), "bounce pad climbs six steps");
        Check(Reaches("111", "S^G"), "hops over spikes");
        Check(!Reaches("17", "SG"), "no bounce pad, no big climb");
        Check(!Reaches("151", "S.G"), "a wall blocks the way");
        Check(Reaches("1.2..1", "S.x..G"), "rides a raft across");
    }

    static void ProgressUnlocksInOrder()
    {
        var p = new Progress();
        Check(p.IsUnlocked(0), "first level open");
        Check(!p.IsUnlocked(1), "second level locked");
        Check(p.RecordClear(Levels.All[0].Id, 3, 50f), "first clear is a best time");
        Check(p.IsUnlocked(1), "clearing opens the next level");
        Check(!p.RecordClear(Levels.All[0].Id, 7, 60f), "slower clear is not a best time");
        var r = p.Get(Levels.All[0].Id);
        Check(r.BestPearls == 7 && Math.Abs(r.BestTime - 50f) < 0.001f, "keeps best pearls and time separately");
        Check(p.RecordClear(Levels.All[0].Id, 1, 40f) && r.BestPearls == 7, "faster clear keeps pearl best");
        Check(p.TotalPearls() == 7, "total pearls");
        Check(!p.IsUnlocked(Levels.All.Count), "no level past the end");
        Check(Progress.FormatTime(75.46f) == "1:15.4", "time format " + Progress.FormatTime(75.46f));
    }
}
