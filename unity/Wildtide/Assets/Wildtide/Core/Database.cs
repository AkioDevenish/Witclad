using System.Collections.Generic;

namespace Wildtide.Core
{
    /// <summary>
    /// Vertical-slice content: three starter lines, six route creatures and one legendary.
    /// Names and designs come from the Wildtide prompt library; numbers are first-pass balance.
    /// </summary>
    public static class Database
    {
        public static readonly Dictionary<string, MoveData> Moves = new Dictionary<string, MoveData>();
        public static readonly Dictionary<string, Species> Species = new Dictionary<string, Species>();
        public static readonly Dictionary<string, LanternData> Lanterns = new Dictionary<string, LanternData>();

        public static readonly string[] Starters = { "cindlet", "narlet", "budbara" };

        /// <summary>Wild encounters in the Windmill Meadows tall grass: species id, weight, min and max level.</summary>
        public static readonly EncounterSlot[] WindmillMeadows =
        {
            new EncounterSlot("capfrog", 30, 2, 4),
            new EncounterSlot("kilnpup", 20, 2, 4),
            new EncounterSlot("glasscrab", 20, 2, 4),
            new EncounterSlot("stratojel", 12, 3, 5),
            new EncounterSlot("geodig", 12, 3, 5),
            new EncounterSlot("rimehare", 6, 4, 6),
        };

