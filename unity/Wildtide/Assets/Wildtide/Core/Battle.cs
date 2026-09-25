using System;
using System.Collections.Generic;

namespace Wildtide.Core
{
    public enum BattleOutcome { Ongoing, Won, Lost, Ran, Captured }

    public enum BattleEventKind { Message, Damage, Faint, SwitchIn, CaptureShake, CaptureResult, StatChange, End }

    /// <summary>One step for the UI to play back: a line of text plus what to animate.</summary>
    public sealed class BattleEvent
    {
        public BattleEventKind Kind;
        public string Text;
        /// <summary>True when the event is about the player's side.</summary>
        public bool PlayerSide;
        public int HpAfter;
        public float Effectiveness = 1f;
        public int Shakes;
        public bool Caught;

        public static BattleEvent Say(string text) => new BattleEvent { Kind = BattleEventKind.Message, Text = text };
    }

    /// <summary>A creature in battle: the owned creature plus temporary stat stages.</summary>
    public sealed class Battler
    {
        public readonly Creature Creature;
        readonly int[] stages = new int[6];

        public Battler(Creature creature) { Creature = creature; }

        public int Stage(Stat stat) => stages[(int)stat];

        /// <summary>Returns the stages actually applied (clamped to -6..+6).</summary>
        public int ChangeStage(Stat stat, int delta)
        {
            int before = stages[(int)stat];
            stages[(int)stat] = Math.Max(-6, Math.Min(6, before + delta));
            return stages[(int)stat] - before;
        }

        public float Effective(Stat stat)
        {
            int s = Stage(stat);
            float mult = s >= 0 ? (2f + s) / 2f : 2f / (2f - s);
            return Creature.StatValue(stat) * mult;
        }
    }

    /// <summary>
    /// A single wild battle. Engine-free: the Unity layer calls a Choose* method, then plays back the events.
    /// </summary>
    public sealed class Battle
    {
        public const float CritChance = 1f / 16f;
        public const float CritMultiplier = 1.5f;
        public const float SameElementBonus = 1.5f;

        readonly SaveData save;
        readonly IRng rng;
        int runAttempts;

