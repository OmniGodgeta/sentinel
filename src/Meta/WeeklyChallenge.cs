using System.Collections.Generic;
using System.Globalization;

namespace Sentinel.Meta;

/// <summary>
/// The Weekly Challenge: one fixed endless run that everyone on the same build
/// shares for the ISO week, with a rotating twist. Deterministic — the week
/// number picks the seed and the mutator. No leaderboard, no gate; it just pays
/// out like any other run and tracks your own deepest wave for the week.
/// </summary>
public static class WeeklyChallenge
{
    public readonly record struct Week(
        int Year, int Number, ulong Seed,
        string MutatorName, string MutatorBlurb,
        Config.AscensionTierDef Twist)
    {
        public string Id => $"{Year}-W{Number:00}";
        public string Title => $"Week {Number} · {MutatorName}";
    }

    private sealed record Mutator(string Name, string Blurb, Config.AscensionTierDef Twist);

    // Each twist is an ascension-shaped modifier: enemy buffs + a player perk +
    // a reward multiplier that pays for the extra difficulty.
    private static readonly Mutator[] Mutators =
    {
        new("Swarm", "Twice the numbers, half the hull. Area damage is king.",
            Tier("Swarm", hp: 0.55f, count: 1.8f, reward: 1.25f,
                 perks: ("turret_splash", 0.35f))),

        new("Ironhide", "Every hull is up-armoured. Bring something that punches once, hard.",
            Tier("Ironhide", hp: 1.2f, armor: 7f, reward: 1.35f,
                 perks: ("turret_armor_pen", 4f))),

        new("Blitz", "The Harvest is in a hurry. So are you.",
            Tier("Blitz", speed: 1.4f, reward: 1.3f,
                 perks: ("ability_cooldown", -0.3f), perk2: ("hero_missile_cd", -4f))),

        new("Bulwark", "Fat shields everywhere — and a planet that can take it.",
            Tier("Bulwark", hp: 1.15f, shield: 26f, reward: 1.4f,
                 perks: ("planet_integrity", 0.6f), perk2: ("turret_damage", 0.2f))),

        new("Glass Cannon", "Your guns hit like trucks. Your planet does not.",
            Tier("Glass Cannon", hp: 1.1f, reward: 1.3f,
                 perks: ("turret_damage", 0.7f), perk2: ("planet_damage_taken", 0.5f))),

        new("Overwhelm", "More of everything, faster. The battery works overtime.",
            Tier("Overwhelm", hp: 1.15f, speed: 1.15f, count: 1.35f, reward: 1.5f,
                 perks: ("battery_rate", 0.4f), perk2: ("battery_salvo", 1f))),

        new("Precision", "Thin ranks, tough targets. Every shot has to count.",
            Tier("Precision", hp: 1.35f, count: 0.7f, reward: 1.25f,
                 perks: ("turret_crit_chance", 0.2f), perk2: ("turret_crit_mult", 0.5f))),

        new("Sentinel Surge", "The Sentinel remembers how to fight. Lean on it.",
            Tier("Sentinel Surge", hp: 1.2f, speed: 1.1f, reward: 1.35f,
                 perks: ("sentinel_count", 2f), perk2: ("sentinel_damage", 0.6f))),
    };

    public static Week Current(System.DateTime? utcNow = null)
    {
        var now = (utcNow ?? System.DateTime.UtcNow).Date;
        int number = ISOWeek.GetWeekOfYear(now);
        int year = ISOWeek.GetYear(now);

        ulong h = Splitmix((ulong)year * 64UL + (ulong)number ^ 0x5EEDF00D5EEDF00DUL);
        var mut = Mutators[(int)(h % (ulong)Mutators.Length)];
        ulong seed = Splitmix(h ^ 0xA24BAED4963EE407UL) | 1UL;

        return new Week(year, number, seed, mut.Name, mut.Blurb, mut.Twist);
    }

    private static Config.AscensionTierDef Tier(
        string name, float hp = 1f, float speed = 1f, float shield = 0f, float armor = 0f,
        float count = 1f, float reward = 1f,
        (string k, float v)? perks = null, (string k, float v)? perk2 = null)
    {
        var fx = new Dictionary<string, float>();
        if (perks is { } p) fx[p.k] = p.v;
        if (perk2 is { } q) fx[q.k] = q.v;
        return new Config.AscensionTierDef
        {
            Tier = 0,
            Name = name,
            EnemyHpMult = hp,
            EnemySpeedMult = speed,
            EnemyShieldAdd = shield,
            EnemyArmorAdd = armor,
            EnemyCountMult = count,
            RewardMult = reward,
            PlayerEffects = fx,
        };
    }

    private static ulong Splitmix(ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }
}
