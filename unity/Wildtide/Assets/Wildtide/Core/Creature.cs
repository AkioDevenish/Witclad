using System;
using System.Collections.Generic;

namespace Wildtide.Core
{
    [Serializable]
    public sealed class MoveSlot
    {
        public string MoveId;
        public int Pp;

        public MoveData Data => Database.GetMove(MoveId);
    }

    /// <summary>
    /// One creature the player (or the wild) owns. Public fields so Unity's JsonUtility can save it as-is.
    /// </summary>
    [Serializable]
    public sealed class Creature
    {
        public const int MaxLevel = 100;
        public const int MaxMoves = 4;

        public string SpeciesId;
        public string Nickname;
        public int Level;
        public int Xp;
        public int Hp;
        public List<MoveSlot> Moves = new List<MoveSlot>();

        public Species Species => Database.GetSpecies(SpeciesId);
        public string Name => string.IsNullOrEmpty(Nickname) ? Species.Name : Nickname;
        public bool Fainted => Hp <= 0;
        public int MaxHp => StatValue(Stat.Hp);

        public static Creature Create(string speciesId, int level)
        {
            var c = new Creature { SpeciesId = speciesId, Level = Math.Max(1, Math.Min(MaxLevel, level)) };
            c.Xp = XpForLevel(c.Level);
            // Know the latest four moves learnable at this level.
            foreach (var entry in c.Species.Learnset)
                if (entry.Level <= c.Level) c.LearnOrReplaceOldest(entry.MoveId);
            c.Hp = c.MaxHp;
            return c;
        }

        /// <summary>Cubic growth curve: reaching level n takes n^3 total XP.</summary>
        public static int XpForLevel(int level) => level <= 1 ? 0 : level * level * level;

        public int StatValue(Stat stat)
        {
            int b = Species.Base.Get(stat);
            if (stat == Stat.Hp) return 2 * b * Level / 100 + Level + 10;
            return 2 * b * Level / 100 + 5;
        }

        public void HealFully()
        {
            Hp = MaxHp;
            foreach (var m in Moves) m.Pp = m.Data.MaxPp;
        }

        public bool Knows(string moveId) => Moves.Exists(m => m.MoveId == moveId);

        /// <summary>Returns true if the move was added (false if it was already known).</summary>
        public bool LearnOrReplaceOldest(string moveId)
        {
            if (Knows(moveId)) return false;
            if (Moves.Count >= MaxMoves) Moves.RemoveAt(0);
            Moves.Add(new MoveSlot { MoveId = moveId, Pp = Database.GetMove(moveId).MaxPp });
            return true;
        }

        /// <summary>Adds XP, levelling up as many times as it earns. Reports each change in order.</summary>
        public List<string> GainXp(int amount)
        {
            var log = new List<string>();
            if (Level >= MaxLevel || amount <= 0) return log;
            Xp += amount;
            log.Add($"{Name} gained {amount} XP.");
            while (Level < MaxLevel && Xp >= XpForLevel(Level + 1))
            {
                int oldMax = MaxHp;
                Level++;
                Hp = Math.Min(MaxHp, Hp + (MaxHp - oldMax));
                log.Add($"{Name} grew to level {Level}!");
                foreach (var entry in Species.Learnset)
                    if (entry.Level == Level && LearnOrReplaceOldest(entry.MoveId))
                        log.Add($"{Name} learned {Database.GetMove(entry.MoveId).Name}!");
            }
            return log;
        }

        public bool CanEvolve => Species.EvolvesTo != null && Level >= Species.EvolveLevel;

        /// <summary>Evolves in place, keeping the same share of HP. Returns the old species name.</summary>
        public string Evolve()
        {
            if (!CanEvolve) throw new InvalidOperationException($"{Name} can't evolve yet");
            string oldName = Species.Name;
            float hpShare = MaxHp > 0 ? (float)Hp / MaxHp : 1f;
            SpeciesId = Species.EvolvesTo;
            Hp = Math.Max(1, (int)Math.Round(MaxHp * hpShare));
            foreach (var entry in Species.Learnset)
                if (entry.Level <= Level && entry.Level == 1) LearnOrReplaceOldest(entry.MoveId);
            return oldName;
        }
    }

    [Serializable]
    public sealed class ItemStack
    {
        public string ItemId;
        public int Count;
    }

    /// <summary>Everything that gets written to disk.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int PartyLimit = 6;

        public int Version = 1;
        public string PlayerName = "Player";
        public List<Creature> Party = new List<Creature>();
        public List<Creature> Storage = new List<Creature>();
        public List<ItemStack> Lanterns = new List<ItemStack>();
        public List<string> SeenSpecies = new List<string>();
        public List<string> CaughtSpecies = new List<string>();
        public int Coins = 500;
        public float PosX, PosY, PosZ;
        public bool HasPosition;

        public bool HasStarter => Party.Count > 0 || Storage.Count > 0;

        public Creature Lead => Party.Find(c => !c.Fainted);

        public bool AllFainted => Party.TrueForAll(c => c.Fainted);

        public static SaveData NewGame()
        {
            var save = new SaveData();
            save.AddLanterns("tin", 10);
            return save;
        }

        public int LanternCount(string id)
        {
            var stack = Lanterns.Find(s => s.ItemId == id);
            return stack?.Count ?? 0;
        }

        public void AddLanterns(string id, int count)
        {
            var stack = Lanterns.Find(s => s.ItemId == id);
            if (stack == null) Lanterns.Add(new ItemStack { ItemId = id, Count = count });
            else stack.Count += count;
        }

        public bool UseLantern(string id)
        {
            var stack = Lanterns.Find(s => s.ItemId == id);
            if (stack == null || stack.Count <= 0) return false;
            stack.Count--;
            return true;
        }

        public void MarkSeen(string speciesId)
        {
            if (!SeenSpecies.Contains(speciesId)) SeenSpecies.Add(speciesId);
        }

        /// <summary>Adds to the party, or to storage when the party is full. Returns true if it went to the party.</summary>
        public bool AddCaught(Creature c)
        {
            MarkSeen(c.SpeciesId);
            if (!CaughtSpecies.Contains(c.SpeciesId)) CaughtSpecies.Add(c.SpeciesId);
            if (Party.Count < PartyLimit) { Party.Add(c); return true; }
            Storage.Add(c);
            return false;
        }

        public void HealParty()
        {
            foreach (var c in Party) c.HealFully();
        }
    }
}
