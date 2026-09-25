using System.Collections.Generic;

namespace Wildtide.Core
{
    public enum Stat { Hp, Attack, Defense, SpAttack, SpDefense, Speed }

    public enum MoveCategory { Physical, Special, Status }

    public sealed class MoveData
    {
        public string Id;
        public string Name;
        public Element Element;
        public MoveCategory Category;
        public int Power;
        /// <summary>0-100. 0 means the move never misses.</summary>
        public int Accuracy;
        public int MaxPp;
        /// <summary>Status moves: which stat changes, by how many stages, and on whom.</summary>
        public Stat StatChanged;
        public int StatStages;
        public bool TargetsSelf;
    }

    public struct BaseStats
    {
        public int Hp, Attack, Defense, SpAttack, SpDefense, Speed;

        public BaseStats(int hp, int atk, int def, int spa, int spd, int spe)
        {
            Hp = hp; Attack = atk; Defense = def; SpAttack = spa; SpDefense = spd; Speed = spe;
        }

        public int Get(Stat stat)
        {
            switch (stat)
            {
                case Stat.Hp: return Hp;
                case Stat.Attack: return Attack;
                case Stat.Defense: return Defense;
                case Stat.SpAttack: return SpAttack;
                case Stat.SpDefense: return SpDefense;
                default: return Speed;
            }
        }
    }

    public sealed class LearnsetEntry
    {
        public int Level;
        public string MoveId;
        public LearnsetEntry(int level, string moveId) { Level = level; MoveId = moveId; }
    }

    public sealed class Species
    {
        public string Id;
        /// <summary>The prompt id in prompts/prompts.json; art in Resources is looked up by this.</summary>
        public string ArtId;
        public string Name;
        public Element Element;
        public Element? SecondaryElement;
        public BaseStats Base;
        /// <summary>1-255; higher is easier to capture.</summary>
        public int CatchRate;
        /// <summary>XP yield multiplier when this species is defeated.</summary>
        public int BaseXp;
        public string EvolvesTo;
        public int EvolveLevel;
        public List<LearnsetEntry> Learnset = new List<LearnsetEntry>();
        /// <summary>Placeholder tint until concept art or a mesh is imported.</summary>
        public uint PlaceholderColor;
        public float PlaceholderScale = 1f;
        public string Description;
    }

    public sealed class LanternData
    {
        public string Id;
        public string Name;
        public float CatchBonus;
        /// <summary>The star lantern never fails.</summary>
        public bool AlwaysCatches;
        public int Price;
    }
}