        public Battler Player { get; private set; }
        public Battler Wild { get; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
        /// <summary>Set when the player's creature fainted and another must be sent in before continuing.</summary>
        public bool MustSwitch { get; private set; }
        readonly HashSet<Creature> participants = new HashSet<Creature>();

        public Battle(SaveData save, Creature wild, IRng rng)
        {
            this.save = save;
            this.rng = rng;
            Wild = new Battler(wild);
            var lead = save.Lead ?? throw new InvalidOperationException("No creature able to battle");
            Player = new Battler(lead);
            participants.Add(lead);
            save.MarkSeen(wild.SpeciesId);
        }

        public List<BattleEvent> Start()
        {
            return new List<BattleEvent>
            {
                BattleEvent.Say($"A wild {Wild.Creature.Name} appeared!"),
                new BattleEvent { Kind = BattleEventKind.SwitchIn, PlayerSide = true, Text = $"Go, {Player.Creature.Name}!", HpAfter = Player.Creature.Hp },
            };
        }

        // ---- player choices -------------------------------------------------------------------

        public List<BattleEvent> ChooseMove(int slot)
        {
            var events = new List<BattleEvent>();
            if (!CanAct()) return events;
            var move = Player.Creature.Moves[slot];
            if (move.Pp <= 0)
            {
                events.Add(BattleEvent.Say($"{move.Data.Name} has no energy left!"));
                return events;
            }

            var wildMove = PickWildMove();
            bool playerFirst = GoesFirst(Player, Wild);
            if (playerFirst)
            {
                UseMove(Player, Wild, move, true, events);
                if (!Wild.Creature.Fainted && wildMove != null) UseMove(Wild, Player, wildMove, false, events);
            }
            else
            {
                if (wildMove != null) UseMove(Wild, Player, wildMove, false, events);
                if (!Player.Creature.Fainted) UseMove(Player, Wild, move, true, events);
            }
            Resolve(events);
            return events;
        }

        public List<BattleEvent> ChooseLantern(string lanternId)
        {
            var events = new List<BattleEvent>();
            if (!CanAct()) return events;
            if (!save.UseLantern(lanternId))
            {
                events.Add(BattleEvent.Say("You don't have any of those."));
                return events;
            }
            var lantern = Database.Lanterns[lanternId];
            events.Add(BattleEvent.Say($"You threw a {lantern.Name}!"));
            var (shakes, caught) = CaptureMath.Attempt(Wild.Creature, lantern, rng);
            for (int i = 0; i < shakes; i++) events.Add(new BattleEvent { Kind = BattleEventKind.CaptureShake, Shakes = i + 1, Text = "..." });
            if (caught)
            {
                events.Add(new BattleEvent { Kind = BattleEventKind.CaptureResult, Caught = true, Text = $"Gotcha! {Wild.Creature.Name} was bonded!" });
                bool toParty = save.AddCaught(Wild.Creature);
                if (!toParty) events.Add(BattleEvent.Say($"Your team is full, so {Wild.Creature.Name} went to storage."));
                Outcome = BattleOutcome.Captured;
                events.Add(new BattleEvent { Kind = BattleEventKind.End });
                return events;
            }
            events.Add(new BattleEvent { Kind = BattleEventKind.CaptureResult, Caught = false, Text = FailLine(shakes) });
            var wildMove = PickWildMove();
            if (wildMove != null) UseMove(Wild, Player, wildMove, false, events);
            Resolve(events);
            return events;
        }

        public List<BattleEvent> ChooseRun()
        {
            var events = new List<BattleEvent>();
            if (!CanAct()) return events;
            runAttempts++;
            float playerSpeed = Player.Effective(Stat.Speed), wildSpeed = Math.Max(1f, Wild.Effective(Stat.Speed));
            // Faster creatures always escape; slower ones get better odds each try.
            float chance = playerSpeed >= wildSpeed ? 1f : (playerSpeed * 128f / wildSpeed + 30f * runAttempts) / 256f;
            if (rng.Value() < chance)
            {
                events.Add(BattleEvent.Say("Got away safely!"));
                Outcome = BattleOutcome.Ran;
                events.Add(new BattleEvent { Kind = BattleEventKind.End });
                return events;
            }
            events.Add(BattleEvent.Say("Couldn't get away!"));
            var wildMove = PickWildMove();
            if (wildMove != null) UseMove(Wild, Player, wildMove, false, events);
            Resolve(events);
            return events;
        }

        /// <summary>Swap in another party member. Free after a faint; otherwise the wild creature gets a turn.</summary>
        public List<BattleEvent> ChooseSwitch(int partyIndex)
        {
            var events = new List<BattleEvent>();
            if (Outcome != BattleOutcome.Ongoing) return events;
            var next = save.Party[partyIndex];
            if (next.Fainted || next == Player.Creature)
            {
                events.Add(BattleEvent.Say(next.Fainted ? $"{next.Name} can't battle!" : $"{next.Name} is already out!"));
                return events;
            }
            bool forced = MustSwitch;
            if (!forced) events.Add(BattleEvent.Say($"Come back, {Player.Creature.Name}!"));
            Player = new Battler(next);
            participants.Add(next);
            MustSwitch = false;
            events.Add(new BattleEvent { Kind = BattleEventKind.SwitchIn, PlayerSide = true, Text = $"Go, {next.Name}!", HpAfter = next.Hp });
            if (!forced)
            {
                var wildMove = PickWildMove();
                if (wildMove != null) UseMove(Wild, Player, wildMove, false, events);
                Resolve(events);
            }
            return events;
        }

        // ---- rules ----------------------------------------------------------------------------

        bool CanAct() => Outcome == BattleOutcome.Ongoing && !MustSwitch;

        bool GoesFirst(Battler a, Battler b)
        {
            float sa = a.Effective(Stat.Speed), sb = b.Effective(Stat.Speed);
            if (Math.Abs(sa - sb) < 0.01f) return rng.Value() < 0.5f;
            return sa > sb;
        }

        MoveSlot PickWildMove()
        {
            var usable = Wild.Creature.Moves.FindAll(m => m.Pp > 0);
            return usable.Count == 0 ? null : usable[rng.Range(0, usable.Count)];
        }

        void UseMove(Battler user, Battler target, MoveSlot slot, bool playerIsUser, List<BattleEvent> events)
        {
            var move = slot.Data;
            slot.Pp--;
            string who = playerIsUser ? user.Creature.Name : $"The wild {user.Creature.Name}";
            events.Add(BattleEvent.Say($"{who} used {move.Name}!"));

            if (move.Accuracy > 0 && rng.Range(0, 100) >= move.Accuracy)
            {
                events.Add(BattleEvent.Say("But it missed!"));
                return;
            }

            if (move.Category == MoveCategory.Status)
            {
                var affected = move.TargetsSelf ? user : target;
                bool affectedIsPlayer = move.TargetsSelf ? playerIsUser : !playerIsUser;
                int applied = affected.ChangeStage(move.StatChanged, move.StatStages);
                string stat = StatName(move.StatChanged);
                string text = applied == 0
                    ? $"{affected.Creature.Name}'s {stat} won't go any {(move.StatStages > 0 ? "higher" : "lower")}!"
                    : $"{affected.Creature.Name}'s {stat} {(applied > 0 ? "rose" : "fell")}{(Math.Abs(applied) > 1 ? " sharply" : "")}!";
                events.Add(new BattleEvent { Kind = BattleEventKind.StatChange, PlayerSide = affectedIsPlayer, Text = text });
                return;
            }

            bool crit = rng.Value() < CritChance;
            float effectiveness = TypeChart.Multiplier(move.Element, target.Creature.Species.Element, target.Creature.Species.SecondaryElement);
            int damage = Damage(user, target, move, crit, effectiveness, 0.85f + 0.15f * rng.Value());
            target.Creature.Hp = Math.Max(0, target.Creature.Hp - damage);
            events.Add(new BattleEvent
            {
                Kind = BattleEventKind.Damage, PlayerSide = !playerIsUser, HpAfter = target.Creature.Hp, Effectiveness = effectiveness,
            });
            if (crit) events.Add(BattleEvent.Say("A critical hit!"));
            if (effectiveness > 1f) events.Add(BattleEvent.Say("It's super effective!"));
            else if (effectiveness < 1f) events.Add(BattleEvent.Say("It's not very effective..."));

            if (target.Creature.Fainted)
            {
                string name = playerIsUser ? $"The wild {target.Creature.Name}" : target.Creature.Name;
                events.Add(new BattleEvent { Kind = BattleEventKind.Faint, PlayerSide = !playerIsUser, Text = $"{name} fainted!" });
            }
        }

        /// <summary>Classic formula: level, power and attack/defence, then element, crit and random spread.</summary>
        public static int Damage(Battler user, Battler target, MoveData move, bool crit, float effectiveness, float spread)
        {
            bool physical = move.Category == MoveCategory.Physical;
            float atk = user.Effective(physical ? Stat.Attack : Stat.SpAttack);
            float def = Math.Max(1f, target.Effective(physical ? Stat.Defense : Stat.SpDefense));
            float baseDamage = ((2f * user.Creature.Level / 5f + 2f) * move.Power * atk / def) / 50f + 2f;
            var species = user.Creature.Species;
            if (move.Element == species.Element || move.Element == species.SecondaryElement) baseDamage *= SameElementBonus;
            if (crit) baseDamage *= CritMultiplier;
            baseDamage *= effectiveness * spread;
            return Math.Max(1, (int)baseDamage);
        }

        void Resolve(List<BattleEvent> events)
        {
            if (Wild.Creature.Fainted)
            {
                AwardXp(events);
                Outcome = BattleOutcome.Won;
                events.Add(new BattleEvent { Kind = BattleEventKind.End });
                return;
            }
            if (Player.Creature.Fainted)
            {
                if (save.AllFainted)
                {
                    events.Add(BattleEvent.Say("You have no creatures left who can battle..."));
                    events.Add(BattleEvent.Say("You hurried back to the Hearth House."));
                    Outcome = BattleOutcome.Lost;
                    events.Add(new BattleEvent { Kind = BattleEventKind.End });
                }
                else MustSwitch = true;
            }
        }

        void AwardXp(List<BattleEvent> events)
        {
            var winners = new List<Creature>();
            foreach (var c in participants) if (!c.Fainted) winners.Add(c);
            if (winners.Count == 0) return;
            int total = Wild.Creature.Species.BaseXp * Wild.Creature.Level / 5 + 1;
            int each = Math.Max(1, total / winners.Count);
            foreach (var c in winners)
                foreach (var line in c.GainXp(each)) events.Add(BattleEvent.Say(line));
        }

        static string FailLine(int shakes)
        {
            switch (shakes)
            {
                case 0: return "Oh no! It broke free right away!";
                case 1: return "Argh! It almost looked settled!";
                case 2: return "Aww! It seemed close!";
                default: return "Shoot! It was so close too!";
            }
        }

        static string StatName(Stat stat)
        {
            switch (stat)
            {
                case Stat.Attack: return "attack";
                case Stat.Defense: return "defence";
                case Stat.SpAttack: return "element power";
                case Stat.SpDefense: return "element guard";
                case Stat.Speed: return "speed";
                default: return "health";
            }
        }
    }