        static Database()
        {
            // --- Moves ---------------------------------------------------------------------------
            Move("tackle", "Tackle", Element.Stone, MoveCategory.Physical, 40, 100, 35);
            Move("leer", "Stare Down", Element.Umbra, MoveCategory.Status, 0, 100, 30, Stat.Defense, -1, false);
            Move("growl", "Growl", Element.Spirit, MoveCategory.Status, 0, 100, 40, Stat.Attack, -1, false);
            Move("harden", "Curl Up", Element.Iron, MoveCategory.Status, 0, 0, 30, Stat.Defense, 1, true);

            Move("ember", "Ember", Element.Flame, MoveCategory.Special, 40, 100, 25);
            Move("scale-flare", "Scale Flare", Element.Flame, MoveCategory.Physical, 65, 95, 20);
            Move("magma-crown", "Magma Crown", Element.Flame, MoveCategory.Special, 90, 100, 10);
            Move("kiln-bark", "Kiln Bark", Element.Flame, MoveCategory.Special, 50, 100, 20);

            Move("bubble", "Bubble", Element.Tide, MoveCategory.Special, 40, 100, 30);
            Move("tusk-surge", "Tusk Surge", Element.Tide, MoveCategory.Physical, 65, 95, 20);
            Move("breaking-wave", "Breaking Wave", Element.Tide, MoveCategory.Special, 90, 100, 10);
            Move("glass-pinch", "Glass Pinch", Element.Tide, MoveCategory.Physical, 50, 100, 25);

            Move("leaf-flick", "Leaf Flick", Element.Verdant, MoveCategory.Special, 40, 100, 25);
            Move("vine-lash", "Vine Lash", Element.Verdant, MoveCategory.Physical, 65, 95, 20);
            Move("bloom-burst", "Bloom Burst", Element.Verdant, MoveCategory.Special, 90, 100, 10);
            Move("spore-puff", "Spore Puff", Element.Verdant, MoveCategory.Status, 0, 90, 20, Stat.Speed, -1, false);

            Move("static-sting", "Static Sting", Element.Storm, MoveCategory.Special, 45, 100, 25);
            Move("geode-claw", "Geode Claw", Element.Stone, MoveCategory.Physical, 55, 95, 20);
            Move("icicle-ear", "Icicle Ear", Element.Frost, MoveCategory.Physical, 50, 100, 25);
            Move("gust", "Gust", Element.Gale, MoveCategory.Special, 40, 100, 30);
            Move("abyss-song", "Abyss Song", Element.Tide, MoveCategory.Special, 110, 85, 5);
            Move("rune-glow", "Rune Glow", Element.Lumen, MoveCategory.Special, 80, 100, 10);

            // --- Species -------------------------------------------------------------------------
            // Flame line: ember pangolin
            Add("cindlet", "cindlet-stage-1", "Cindlet", Element.Flame, null, new BaseStats(44, 52, 48, 60, 50, 61), 45, 62,
                "scorchscale", 16, 0xE8743BFF, 0.8f, "A pudgy baby pangolin. Its ember seams glow brighter when it's brave.",
                L(1, "tackle"), L(1, "growl"), L(4, "ember"), L(9, "harden"), L(13, "scale-flare"));
            Add("scorchscale", "scorchscale-stage-2", "Scorchscale", Element.Flame, null, new BaseStats(58, 68, 60, 78, 62, 78), 45, 142,
                "pyrangol", 36, 0xF08A2EFF, 1.1f, "Its scales flare open like vents, shedding sparks when it grins.",
                L(1, "tackle"), L(1, "ember"), L(20, "kiln-bark"), L(28, "scale-flare"));
            Add("pyrangol", "pyrangol-stage-3", "Pyrangol", Element.Flame, Element.Iron, new BaseStats(78, 94, 88, 100, 80, 90), 45, 239,
                null, 0, 0xFF6A1FFF, 1.5f, "Forged scales, a crown of fire. It stands between danger and the ones it loves.",
                L(1, "scale-flare"), L(36, "magma-crown"));

            // Tide line: narwhal
            Add("narlet", "narlet-stage-1", "Narlet", Element.Tide, null, new BaseStats(50, 48, 55, 58, 56, 50), 45, 63,
                "tuskwave", 16, 0x2E6FD6FF, 0.8f, "Travels on land inside a wobbling bubble. Waves its flippers like hands.",
                L(1, "tackle"), L(1, "growl"), L(4, "bubble"), L(9, "harden"), L(13, "tusk-surge"));
            Add("tuskwave", "tuskwave-stage-2", "Tuskwave", Element.Tide, null, new BaseStats(65, 64, 70, 76, 72, 66), 45, 142,
                "narvalor", 36, 0x33A6E0FF, 1.1f, "Surfs through the air on a ribbon of water, freckles glowing cyan.",
                L(1, "bubble"), L(1, "tusk-surge"), L(22, "glass-pinch"));
            Add("narvalor", "narvalor-stage-3", "Narvalor", Element.Tide, Element.Lumen, new BaseStats(90, 84, 92, 100, 90, 74), 45, 239,
                null, 0, 0x1C3F8FFF, 1.6f, "A knight of the open sea. Its tusk of polished coral never breaks.",
                L(1, "tusk-surge"), L(36, "breaking-wave"), L(40, "rune-glow"));

            // Verdant line: moss capybara
            Add("budbara", "budbara-stage-1", "Budbara", Element.Verdant, null, new BaseStats(58, 50, 60, 48, 58, 40), 45, 64,
                "grovebara", 16, 0x6BB04AFF, 0.85f, "Unbothered and sweet. Nothing on the island has ever made it hurry.",
                L(1, "tackle"), L(1, "growl"), L(4, "leaf-flick"), L(9, "harden"), L(13, "vine-lash"));
            Add("grovebara", "grovebara-stage-2", "Grovebara", Element.Verdant, null, new BaseStats(76, 66, 78, 60, 74, 52), 45, 142,
                "elderbara", 36, 0x4E9A3AFF, 1.15f, "A tiny bird lives on its head. Neither remembers who moved in first.",
                L(1, "leaf-flick"), L(1, "vine-lash"), L(20, "spore-puff"));
            Add("elderbara", "elderbara-stage-3", "Elderbara", Element.Verdant, Element.Spirit, new BaseStats(104, 84, 100, 76, 96, 62), 45, 239,
                null, 0, 0x2F6E2BFF, 1.6f, "A walking sanctuary. Fireflies drift through its mane of flowering moss.",
                L(1, "vine-lash"), L(36, "bloom-burst"));

            // Route creatures (evolutions still to be designed)
            Add("kilnpup", "kilnpup", "Kilnpup", Element.Flame, null, new BaseStats(48, 56, 50, 44, 42, 58), 190, 55,
                null, 0, 0xC96A3DFF, 0.7f, "A glazed clay puppy with a kiln fire glowing through its vents.",
                L(1, "tackle"), L(3, "leer"), L(6, "kiln-bark"));
            Add("glasscrab", "glasscrab", "Glasscrab", Element.Tide, Element.Stone, new BaseStats(44, 60, 70, 36, 44, 38), 190, 57,
                null, 0, 0x8FD1B8FF, 0.65f, "Lives in a bottle of frosted sea glass. Waves its big claw at strangers.",
                L(1, "harden"), L(2, "glass-pinch"), L(7, "bubble"));
            Add("capfrog", "capfrog", "Capfrog", Element.Verdant, Element.Venom, new BaseStats(52, 40, 48, 52, 50, 34), 220, 50,
                null, 0, 0xC8413AFF, 0.65f, "Puffs spores when it croaks. Its grin never quite wakes up.",
                L(1, "growl"), L(2, "leaf-flick"), L(5, "spore-puff"));
            Add("stratojel", "stratojel", "Stratojel", Element.Storm, Element.Gale, new BaseStats(42, 34, 38, 66, 52, 62), 170, 60,
                null, 0, 0xB9C2CFFF, 0.75f, "A thundercloud jellyfish. Its eyes only show when it's charging.",
                L(1, "gust"), L(4, "static-sting"));
            Add("geodig", "geodig", "Geodig", Element.Stone, null, new BaseStats(56, 64, 72, 30, 42, 30), 170, 60,
                null, 0, 0x8A6C8FFF, 0.7f, "Its claws are cracked-open geodes. It digs toward anything that glitters.",
                L(1, "tackle"), L(3, "harden"), L(6, "geode-claw"));
            Add("rimehare", "rimehare", "Rimehare", Element.Frost, null, new BaseStats(46, 58, 42, 50, 48, 74), 120, 64,
                null, 0, 0xDDEEFFFF, 0.7f, "Its breath mists even in summer. Icicle ear-tips chime when it runs.",
                L(1, "tackle"), L(3, "growl"), L(5, "icicle-ear"));

            // Legendary
            Add("veyrath", "veyrath-guardian-of-the-sea", "Veyrath", Element.Tide, Element.Spirit, new BaseStats(120, 100, 110, 130, 120, 90), 3, 306,
                null, 0, 0x1E5A7AFF, 3f, "Guardian of the archipelago. It carries ruins and a forest on its back.",
                L(1, "breaking-wave"), L(1, "rune-glow"), L(50, "abyss-song"));

            // --- Bond Lanterns (the five tiers from the prop sheet) --------------------------------
            Lantern("tin", "Tin Lantern", 1f, 200);
            Lantern("brass", "Brass Lantern", 1.5f, 600);
            Lantern("seaglass", "Sea-glass Lantern", 2f, 1200);
            Lantern("filigree", "Filigree Lantern", 2.5f, 2400);
            Lantern("star", "Star Lantern", 255f, 0, alwaysCatches: true);
        }

