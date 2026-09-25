namespace Wildtide.Core
{
    public enum Element
    {
        Flame, Tide, Verdant, Storm, Stone, Frost, Gale, Venom, Spirit, Iron, Lumen, Umbra
    }

    /// <summary>Attack effectiveness. No immunities: the weakest matchup is 0.5x.</summary>
    public static class TypeChart
    {
        public const float Strong = 2f;
        public const float Weak = 0.5f;

        // Row = attacking element, listing what it is strong and weak against.
        static readonly Element[][] StrongAgainst =
        {
            /* Flame   */ new[] { Element.Verdant, Element.Frost, Element.Iron },
            /* Tide    */ new[] { Element.Flame, Element.Stone },
            /* Verdant */ new[] { Element.Tide, Element.Stone },
            /* Storm   */ new[] { Element.Tide, Element.Gale },
            /* Stone   */ new[] { Element.Flame, Element.Storm, Element.Frost, Element.Gale },
            /* Frost   */ new[] { Element.Verdant, Element.Gale },
            /* Gale    */ new[] { Element.Verdant, Element.Venom },
            /* Venom   */ new[] { Element.Verdant, Element.Lumen },
            /* Spirit  */ new[] { Element.Spirit, Element.Venom },
            /* Iron    */ new[] { Element.Frost, Element.Stone, Element.Lumen },
            /* Lumen   */ new[] { Element.Umbra, Element.Spirit },
            /* Umbra   */ new[] { Element.Spirit, Element.Lumen },
        };

        static readonly Element[][] WeakAgainst =
        {
            /* Flame   */ new[] { Element.Flame, Element.Tide, Element.Stone },
            /* Tide    */ new[] { Element.Tide, Element.Verdant },
            /* Verdant */ new[] { Element.Flame, Element.Verdant, Element.Venom, Element.Gale, Element.Iron },
            /* Storm   */ new[] { Element.Storm, Element.Verdant, Element.Stone },
            /* Stone   */ new[] { Element.Verdant, Element.Iron },
            /* Frost   */ new[] { Element.Flame, Element.Tide, Element.Frost, Element.Iron },
            /* Gale    */ new[] { Element.Stone, Element.Storm, Element.Iron },
            /* Venom   */ new[] { Element.Venom, Element.Stone, Element.Spirit, Element.Iron },
            /* Spirit  */ new[] { Element.Umbra },
            /* Iron    */ new[] { Element.Flame, Element.Tide, Element.Storm, Element.Iron },
            /* Lumen   */ new[] { Element.Lumen, Element.Iron },
            /* Umbra   */ new[] { Element.Umbra },
        };

        public static float Multiplier(Element attack, Element defend)
        {
            if (System.Array.IndexOf(StrongAgainst[(int)attack], defend) >= 0) return Strong;
            if (System.Array.IndexOf(WeakAgainst[(int)attack], defend) >= 0) return Weak;
            return 1f;
        }

        public static float Multiplier(Element attack, Element primary, Element? secondary)
        {
            float m = Multiplier(attack, primary);
            if (secondary.HasValue && secondary.Value != primary) m *= Multiplier(attack, secondary.Value);
            return m;
        }
    }
}