    public static class CaptureMath
    {
        /// <summary>
        /// Lower HP, a higher catch rate and a better lantern all help. Returns how many times the lantern
        /// wobbles (0-3) and whether it clicked shut.
        /// </summary>
        public static (int shakes, bool caught) Attempt(Creature wild, LanternData lantern, IRng rng)
        {
            if (lantern.AlwaysCatches) return (3, true);
            float a = CatchValue(wild, lantern);
            if (a >= 255f) return (3, true);
            // Four independent checks; passing all four is a capture.
            double b = 65536.0 / Math.Pow(255.0 / a, 0.1875);
            int passed = 0;
            for (int i = 0; i < 4; i++)
            {
                if (rng.Range(0, 65536) < b) passed++;
                else break;
            }
            return passed == 4 ? (3, true) : (Math.Min(passed, 3), false);
        }

        public static float CatchValue(Creature wild, LanternData lantern)
        {
            float max = wild.MaxHp, hp = Math.Max(1, wild.Hp);
            return (3f * max - 2f * hp) * wild.Species.CatchRate * lantern.CatchBonus / (3f * max);
        }

        /// <summary>Exact probability of a capture, for tuning and tests.</summary>
        public static double Probability(Creature wild, LanternData lantern)
        {
            if (lantern.AlwaysCatches) return 1.0;
            float a = CatchValue(wild, lantern);
            if (a >= 255f) return 1.0;
            double b = 65536.0 / Math.Pow(255.0 / a, 0.1875);
            return Math.Pow(b / 65536.0, 4);
        }
    }
}
