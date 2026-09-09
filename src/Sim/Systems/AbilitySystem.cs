using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    private const float BarrageTickInterval = 0.2f;

    private void StepAbilities()
    {
        float dt = SimClock.TickDelta;

        for (int i = 0; i < Abilities.Length; i++)
        {
            ref var a = ref Abilities[i];
            if (a.DefIndex < 0) continue;

            if (a.CooldownLeft > 0f) a.CooldownLeft -= dt;
            if (a.ActiveLeft <= 0f) continue;

            a.ActiveLeft -= dt;
            var def = AbilityDefs[a.DefIndex];

            if (def.Kind == "barrage")
            {
                a.TickAccum += dt;
                while (a.TickAccum >= BarrageTickInterval)
                {
                    a.TickAccum -= BarrageTickInterval;
                    ApplyBarrageTick(def, a.P0, a.P1);
                }
            }
        }
    }

    private void ApplyBarrageTick(Config.AbilityDef def, float center, float half)
    {
        float dmg = def.Damage;
        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref readonly var e = ref Enemies[i];
            if (!e.Alive) continue;
            // enemies further than the planet count; the slugs saturate the whole radial sector
            float ang = e.Pos.Angle();
            if (Mathf.Abs(Mathf.AngleDifference(center, ang)) <= half)
                DamageEnemy(i, dmg, DamageSource.Ability);
        }
        // one event at the sector midpoint for VFX
        Vector2 midDir = Vector2.FromAngle(center);
        Events.Push(SimEventKind.BarrageTick, midDir * (B.SpawnRadius * 0.6f), half);
    }

    private void TryCastAbility(int slot, Vector2 reticle)
    {
        if (slot < 0 || slot >= Abilities.Length) return;
        ref var a = ref Abilities[slot];
        if (a.DefIndex < 0 || a.CooldownLeft > 0f) return;
        if (Phase != SimPhase.Wave) return;

        var def = AbilityDefs[a.DefIndex];
        switch (def.Kind)
        {
            case "barrage":
                a.P0 = reticle == Vector2.Zero ? 0f : reticle.Angle();
                a.P1 = Mathf.DegToRad(def.ArcDegrees <= 0f ? 60f : def.ArcDegrees) * 0.5f;
                a.ActiveLeft = def.Duration <= 0f ? 3f : def.Duration;
                a.TickAccum = 0f;
                break;

            case "barrier":
                a.P2 = def.Value <= 0f ? 400f : def.Value;
                a.ActiveLeft = def.Duration <= 0f ? 8f : def.Duration;
                break;

            case "overdrive":
                Hero.OverdriveLeft = def.Duration <= 0f ? 6f : def.Duration;
                Hero.OverdriveVolleyMult = def.Value <= 0f ? 0.4f : def.Value;
                a.ActiveLeft = Hero.OverdriveLeft;
                // let the player immediately re-fire
                if (Hero.VolleyCooldownLeft > Cfg.Hero.VolleyCooldown * Hero.OverdriveVolleyMult)
                    Hero.VolleyCooldownLeft = Cfg.Hero.VolleyCooldown * Hero.OverdriveVolleyMult;
                break;
        }

        a.CooldownLeft = def.Cooldown;
        Events.Push(SimEventKind.AbilityCast, reticle, 0f, slot);
    }
}