        static LearnsetEntry L(int level, string move) => new LearnsetEntry(level, move);

        static void Move(string id, string name, Element element, MoveCategory category, int power, int accuracy, int pp,
            Stat stat = Stat.Attack, int stages = 0, bool self = false)
        {
            Moves[id] = new MoveData
            {
                Id = id, Name = name, Element = element, Category = category, Power = power, Accuracy = accuracy,
                MaxPp = pp, StatChanged = stat, StatStages = stages, TargetsSelf = self,
            };
        }

        static void Add(string id, string artId, string name, Element element, Element? secondary, BaseStats stats, int catchRate,
            int baseXp, string evolvesTo, int evolveLevel, uint color, float scale, string description, params LearnsetEntry[] learnset)
        {
            Species[id] = new Species
            {
                Id = id, ArtId = artId, Name = name, Element = element, SecondaryElement = secondary, Base = stats,
                CatchRate = catchRate, BaseXp = baseXp, EvolvesTo = evolvesTo, EvolveLevel = evolveLevel,
                PlaceholderColor = color, PlaceholderScale = scale, Description = description,
                Learnset = new List<LearnsetEntry>(learnset),
            };
        }

        static void Lantern(string id, string name, float bonus, int price, bool alwaysCatches = false)
        {
            Lanterns[id] = new LanternData { Id = id, Name = name, CatchBonus = bonus, Price = price, AlwaysCatches = alwaysCatches };
        }

        public static Species GetSpecies(string id)
        {
            if (!Species.TryGetValue(id, out var s)) throw new KeyNotFoundException($"Unknown species '{id}'");
            return s;
        }

        public static MoveData GetMove(string id)
        {
            if (!Moves.TryGetValue(id, out var m)) throw new KeyNotFoundException($"Unknown move '{id}'");
            return m;
        }

        public static EncounterSlot Roll(EncounterSlot[] table, IRng rng)
        {
            int total = 0;
            foreach (var slot in table) total += slot.Weight;
            int pick = rng.Range(0, total);
            foreach (var slot in table)
            {
                if (pick < slot.Weight) return slot;
                pick -= slot.Weight;
            }
            return table[table.Length - 1];
        }
    }

    public readonly struct EncounterSlot
    {
        public readonly string SpeciesId;
        public readonly int Weight, MinLevel, MaxLevel;

        public EncounterSlot(string speciesId, int weight, int minLevel, int maxLevel)
        {
            SpeciesId = speciesId; Weight = weight; MinLevel = minLevel; MaxLevel = maxLevel;
        }
    }
}
