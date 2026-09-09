using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    // special ability field-effects, read by other systems
    internal float PdgActiveLeft;
    internal float PdgRadius;
    internal float SalvageActiveLeft;
    internal Vector2 SalvageAnchor;
    internal float SalvageRadius;
    internal float SalvageBonusFrac;
    internal float DronesActiveLeft;
    internal int DroneCount;
    internal float DroneDps;
    internal float DroneRange;

    private void StepAbilities()
    {
        float dt = SimClock.TickDelta;

        if (PdgActiveLeft > 0f) PdgActiveLeft -= dt;
        if (SalvageActiveLeft > 0f) SalvageActiveLeft -= dt;
        if (DronesActiveLeft > 0f) DronesActiveLeft -= dt;

        for (int i = 0; i < Abilities.Length; i++)
        {
            ref var a = ref Abilities[i];
            if (a.DefIndex < 0) continue;

            if (a.CooldownLeft > 0f) a.CooldownLeft -= dt;
            if (a.ActiveLeft <= 0f) continue;
            a.ActiveLeft -= dt;

            var def = AbilityDefs[a.DefIndex];
            a.TickAccum += dt;
            float interval = def.TickInterval <= 0f ? 0.2f : def.TickInterval;

            float pw = AbilityPower(i);
            switch (def.Kind)
            {
                case "barrage":
                    while (a.TickAccum >= interval) { a.TickAccum -= interval; ApplyBarrageTick(def.Damage * pw, a.P0, a.P1); }
                    break;

                case "lance":
                    while (a.TickAccum >= interval) { a.TickAccum -= interval; ApplyZoneDamage(a.Anchor, AbilityRad(def.Radius), def.Damage * pw); }
                    break;

                case "slow":
                    ApplyZoneSlow(a.Anchor, AbilityRad(def.Radius), def.Value);
                    break;

                case "snare":
                    ApplyZoneSnare(a.Anchor, AbilityRad(def.Radius), def.Value * pw);
                    break;

                case "repair":
                    PlanetIntegrity = Mathf.Min(PlanetIntegrityMax, PlanetIntegrity + def.Value * pw * dt);
                    if (Hero.Alive) Hero.Hull = Mathf.Min(Hero.MaxHull, Hero.Hull + def.Value2 * pw * dt);
                    break;
            }
        }
    }

    private void ApplyBarrageTick(float dmg, float center, float half)
    {
        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref readonly var e = ref Enemies[i];
            if (!e.Alive) continue;
            if (Mathf.Abs(Mathf.AngleDifference(center, e.Pos.Angle())) <= half)
                DamageEnemy(i, dmg, DamageSource.Ability);
        }
        Events.Push(SimEventKind.BarrageTick, Vector2.FromAngle(center) * (B.SpawnRadius * 0.6f), half);
    }

    private void ApplyZoneDamage(Vector2 anchor, float radius, float dmg)
    {
        float rSq = radius * radius;
        for (int i = 0; i < EnemyHighWater; i++)
        {
            if (!Enemies[i].Alive) continue;
            if (Enemies[i].Pos.DistanceSquaredTo(anchor) <= rSq)
                DamageEnemy(i, dmg, DamageSource.Ability);
        }
        Events.Push(SimEventKind.BarrageTick, anchor, radius);
    }

    private void ApplyZoneSlow(Vector2 anchor, float radius, float factor)
    {
        float rSq = radius * radius;
        for (int i = 0; i < EnemyHighWater; i++)
            if (Enemies[i].Alive && Enemies[i].Pos.DistanceSquaredTo(anchor) <= rSq)
                ApplySlow(i, factor);
    }

    private void ApplyZoneSnare(Vector2 anchor, float radius, float pullStrength)
    {
        float rSq = (radius * 1.6f) * (radius * 1.6f);
        for (int i = 0; i < EnemyHighWater; i++)
        {
            if (!Enemies[i].Alive) continue;
            if (Enemies[i].Pos.DistanceSquaredTo(anchor) > rSq) continue;
            ApplyPull(i, anchor, pullStrength);
            ApplySlow(i, 0.6f);
        }
    }

    private void TryCastAbility(int slot, Vector2 reticle)
    {
        if (slot < 0 || slot >= Abilities.Length) return;
        ref var a = ref Abilities[slot];
        if (a.DefIndex < 0 || a.CooldownLeft > 0f || Phase != SimPhase.Wave) return;

        var def = AbilityDefs[a.DefIndex];
        a.Anchor = reticle;
        a.TickAccum = 0f;
        float pw = AbilityPower(slot);

        switch (def.Kind)
        {
            case "barrage":
                a.P0 = reticle == Vector2.Zero ? 0f : reticle.Angle();
                a.P1 = Mathf.DegToRad(def.ArcDegrees <= 0f ? 60f : def.ArcDegrees) * 0.5f;
                a.ActiveLeft = Nz(def.Duration, 3f);
                break;

            case "lance":
                a.ActiveLeft = Nz(def.Duration, 3f);
                break;

            case "nova":
                CastNova(def, pw);
                a.ActiveLeft = 0.4f; // vfx window
                break;

            case "slow":
            case "snare":
                a.ActiveLeft = Nz(def.Duration, 4f);
                break;

            case "ion":
                CastIon(def, reticle, pw);
                a.ActiveLeft = 0.3f;
                break;

            case "barrier":
                a.P2 = Nz(def.Value, 400f) * pw;
                a.ActiveLeft = Nz(def.Duration, 8f);
                break;

            case "pointdef":
                PdgActiveLeft = Nz(def.Duration, 6f);
                PdgRadius = AbilityRad(Nz(def.Radius, 260f));
                a.ActiveLeft = PdgActiveLeft;
                break;

            case "repair":
                a.ActiveLeft = Nz(def.Duration, 6f);
                break;

            case "overdrive":
                Hero.OverdriveLeft = Nz(def.Duration, 6f);
                Hero.OverdriveVolleyMult = Nz(def.Value, 0.4f);
                a.ActiveLeft = Hero.OverdriveLeft;
                if (Hero.VolleyCooldownLeft > Cfg.Hero.VolleyCooldown * Hero.OverdriveVolleyMult)
                    Hero.VolleyCooldownLeft = Cfg.Hero.VolleyCooldown * Hero.OverdriveVolleyMult;
                break;

            case "salvage":
                SalvageActiveLeft = Nz(def.Duration, 10f);
                SalvageAnchor = reticle;
                SalvageRadius = AbilityRad(Nz(def.Radius, 220f));
                SalvageBonusFrac = Nz(def.Value, 0.5f) * pw;
                a.ActiveLeft = SalvageActiveLeft;
                break;

            case "drones":
                DronesActiveLeft = Nz(def.Duration, 10f);
                DroneCount = def.IntValue <= 0 ? 3 : def.IntValue;
                DroneDps = Nz(def.Damage, 20f) * pw;
                DroneRange = AbilityRad(Nz(def.Radius, 220f));
                a.ActiveLeft = DronesActiveLeft;
                break;
        }

        a.CooldownLeft = AbilityCd(def, slot);
        Events.Push(SimEventKind.AbilityCast, reticle, 0f, slot);
    }

    private void CastNova(Config.AbilityDef def, float pw)
    {
        float r = AbilityRad(Nz(def.Radius, 260f));
        float rSq = r * r;
        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref var e = ref Enemies[i];
            if (!e.Alive) continue;
            if (e.Pos.LengthSquared() > rSq) continue;
            DamageEnemy(i, def.Damage * pw, DamageSource.Ability);
            if (!_missionEnemyDefs[e.DefIndex].CcImmune)
            {
                Vector2 outward = e.Pos.LengthSquared() > 1f ? e.Pos.Normalized() : Vector2.Up;
                e.Pos += outward * Nz(def.Value, 70f);
                e.DistToCenter = e.Pos.Length();
                e.Standoff = false;
            }
        }
        Events.Push(SimEventKind.NovaPulse, Vector2.Zero, r);
    }

    private void CastIon(Config.AbilityDef def, Vector2 reticle, float pw)
    {
        int start = ClosestEnemyTo(reticle == Vector2.Zero ? Vector2.Zero : reticle, 9999f);
        if (start < 0) return;
        int jumps = def.IntValue <= 0 ? 4 : def.IntValue;
        int cur = start;
        System.Span<bool> hit = stackalloc bool[64];
        for (int j = 0; j < jumps && cur >= 0; j++)
        {
            ref var e = ref Enemies[cur];
            e.Shield = 0f;                       // strip shields
            e.ShieldRegenTimer = -Nz(def.Value2, 3f); // delay regen extra
            DamageEnemy(cur, def.Damage * pw, DamageSource.Ability);
            ApplySlow(cur, 0.5f);
            if (cur < 64) hit[cur] = true;
            Events.Push(SimEventKind.BarrageTick, e.Pos, 3f);

            Vector2 anchor = e.Pos;
            int next = -1; float best = 140f * 140f;
            for (int i = 0; i < EnemyHighWater; i++)
            {
                if (!Enemies[i].Alive || (i < 64 && hit[i])) continue;
                float d = Enemies[i].Pos.DistanceSquaredTo(anchor);
                if (d < best) { best = d; next = i; }
            }
            cur = next;
        }
    }

    private static float Nz(float v, float fallback) => v <= 0f ? fallback : v;

    /// <summary>Effect multiplier for an equipped ability: its level, the branch
    /// picks at 5/10/15/20, and the Sentinel Protocols research.</summary>
    private float AbilityPower(int slot)
    {
        float eff = Abilities[slot].EffMult <= 0f ? 1f : Abilities[slot].EffMult;
        return eff * Mathf.Max(0.2f, Mods.AbilityEffectMult);
    }

    private float AbilityCd(Config.AbilityDef def, int slot)
    {
        float cm = Abilities[slot].CdMult <= 0f ? 1f : Abilities[slot].CdMult;
        return def.Cooldown * Mathf.Max(0.2f, Mods.AbilityCooldownMult) * cm;
    }
    private float AbilityRad(float r) => r * Mathf.Max(0.3f, Mods.AbilityRadiusMult);
}
