using System;
using System.Collections.Generic;

namespace Wildtide.Core
{
    /// <summary>Best result on one island. Public fields so Unity's JsonUtility can save it.</summary>
    [Serializable]
    public sealed class LevelRecord
    {
        public string Id;
        public bool Cleared;
        public int BestPearls;
        /// <summary>Seconds; 0 until the level is cleared.</summary>
        public float BestTime;
    }

    [Serializable]
    public sealed class Progress
    {
        public List<LevelRecord> Records = new List<LevelRecord>();
        /// <summary>Which hero you play as; one of <see cref="Heroes"/>.</summary>
        public string Hero = Heroes[0];

        /// <summary>Playable heroes. Each has a model at Resources/Characters/&lt;name&gt; (made by tools/blender/heroes.py).</summary>
        public static readonly string[] Heroes = { "Lyra", "Gareth" };

        public static string HeroTitle(string hero) => hero == "Gareth" ? "Sir Gareth" : hero;

        /// <summary>Switches to the next hero, wrapping round. Unknown names start again from the first.</summary>
        public string NextHero()
        {
            int i = Array.IndexOf(Heroes, Hero);
            Hero = Heroes[(i + 1) % Heroes.Length];
            return Hero;
        }

        public LevelRecord Get(string id)
        {
            foreach (var r in Records)
                if (r != null && r.Id == id) return r;
            return null;
        }

        /// <summary>The first island is always open; each later one opens when the one before it is cleared.</summary>
        public bool IsUnlocked(int index)
        {
            if (index <= 0) return true;
            if (index >= Levels.All.Count) return false;
            var previous = Get(Levels.All[index - 1].Id);
            return previous != null && previous.Cleared;
        }

        /// <summary>Records a clear, keeping the best pearl count and the fastest time. Returns true for a new best time.</summary>
        public bool RecordClear(string id, int pearls, float seconds)
        {
            var r = Get(id);
            if (r == null)
            {
                r = new LevelRecord { Id = id };
                Records.Add(r);
            }
            bool fastest = !r.Cleared || seconds < r.BestTime;
            r.Cleared = true;
            if (pearls > r.BestPearls) r.BestPearls = pearls;
            if (fastest) r.BestTime = seconds;
            return fastest;
        }

        public int TotalPearls()
        {
            int total = 0;
            foreach (var r in Records)
                if (r != null) total += r.BestPearls;
            return total;
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int whole = (int)seconds;
            return $"{whole / 60}:{whole % 60:00}.{(int)((seconds - whole) * 10f)}";
        }
    }
}
