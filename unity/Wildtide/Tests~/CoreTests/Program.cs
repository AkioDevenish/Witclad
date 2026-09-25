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

    /// <summary>Plays back fixed values so a test controls every roll.</summary>
    sealed class ScriptedRng : IRng
    {
        readonly float value;
        public ScriptedRng(float value) { this.value = value; }
        public int Range(int min, int max) => min + (int)((max - min) * value);
        public float Value() => value;
    }

    static int Main()
    {
        DatabaseIsConsistent();
        TypeChartTriangle();
        StatsAndLevels();
        DamageFormula();
        BattleToVictory();
        LosingSendsYouHome();
        FaintForcesSwitch();
        Capture();
        RunAway();
        Evolution();
        SaveHelpers();
        FullRandomBattles();
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    static void DatabaseIsConsistent()
    {
        foreach (var s in Database.Species.Values)
        {
            foreach (var l in s.Learnset) Check(Database.Moves.ContainsKey(l.MoveId), $"{s.Id} learns unknown move {l.MoveId}");
            if (s.EvolvesTo != null)
            {
                Check(Database.Species.ContainsKey(s.EvolvesTo), $"{s.Id} evolves into unknown {s.EvolvesTo}");
                Check(s.EvolveLevel > 1, $"{s.Id} evolve level");
            }
            Check(s.CatchRate is >= 1 and <= 255, $"{s.Id} catch rate");
            Check(Creature.Create(s.Id, 5).Moves.Count > 0, $"{s.Id} knows a move at level 5");
        }
        foreach (var slot in Database.WindmillMeadows) Check(Database.Species.ContainsKey(slot.SpeciesId), $"encounter {slot.SpeciesId}");
        foreach (var id in Database.Starters) Check(Database.Species.ContainsKey(id), $"starter {id}");
        Check(Database.Species.Count == 16, "16 species in the slice");
        Check(Database.Lanterns.Count == 5, "five lantern tiers");
    }

    static void TypeChartTriangle()
    {
        Check(TypeChart.Multiplier(Element.Flame, Element.Verdant) == 2f, "flame beats verdant");
        Check(TypeChart.Multiplier(Element.Verdant, Element.Tide) == 2f, "verdant beats tide");
        Check(TypeChart.Multiplier(Element.Tide, Element.Flame) == 2f, "tide beats flame");
        Check(TypeChart.Multiplier(Element.Flame, Element.Tide) == 0.5f, "flame resisted by tide");
        Check(TypeChart.Multiplier(Element.Storm, Element.Stone) == 0.5f, "storm resisted by stone");
        Check(TypeChart.Multiplier(Element.Gale, Element.Flame) == 1f, "neutral");
        Check(TypeChart.Multiplier(Element.Flame, Element.Verdant, Element.Venom) == 2f, "dual type neutral second");
        Check(TypeChart.Multiplier(Element.Iron, Element.Frost, Element.Stone) == 4f, "dual weakness stacks");
        foreach (Element a in Enum.GetValues(typeof(Element)))
            foreach (Element d in Enum.GetValues(typeof(Element)))
                Check(TypeChart.Multiplier(a, d) is 0.5f or 1f or 2f, $"{a} vs {d} in range");
    }

    static void StatsAndLevels()
    {
        var c = Creature.Create("cindlet", 5);
        Check(c.Level == 5 && c.Xp == 125, "level 5 starts with 125 xp");
        Check(c.Hp == c.MaxHp && c.MaxHp == 2 * 44 * 5 / 100 + 5 + 10, "hp formula");
        Check(c.Knows("tackle") && c.Knows("ember") && !c.Knows("scale-flare"), "level 5 moves");
        var log = c.GainXp(Creature.XpForLevel(7) - c.Xp);
        Check(c.Level == 7, "levels twice");
        Check(log.Count(l => l.Contains("grew to level")) == 2, "two level-up lines");
        var high = Creature.Create("cindlet", 14);
        Check(high.Moves.Count == 4 && high.Knows("scale-flare") && !high.Knows("tackle"), "oldest move replaced");
        Check(Creature.Create("cindlet", 500).Level == 100, "level clamps");
    }

    static void DamageFormula()
    {
        var narlet = new Battler(Creature.Create("narlet", 10));
        var cindlet = new Battler(Creature.Create("cindlet", 10));
        var bubble = Database.GetMove("bubble");
        int super = Battle.Damage(narlet, cindlet, bubble, false, 2f, 1f);
        int neutral = Battle.Damage(narlet, cindlet, bubble, false, 1f, 1f);
        Check(super > neutral, "super effective hits harder");
        int crit = Battle.Damage(narlet, cindlet, bubble, true, 1f, 1f);
        Check(crit > neutral, "crit hits harder");
        int tackle = Battle.Damage(narlet, cindlet, Database.GetMove("tackle"), false, 1f, 1f);
        Check(neutral > tackle, "same-element bonus applies");
        cindlet.ChangeStage(Stat.SpDefense, 2);
        Check(Battle.Damage(narlet, cindlet, bubble, false, 1f, 1f) < neutral, "defence stage reduces damage");
        Check(cindlet.ChangeStage(Stat.SpDefense, 10) == 4 && cindlet.Stage(Stat.SpDefense) == 6, "stage clamps at +6");
    }

    static SaveData SaveWith(params Creature[] party)
    {
        var s = SaveData.NewGame();
        s.Party.AddRange(party);
        return s;
    }

    static void BattleToVictory()
    {
        var save = SaveWith(Creature.Create("narlet", 20));
        var wild = Creature.Create("kilnpup", 2);
        var battle = new Battle(save, wild, new ScriptedRng(0.5f));
        Check(battle.Start().Count == 2, "start events");
        Check(save.SeenSpecies.Contains("kilnpup"), "marked seen");
        int xpBefore = save.Party[0].Xp;
        List<BattleEvent> ev = null;
        for (int i = 0; i < 10 && battle.Outcome == BattleOutcome.Ongoing; i++)
            ev = battle.ChooseMove(save.Party[0].Moves.FindIndex(m => m.Data.Power > 0));
        Check(battle.Outcome == BattleOutcome.Won, "strong lead wins");
        Check(save.Party[0].Xp > xpBefore, "xp awarded");
        Check(ev.Last().Kind == BattleEventKind.End, "ends with End");
        Check(ev.Any(e => e.Kind == BattleEventKind.Faint && !e.PlayerSide), "wild faint event");
        Check(battle.ChooseMove(0).Count == 0, "no actions after the end");
    }

    static void LosingSendsYouHome()
    {
        var save = SaveWith(Creature.Create("budbara", 2));
        var battle = new Battle(save, Creature.Create("kilnpup", 30), new SystemRng(7));
        for (int i = 0; i < 200 && battle.Outcome == BattleOutcome.Ongoing; i++) battle.ChooseMove(0);
        Check(battle.Outcome == BattleOutcome.Lost, "weak lead loses");
        Check(save.AllFainted, "all fainted");
    }

    static void FaintForcesSwitch()
    {
        var save = SaveWith(Creature.Create("budbara", 2), Creature.Create("narlet", 40));
        var battle = new Battle(save, Creature.Create("kilnpup", 25), new SystemRng(7));
        for (int i = 0; i < 200 && !battle.MustSwitch && battle.Outcome == BattleOutcome.Ongoing; i++) battle.ChooseMove(0);
        Check(battle.MustSwitch, "must switch after faint");
        Check(battle.ChooseMove(0).Count == 0, "can't attack while a switch is pending");
        var ev = battle.ChooseSwitch(1);
        Check(!battle.MustSwitch && battle.Player.Creature == save.Party[1], "switched in");
        Check(ev.Count == 1, "forced switch gives the wild creature no free turn");
    }

    static void Capture()
    {
        var weak = Creature.Create("capfrog", 3);
        weak.Hp = 1;
        var full = Creature.Create("capfrog", 3);
        var tin = Database.Lanterns["tin"];
        Check(CaptureMath.Probability(weak, tin) > CaptureMath.Probability(full, tin), "low hp is easier");
        Check(CaptureMath.Probability(full, Database.Lanterns["filigree"]) > CaptureMath.Probability(full, tin), "better lantern is easier");
        var legend = Creature.Create("veyrath", 50);
        Check(CaptureMath.Probability(legend, tin) < 0.2, "legendaries are hard");
        Check(CaptureMath.Attempt(legend, Database.Lanterns["star"], new ScriptedRng(0.99f)).caught, "star lantern always works");

        var save = SaveWith(Creature.Create("narlet", 5));
        var battle = new Battle(save, weak, new ScriptedRng(0.0f));
        var ev = battle.ChooseLantern("tin");
        Check(battle.Outcome == BattleOutcome.Captured, "captured with a low roll");
        Check(save.Party.Count == 2 && save.CaughtSpecies.Contains("capfrog"), "joins the party");
        Check(save.LanternCount("tin") == 9, "lantern used up");
        Check(ev.Count(e => e.Kind == BattleEventKind.CaptureShake) == 3, "three shakes");

        var save2 = SaveWith(Creature.Create("narlet", 5));
        var miss = new Battle(save2, Creature.Create("rimehare", 5), new ScriptedRng(0.999f));
        miss.ChooseLantern("tin");
        Check(miss.Outcome == BattleOutcome.Ongoing, "high roll breaks free");
        var none = new Battle(save2, Creature.Create("rimehare", 5), new ScriptedRng(0.5f)).ChooseLantern("star");
        Check(none.Count == 1 && none[0].Text.Contains("don't have"), "can't throw what you don't have");

        var fullParty = SaveWith(Enumerable.Range(0, 6).Select(_ => Creature.Create("geodig", 5)).ToArray());
        Check(!fullParty.AddCaught(Creature.Create("capfrog", 3)) && fullParty.Storage.Count == 1, "overflow goes to storage");
    }

    static void RunAway()
    {
        var save = SaveWith(Creature.Create("rimehare", 10));
        var fast = new Battle(save, Creature.Create("geodig", 3), new ScriptedRng(0.99f));
        fast.ChooseRun();
        Check(fast.Outcome == BattleOutcome.Ran, "faster creature always escapes");
        var slowSave = SaveWith(Creature.Create("geodig", 3));
        var slow = new Battle(slowSave, Creature.Create("rimehare", 10), new ScriptedRng(0.99f));
        slow.ChooseRun();
        Check(slow.Outcome != BattleOutcome.Ran, "slow creature can fail to escape");
    }

    static void Evolution()
    {
        var c = Creature.Create("cindlet", 15);
        Check(!c.CanEvolve, "not yet");
        c.GainXp(Creature.XpForLevel(16) - c.Xp);
        Check(c.CanEvolve, "can evolve at 16");
        c.Hp = c.MaxHp / 2;
        string old = c.Evolve();
        Check(old == "Cindlet" && c.SpeciesId == "scorchscale", "evolved");
        Check(Math.Abs((float)c.Hp / c.MaxHp - 0.5f) < 0.05f, "keeps hp share");
        Check(c.Knows("ember"), "keeps moves");
    }

    static void SaveHelpers()
    {
        var s = SaveData.NewGame();
        Check(!s.HasStarter && s.LanternCount("tin") == 10, "new game");
        s.AddLanterns("tin", 2);
        s.AddLanterns("brass", 1);
        Check(s.LanternCount("tin") == 12 && s.LanternCount("brass") == 1, "stacks");
        var c = Creature.Create("budbara", 5);
        c.Hp = 0;
        c.Moves[0].Pp = 0;
        s.Party.Add(c);
        s.HealParty();
        Check(c.Hp == c.MaxHp && c.Moves[0].Pp == c.Moves[0].Data.MaxPp, "heal restores hp and pp");
    }

    /// <summary>Hundreds of random battles must always finish without exceptions.</summary>
    static void FullRandomBattles()
    {
        var rng = new SystemRng(1234);
        var outcomes = new Dictionary<BattleOutcome, int>();
        for (int n = 0; n < 500; n++)
        {
            var save = SaveWith(Creature.Create(Database.Starters[n % 3], 5), Creature.Create("geodig", 4));
            var slot = Database.Roll(Database.WindmillMeadows, rng);
            var battle = new Battle(save, Creature.Create(slot.SpeciesId, rng.Range(slot.MinLevel, slot.MaxLevel + 1)), rng);
            battle.Start();
            int turns = 0;
            while (battle.Outcome == BattleOutcome.Ongoing && turns++ < 200)
            {
                if (battle.MustSwitch) { battle.ChooseSwitch(save.Party.FindIndex(c => !c.Fainted)); continue; }
                int action = rng.Range(0, 10);
                if (action < 7)
                {
                    var moves = battle.Player.Creature.Moves;
                    int pick = moves.FindIndex(m => m.Pp > 0);
                    if (pick < 0) { battle.ChooseRun(); continue; }
                    battle.ChooseMove(pick);
                }
                else if (action < 9) battle.ChooseLantern("tin");
                else battle.ChooseRun();
            }
            Check(battle.Outcome != BattleOutcome.Ongoing, $"battle {n} finished");
            outcomes[battle.Outcome] = outcomes.GetValueOrDefault(battle.Outcome) + 1;
        }
        Console.WriteLine("500 random battles: " + string.Join(", ", outcomes.Select(kv => $"{kv.Key} {kv.Value}")));
    }
}
