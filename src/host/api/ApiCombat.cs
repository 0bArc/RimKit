using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.combat: verbs, projectiles, explosions, fire, armor, turrets. Ops are combat.*.
    internal static class ApiCombat
    {
        public static void Register()
        {
            R("combat.verbs", Verbs);
            R("combat.attack", Attack);
            R("combat.explode", Explode);
            R("combat.start_fire", StartFire);
            R("combat.extinguish", Extinguish);
            R("combat.fires", Fires);
            R("combat.armor", Armor);
            R("combat.launch", Launch);
            R("combat.damage_defs", DamageDefs);
            R("combat.projectile_defs", ProjectileDefs);
            R("combat.turret_info", TurretInfo);
            R("combat.turret_target", TurretTarget);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.7.0");

        private static IEnumerable<Verb> VerbsOf(Thing t)
        {
            if (t is Pawn p)
            {
                foreach (Verb v in p.VerbTracker.AllVerbs) yield return v;
                if (p.equipment?.Primary != null)
                {
                    var eq = p.equipment.Primary.TryGetComp<CompEquippable>();
                    if (eq != null) foreach (Verb v in eq.AllVerbs) yield return v;
                }
            }
            else
            {
                var eq = (t as ThingWithComps)?.GetComp<CompEquippable>();
                if (eq != null) foreach (Verb v in eq.AllVerbs) yield return v;
            }
        }

        private static string Verbs(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            var arr = Jb.Arr();
            foreach (Verb v in VerbsOf(t))
            {
                VerbProperties vp = v.verbProps;
                var j = Jb.Obj().S("label", vp.label).S("class", vp.verbClass?.Name).F("range", vp.range).I("burst", vp.burstShotCount)
                    .F("warmup", vp.warmupTime).F("cooldown", vp.defaultCooldownTime).B("melee", vp.IsMeleeAttack).S("projectile", vp.defaultProjectile?.defName);
                if (vp.IsMeleeAttack) j.F("melee_damage", vp.AdjustedMeleeDamageAmount(v, v.CasterPawn));
                else if (vp.defaultProjectile != null) j.I("damage", vp.defaultProjectile.projectile.GetDamageAmount(v.EquipmentSource));
                arr.Add(j);
            }

            return arr.Ok();
        }

        private static string Attack(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            Thing target = ObjectHandles.Get<Thing>(Int(a, "target"));
            if (p?.jobs == null || target == null) return Fail("RK2001", "pawn or target handle is stale or null");
            Verb verb = p.TryGetAttackVerb(target, true);
            if (verb == null) return Fail("RK3003", "the pawn has no way to attack that");
            Job job = JobMaker.MakeJob(verb.verbProps.IsMeleeAttack ? JobDefOf.AttackMelee : JobDefOf.AttackStatic, target);
            return OkBool(p.jobs.TryTakeOrderedJob(job, JobTag.Misc));
        }

        // opts: damage_def (default Bomb), damage (default from the def), instigator (thing).
        private static string Explode(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            Opts o = Opts.From(a, "opts");
            DamageDef def = DefDatabase<DamageDef>.GetNamedSilentFail(o.Str("damage_def", "Bomb"));
            if (def == null) return Fail("RK3001", "unknown damage def " + o.Str("damage_def"));
            float radius = Math.Max(0.5f, Float(a, "radius"));
            GenExplosion.DoExplosion(c, map, radius, def, o.Handle<Thing>("instigator"), o.Has("damage") ? o.Int("damage") : -1);
            return OkBool(true);
        }

        private static string StartFire(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            float size = string.IsNullOrEmpty(Str(a, "size")) ? 0.1f : Math.Max(0.05f, Math.Min(1f, Float(a, "size")));
            return OkBool(FireUtility.TryStartFireIn(c, map, size, null));
        }

        private static string Extinguish(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            int n = 0;
            foreach (Thing t in c.GetThingList(map).ToList())
            {
                if (t is Fire f) { f.Destroy(); n++; }
            }

            return OkInt(n);
        }

        private static string Fires(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            return OkHandles(map.listerThings.ThingsOfDef(ThingDefOf.Fire).ToList());
        }

        private static string Armor(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            return Jb.Obj().F("sharp", t.GetStatValue(StatDefOf.ArmorRating_Sharp)).F("blunt", t.GetStatValue(StatDefOf.ArmorRating_Blunt))
                .F("heat", t.GetStatValue(StatDefOf.ArmorRating_Heat)).Ok();
        }

        // Launches a projectile def from one cell to another. opts: launcher (thing).
        private static string Launch(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "projectile"));
            if (def == null || def.projectile == null) return Fail("RK3001", Str(a, "projectile") + " is not a projectile def");
            var from = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            var to = new IntVec3(Int(a, "x2"), 0, Int(a, "z2"));
            if (!from.InBounds(map) || !to.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            Opts o = Opts.From(a, "opts");
            var proj = (Projectile)GenSpawn.Spawn(ThingMaker.MakeThing(def), from, map);
            proj.Launch(o.Handle<Thing>("launcher"), from.ToVector3Shifted(), to, to, ProjectileHitFlags.IntendedTarget);
            return OkHandle(proj);
        }

        private static string DamageDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (DamageDef d in DefDatabase<DamageDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).B("harmful", d.harmsHealth).B("explosive", d.isExplosive));
            return arr.Ok();
        }

        private static string ProjectileDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading.Where(x => x.projectile != null))
            {
                int damage = 0;
                float penetration = 0f;
                try { damage = d.projectile.GetDamageAmount(null); penetration = d.projectile.GetArmorPenetration(null); }
                catch (Exception) { damage = 0; }
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).I("damage", damage).F("speed", d.projectile.speed)
                    .S("damage_def", d.projectile.damageDef?.defName).F("armor_penetration", penetration));
            }
            return arr.Ok();
        }

        private static string TurretInfo(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (!(t is Building_Turret turret)) return Fail("RK3003", "the thing is not a turret");
            var j = Jb.Obj().B("has_target", turret.CurrentTarget.IsValid).S("mode", turret.GetType().Name);
            if (turret.CurrentTarget.HasThing) j.H("target", turret.CurrentTarget.Thing);
            return j.Ok();
        }

        private static string TurretTarget(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (!(t is Building_TurretGun turret)) return Fail("RK3003", "the thing is not a gun turret");
            Thing target = ObjectHandles.Get<Thing>(Int(a, "target"));
            if (target == null)
            {
                // The method that clears a forced target has had different names between game versions.
                var reset = AccessTools.Method(typeof(Building_TurretGun), "ResetForcedTarget") ?? AccessTools.Method(typeof(Building_TurretGun), "ResetCurrentTarget");
                if (reset == null) return Fail("RK3003", "clearing a forced turret target is not supported on this game version");
                reset.Invoke(turret, null);
                return OkBool(true);
            }
            turret.OrderAttack(target);
            return OkBool(true);
        }
    }
}
