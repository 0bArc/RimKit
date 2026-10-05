using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimKit
{
    // Lua host ops (op name + json args).
    internal static class GameApi
    {
        public static string Invoke(string op, string argsJson)
        {
            try
            {
                var args = JsonLite.ParseObject(argsJson ?? "{}");
                if (ApiRegistry.TryInvoke(op, args, out string domainResult))
                {
                    return domainResult;
                }
                switch (op)
                {
                    case "find.tick": return OkInt(Current.Game == null ? 0 : Find.TickManager?.TicksGame ?? 0);
                    case "find.current_map": return OkHandle(Current.Game == null ? null : Find.CurrentMap);
                    case "find.world": return OkHandle(Find.World);
                    case "find.camera_driver": return OkHandle(Find.CameraDriver);
                    case "find.selector_first":
                    {
                        var sel = Find.Selector;
                        if (sel == null) return OkHandle(null);
                        if (sel.SelectedPawns != null)
                        {
                            foreach (Pawn p in sel.SelectedPawns)
                            {
                                if (p != null && !p.Destroyed) return OkHandle(p);
                            }
                        }
                        if (sel.SelectedObjectsListForReading != null)
                        {
                            foreach (object o in sel.SelectedObjectsListForReading)
                            {
                                if (o is Pawn p && !p.Destroyed) return OkHandle(p);
                            }
                            foreach (object o in sel.SelectedObjectsListForReading)
                            {
                                if (o is Thing t && !t.Destroyed) return OkHandle(t);
                            }
                        }
                        if (sel.FirstSelectedObject is Pawn fp) return OkHandle(fp);
                        if (sel.FirstSelectedObject is Thing ft) return OkHandle(ft);
                        return OkHandle(sel.SingleSelectedThing);
                    }
                    case "find.selected_things":
                    {
                        var list = new List<object>();
                        if (Find.Selector?.SelectedObjects != null)
                        {
                            foreach (object o in Find.Selector.SelectedObjects)
                            {
                                list.Add(o);
                            }
                        }
                        return OkHandles(list);
                    }
                    case "find.maps": return OkHandles(Find.Maps);
                    case "find.any_player_pawn": return OkHandle(Find.AnyPlayerHomeMap?.mapPawns?.FreeColonists?.FirstOrDefault());

                    case "defs.get": return OkHandle(GetDef(Str(args, "type"), Str(args, "name")));
                    case "defs.exists": return OkBool(GetDef(Str(args, "type"), Str(args, "name")) != null);
                    case "defs.list": return OkStringList(ListDefNames(Str(args, "type")));
                    case "defs.label": return OkStr(ObjectHandles.Get<Def>(Int(args, "h"))?.label ?? "");

                    case "map.nutrition": return OkFloat(MapOf(args)?.resourceCounter?.TotalHumanEdibleNutrition ?? 0f);
                    case "map.width": return OkInt(MapOf(args)?.Size.x ?? 0);
                    case "map.height": return OkInt(MapOf(args)?.Size.z ?? 0);
                    case "map.pawns": return OkHandles(MapOf(args)?.mapPawns?.AllPawns);
                    case "map.colonists": return OkHandles(MapOf(args)?.mapPawns?.FreeColonists);
                    case "map.prisoners": return OkHandles(MapOf(args)?.mapPawns?.PrisonersOfColony);
                    case "map.things": return OkHandles(MapOf(args)?.listerThings?.AllThings);
                    case "map.things_of_def":
                    {
                        var map = MapOf(args);
                        var def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(args, "def"));
                        if (map == null || def == null) return OkHandles(null);
                        return OkHandles(map.listerThings.ThingsOfDef(def));
                    }
                    case "map.spawn":
                    {
                        var map = MapOf(args);
                        var def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(args, "def"));
                        if (map == null || def == null) return Err("bad map/def");
                        IntVec3 cell = new IntVec3(Int(args, "x"), 0, Int(args, "z"));
                        ThingDef stuff = null;
                        if (args.ContainsKey("stuff") && !string.IsNullOrEmpty(Str(args, "stuff")))
                        {
                            stuff = DefDatabase<ThingDef>.GetNamedSilentFail(Str(args, "stuff"));
                        }
                        Thing t = ThingMaker.MakeThing(def, stuff);
                        if (args.ContainsKey("stack"))
                        {
                            t.stackCount = Int(args, "stack");
                        }
                        GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near);
                        return OkHandle(t);
                    }
                    case "map.find_cells":
                    {
                        var map = MapOf(args);
                        if (map == null) return Err("no map");
                        int limit = args.ContainsKey("limit") ? Int(args, "limit") : 64;
                        if (limit <= 0) limit = 64;
                        string terrainName = Str(args, "terrain");
                        TerrainDef terrain = string.IsNullOrEmpty(terrainName)
                            ? null
                            : DefDatabase<TerrainDef>.GetNamedSilentFail(terrainName);
                        var cells = new List<string>();
                        foreach (IntVec3 c in map.AllCells)
                        {
                            if (terrain != null && map.terrainGrid.TerrainAt(c) != terrain)
                            {
                                continue;
                            }
                            cells.Add(c.x.ToString(CultureInfo.InvariantCulture) + "," +
                                      c.z.ToString(CultureInfo.InvariantCulture));
                            if (cells.Count >= limit)
                            {
                                break;
                            }
                        }
                        return OkStringList(cells);
                    }

                    case "thing.def": return OkStr(ThingOf(args)?.def?.defName ?? "");
                    case "thing.label": return OkStr(ThingOf(args)?.LabelCap ?? "");
                    case "thing.label_short": return OkStr(ThingOf(args)?.LabelShort ?? "");
                    case "thing.destroy":
                    {
                        ThingOf(args)?.Destroy(DestroyMode.Vanish);
                        return OkBool(true);
                    }
                    case "thing.despawn":
                    {
                        ThingOf(args)?.DeSpawn();
                        return OkBool(true);
                    }
                    case "thing.pos":
                    {
                        var t = ThingOf(args);
                        return t == null ? OkStr("") : OkStr(t.Position.x + "," + t.Position.z);
                    }
                    case "thing.set_pos":
                    {
                        var t = ThingOf(args);
                        if (t == null) return Err("no thing");
                        t.Position = new IntVec3(Int(args, "x"), 0, Int(args, "z"));
                        return OkBool(true);
                    }
                    case "thing.hp": return OkInt(ThingOf(args)?.HitPoints ?? 0);
                    case "thing.max_hp": return OkInt(ThingOf(args)?.MaxHitPoints ?? 0);
                    case "thing.set_hp":
                    {
                        var t = ThingOf(args);
                        if (t == null) return Err("no thing");
                        t.HitPoints = Int(args, "v");
                        return OkBool(true);
                    }
                    case "thing.stack": return OkInt(ThingOf(args)?.stackCount ?? 0);
                    case "thing.set_stack":
                    {
                        var t = ThingOf(args);
                        if (t == null) return Err("no thing");
                        t.stackCount = Int(args, "v");
                        return OkBool(true);
                    }
                    case "thing.faction": return OkHandle(ThingOf(args)?.Faction);
                    case "thing.set_faction":
                    {
                        var t = ThingOf(args);
                        var f = ObjectHandles.Get<Faction>(Int(args, "faction"));
                        if (t == null) return Err("no thing");
                        t.SetFaction(f);
                        return OkBool(true);
                    }
                    case "thing.map": return OkHandle(ThingOf(args)?.Map);
                    case "thing.spawned": return OkBool(ThingOf(args)?.Spawned == true);

                    case "pawn.is_humanlike": return OkBool(PawnOf(args)?.RaceProps?.Humanlike == true);
                    case "pawn.is_colonist": return OkBool(PawnOf(args)?.IsColonist == true);
                    case "pawn.is_prisoner": return OkBool(PawnOf(args)?.IsPrisoner == true);
                    case "pawn.is_slave": return OkBool(PawnOf(args)?.IsSlave == true);
                    case "pawn.is_downed": return OkBool(PawnOf(args)?.Downed == true);
                    case "pawn.is_dead": return OkBool(PawnOf(args)?.Dead == true);
                    case "pawn.faction_is_player": return OkBool(PawnOf(args)?.Faction == Faction.OfPlayer);
                    case "pawn.name": return OkStr(PawnOf(args)?.Name?.ToStringFull ?? PawnOf(args)?.LabelShort ?? "");
                    case "pawn.gender": return OkStr(PawnOf(args)?.gender.ToString() ?? "");
                    case "pawn.age": return OkFloat(PawnOf(args)?.ageTracker?.AgeBiologicalYearsFloat ?? 0f);
                    case "pawn.kind": return OkStr(PawnOf(args)?.kindDef?.defName ?? "");
                    case "pawn.map": return OkHandle(PawnOf(args)?.Map);
                    case "pawn.faction": return OkHandle(PawnOf(args)?.Faction);
                    case "pawn.hunger": return OkFloat(PawnOf(args)?.needs?.food?.CurLevelPercentage ?? 1f);
                    case "pawn.rest": return OkFloat(PawnOf(args)?.needs?.rest?.CurLevelPercentage ?? 1f);
                    case "pawn.recreation": return OkFloat(NeedPct(PawnOf(args), "Joy") ?? NeedPct(PawnOf(args), "Outdoors") ?? 1f);
                    case "pawn.mood": return OkFloat(PawnOf(args)?.needs?.mood?.CurLevelPercentage ?? 1f);
                    case "pawn.set_hunger":
                    {
                        var p = PawnOf(args);
                        if (p?.needs?.food != null) p.needs.food.CurLevelPercentage = Float(args, "v");
                        return OkBool(true);
                    }
                    case "pawn.set_rest":
                    {
                        var p = PawnOf(args);
                        if (p?.needs?.rest != null) p.needs.rest.CurLevelPercentage = Float(args, "v");
                        return OkBool(true);
                    }
                    case "pawn.health_pct": return OkFloat(PawnOf(args)?.health?.summaryHealth?.SummaryHealthPercent ?? 1f);
                    case "pawn.kill":
                    {
                        PawnOf(args)?.Kill(null);
                        return OkBool(true);
                    }
                    case "pawn.drafted": return OkBool(PawnOf(args)?.Drafted == true);
                    case "pawn.set_drafted":
                    {
                        var p = PawnOf(args);
                        if (p?.drafter != null) p.drafter.Drafted = Bool(args, "v");
                        return OkBool(true);
                    }
                    case "pawn.equipment": return OkHandles(EquipmentThings(PawnOf(args)));
                    case "pawn.apparel": return OkHandles(ApparelThings(PawnOf(args)));
                    case "pawn.inventory": return OkHandles(InventoryThings(PawnOf(args)));
                    case "pawn.carry": return OkHandle(PawnOf(args)?.carryTracker?.CarriedThing);
                    case "pawn.give_hediff":
                    {
                        var p = PawnOf(args);
                        var def = DefDatabase<HediffDef>.GetNamedSilentFail(Str(args, "def"));
                        if (p == null || def == null) return Err("bad pawn/hediff");
                        Hediff h = HediffMaker.MakeHediff(def, p);
                        if (args.ContainsKey("severity")) h.Severity = Float(args, "severity");
                        p.health.AddHediff(h);
                        return OkHandle(h);
                    }
                    case "pawn.remove_hediff":
                    {
                        var p = PawnOf(args);
                        var def = DefDatabase<HediffDef>.GetNamedSilentFail(Str(args, "def"));
                        if (p == null || def == null) return Err("bad pawn/hediff");
                        Hediff existing = p.health.hediffSet.GetFirstHediffOfDef(def);
                        if (existing != null) p.health.RemoveHediff(existing);
                        return OkBool(existing != null);
                    }
                    case "pawn.hediffs": return OkHandles(PawnOf(args)?.health?.hediffSet?.hediffs);
                    case "pawn.skill": return OkInt(PawnOf(args)?.skills?.GetSkill(SkillDefOfNamed(Str(args, "skill")))?.Level ?? -1);
                    case "pawn.set_skill":
                    {
                        var p = PawnOf(args);
                        var skill = p?.skills?.GetSkill(SkillDefOfNamed(Str(args, "skill")));
                        if (skill == null) return Err("no skill");
                        skill.Level = Int(args, "v");
                        return OkBool(true);
                    }
                    case "pawn.give_thing":
                    case "pawn.give_item":
                    {
                        var p = PawnOf(args);
                        string want = Str(args, "def");
                        var def = ResolveThingDef(want,
                            "ComponentIndustrial", "Component", "ComponentSpacer",
                            "Steel", "Silver");
                        if (p == null || def == null) return Err("bad pawn/def:" + want);
                        Thing t = ThingMaker.MakeThing(def);
                        if (args.ContainsKey("stack")) t.stackCount = Math.Max(1, Int(args, "stack"));
                        if (t.def.stackLimit > 0 && t.stackCount > t.def.stackLimit)
                        {
                            t.stackCount = t.def.stackLimit;
                        }
                        if (p.inventory == null || !p.inventory.innerContainer.TryAdd(t))
                        {
                            GenPlace.TryPlaceThing(t, p.PositionHeld, p.MapHeld, ThingPlaceMode.Near);
                        }
                        return OkHandle(t);
                    }
                    case "pawn.set_name":
                    {
                        var p = PawnOf(args);
                        if (p == null) return Err("no pawn");
                        string v = Str(args, "v");
                        if (p.Name is NameTriple nt)
                        {
                            p.Name = new NameTriple(nt.First, v, nt.Last);
                        }
                        else
                        {
                            p.Name = new NameSingle(v);
                        }
                        return OkBool(true);
                    }
                    case "pawn.add_trait":
                    {
                        var p = PawnOf(args);
                        var td = DefDatabase<TraitDef>.GetNamedSilentFail(Str(args, "def"));
                        if (p?.story?.traits == null || td == null) return Err("bad pawn/trait");
                        if (!p.story.traits.HasTrait(td))
                        {
                            p.story.traits.GainTrait(new Trait(td));
                        }
                        return OkBool(true);
                    }
                    case "pawn.remove_trait":
                    {
                        var p = PawnOf(args);
                        var td = DefDatabase<TraitDef>.GetNamedSilentFail(Str(args, "def"));
                        if (p?.story?.traits == null || td == null) return Err("bad pawn/trait");
                        Trait existing = p.story.traits.GetTrait(td);
                        if (existing != null)
                        {
                            p.story.traits.RemoveTrait(existing);
                        }
                        return OkBool(existing != null);
                    }
                    case "pawn.seek_medical":
                    {
                        var p = PawnOf(args);
                        if (p?.jobs == null) return Err("no pawn");
                        JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("PatientGoToBed")
                            ?? DefDatabase<JobDef>.GetNamedSilentFail("Wait_SafeTemperature");
                        if (jobDef == null) return Err("no medical job def");
                        Job job = JobMaker.MakeJob(jobDef);
                        return OkBool(p.jobs.TryTakeOrderedJob(job));
                    }
                    case "pawn.pos":
                    {
                        var p = PawnOf(args);
                        return p == null ? OkStr("") : OkStr(p.Position.x + "," + p.Position.z);
                    }
                    case "pawn.is_moving": return OkBool(PawnOf(args)?.pather?.Moving == true);
                    case "pawn.can_reach":
                    {
                        var p = PawnOf(args);
                        if (p?.Map == null) return OkBool(false);
                        IntVec3 cell = new IntVec3(Int(args, "x"), 0, Int(args, "z"));
                        if (!cell.InBounds(p.Map)) return OkBool(false);
                        return OkBool(p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly));
                    }
                    case "pawn.walk_to":
                    {
                        var p = PawnOf(args);
                        if (p?.jobs == null || p.Map == null) return Err("no pawn/map");
                        IntVec3 cell = new IntVec3(Int(args, "x"), 0, Int(args, "z"));
                        if (!cell.InBounds(p.Map)) return Err("out of bounds");
                        if (!p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) return OkBool(false);
                        if (p.drafter != null) p.drafter.Drafted = true;
                        Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                        job.locomotionUrgency = (args.ContainsKey("sprint") && Bool(args, "sprint"))
                            ? LocomotionUrgency.Sprint
                            : LocomotionUrgency.Jog;
                        return OkBool(p.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder));
                    }
                    case "pawn.wander":
                    {
                        var p = PawnOf(args);
                        if (p?.jobs == null || p.Map == null) return Err("no pawn/map");
                        int radius = args.ContainsKey("radius") ? Int(args, "radius") : 12;
                        if (radius < 1) radius = 12;
                        if (p.drafter != null) p.drafter.Drafted = true;
                        IntVec3 dest = IntVec3.Invalid;
                        bool found = CellFinder.TryFindRandomCellNear(p.Position, p.Map, radius,
                            c => c.Standable(p.Map) && p.CanReach(c, PathEndMode.OnCell, Danger.Deadly),
                            out dest);
                        if (!found || !dest.IsValid) return OkBool(false);
                        Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
                        job.locomotionUrgency = LocomotionUrgency.Jog;
                        return OkBool(p.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder));
                    }
                    case "pawn.stop":
                    {
                        PawnOf(args)?.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                        return OkBool(true);
                    }
                    case "pawn.equip_weapon":
                    {
                        var p = PawnOf(args);
                        if (p?.equipment == null) return Err("no equipment");
                        string defName = Str(args, "def");
                        if (string.IsNullOrEmpty(defName)) defName = "Gun_AssaultRifle";
                        ThingDef def = ResolveThingDef(defName,
                            "Gun_AssaultRifle", "Gun_Revolver", "Gun_BoltActionRifle");
                        if (def == null) return Err("no weapon def");
                        if (p.equipment.Primary != null)
                        {
                            p.equipment.TryDropEquipment(p.equipment.Primary, out _, p.PositionHeld);
                        }
                        ThingWithComps gun = (ThingWithComps)ThingMaker.MakeThing(def);
                        p.equipment.AddEquipment(gun);
                        return OkBool(true);
                    }
                    case "pawn.shoot_hostiles":
                    {
                        var p = PawnOf(args);
                        if (p?.Map == null || p.jobs == null) return Err("no pawn/map");
                        if (p.drafter != null) p.drafter.Drafted = true;
                        bool instant = args.ContainsKey("instant") && Bool(args, "instant");
                        // Snapshot before TakeDamage mutates the list.
                        List<Pawn> hostiles = new List<Pawn>();
                        foreach (Pawn other in p.Map.mapPawns.AllPawnsSpawned.ToList())
                        {
                            if (other == null || other == p || other.Dead || other.Destroyed) continue;
                            if (!other.HostileTo(p)) continue;
                            hostiles.Add(other);
                        }
                        int n = 0;
                        if (instant)
                        {
                            foreach (Pawn other in hostiles)
                            {
                                if (other.Dead || other.Destroyed) continue;
                                other.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 999f, 0f, -1f, p));
                                n++;
                            }
                            return OkInt(n);
                        }
                        Pawn nearest = null;
                        float best = float.MaxValue;
                        foreach (Pawn other in hostiles)
                        {
                            float d = other.Position.DistanceToSquared(p.Position);
                            if (d < best)
                            {
                                best = d;
                                nearest = other;
                            }
                        }
                        if (nearest == null) return OkInt(0);
                        JobDef attackDef = JobDefOf.AttackStatic ?? JobDefOf.AttackMelee;
                        Job job = JobMaker.MakeJob(attackDef, nearest);
                        if (p.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder)) n = 1;
                        return OkInt(n);
                    }
                    case "control.set":
                    {
                        var p = PawnOf(args);
                        bool walk = !args.ContainsKey("click_walk") || Bool(args, "click_walk");
                        ControlBridge.Set(p, walk);
                        // Close debug windows so map clicks work.
                        if (Find.WindowStack != null)
                        {
                            for (int i = Find.WindowStack.Count - 1; i >= 0; i--)
                            {
                                Window w = Find.WindowStack[i];
                                if (w is RimLuaDebugWindow)
                                {
                                    w.Close(false);
                                }
                            }
                        }
                        return OkBool(p != null);
                    }
                    case "control.clear":
                    {
                        ControlBridge.Clear();
                        return OkBool(true);
                    }
                    case "control.get": return OkHandle(ControlBridge.ControlledPawn);
                    case "path.compute":
                    {
                        var p = PawnOf(args);
                        if (p?.Map == null) return OkStringList(null);
                        IntVec3 dest = new IntVec3(Int(args, "x"), 0, Int(args, "z"));
                        if (!dest.InBounds(p.Map)) return OkStringList(null);
                        var cells = new List<string>();
                        PawnPath path = p.Map.pathFinder.FindPathNow(p.Position, dest, TraverseParms.For(p), null, PathEndMode.OnCell);
                        try
                        {
                            if (path == null || !path.Found) return OkStringList(cells);
                            var nodes = path.NodesReversed;
                            for (int i = nodes.Count - 1; i >= 0; i--)
                            {
                                IntVec3 c = nodes[i];
                                cells.Add(c.x.ToString(CultureInfo.InvariantCulture) + "," +
                                          c.z.ToString(CultureInfo.InvariantCulture));
                            }
                        }
                        finally
                        {
                            path?.ReleaseToPool();
                        }
                        return OkStringList(cells);
                    }
                    case "map.spawn_pawn":
                    {
                        var map = MapOf(args);
                        if (map == null) return Err("no map");
                        string kindName = Str(args, "kind");
                        if (string.IsNullOrEmpty(kindName)) kindName = "Villager";
                        PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName)
                            ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Tribesperson")
                            ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Colonist");
                        if (kind == null) return Err("bad pawn kind");
                        string side = Str(args, "faction");
                        Faction fac = Faction.OfPlayer;
                        if (side.Equals("hostile", StringComparison.OrdinalIgnoreCase))
                        {
                            fac = Find.FactionManager?.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false)
                                  ?? Faction.OfAncientsHostile;
                        }
                        else if (side.Equals("neutral", StringComparison.OrdinalIgnoreCase))
                        {
                            fac = Faction.OfAncients;
                        }
                        IntVec3 cell;
                        if (args.ContainsKey("x") && args.ContainsKey("z"))
                        {
                            cell = new IntVec3(Int(args, "x"), 0, Int(args, "z"));
                        }
                        else
                        {
                            cell = DropCellFinder.RandomDropSpot(map);
                        }
                        if (!cell.InBounds(map)) cell = CellFinder.RandomEdgeCell(map);
                        Pawn np = PawnGenerator.GeneratePawn(kind, fac);
                        GenSpawn.Spawn(np, cell, map);
                        return OkHandle(np);
                    }
                    case "pawn.strip":
                    {
                        var p = PawnOf(args);
                        if (p?.apparel != null)
                        {
                            p.apparel.DropAll(p.PositionHeld);
                        }
                        return OkBool(true);
                    }
                    case "pawn.job_def": return OkStr(PawnOf(args)?.CurJobDef?.defName ?? "");
                    case "pawn.end_job":
                    {
                        PawnOf(args)?.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                        return OkBool(true);
                    }

                    case "faction.player": return OkHandle(Faction.OfPlayer);
                    case "faction.of_def":
                    {
                        var fd = DefDatabase<FactionDef>.GetNamedSilentFail(Str(args, "def"));
                        return OkHandle(fd == null ? null : Find.FactionManager?.FirstFactionOfDef(fd));
                    }
                    case "faction.name": return OkStr(ObjectHandles.Get<Faction>(Int(args, "h"))?.Name ?? "");
                    case "faction.list": return OkHandles(Find.FactionManager?.AllFactionsListForReading);
                    case "faction.is_hostile":
                    {
                        var a = ObjectHandles.Get<Faction>(Int(args, "h"));
                        var b = ObjectHandles.Get<Faction>(Int(args, "other"));
                        return OkBool(a != null && b != null && a.HostileTo(b));
                    }
                    case "faction.set_relation":
                    {
                        var a = ObjectHandles.Get<Faction>(Int(args, "h"));
                        var b = ObjectHandles.Get<Faction>(Int(args, "other"));
                        if (a == null || b == null) return Err("bad faction");
                        string kind = Str(args, "kind");
                        FactionRelationKind rel = FactionRelationKind.Neutral;
                        if (kind.Equals("Hostile", StringComparison.OrdinalIgnoreCase))
                        {
                            rel = FactionRelationKind.Hostile;
                        }
                        else if (kind.Equals("Ally", StringComparison.OrdinalIgnoreCase))
                        {
                            rel = FactionRelationKind.Ally;
                        }
                        a.SetRelationDirect(b, rel, true, null, null);
                        return OkBool(true);
                    }

                    case "job.make":
                    {
                        var def = DefDatabase<JobDef>.GetNamedSilentFail(Str(args, "def"));
                        if (def == null) return Err("bad job def");
                        Job job = JobMaker.MakeJob(def);
                        if (args.ContainsKey("target"))
                        {
                            var target = ObjectHandles.Get<object>(Int(args, "target"));
                            if (target is Thing tt) job.targetA = tt;
                            else if (target is LocalTargetInfo lti) job.targetA = lti;
                        }
                        return OkHandle(job);
                    }
                    case "job.start":
                    {
                        var p = PawnOf(args);
                        var job = ObjectHandles.Get<Job>(Int(args, "job"));
                        if (p?.jobs == null || job == null) return Err("bad pawn/job");
                        p.jobs.StartJob(job, JobCondition.InterruptForced);
                        return OkBool(true);
                    }
                    case "job.start_lua":
                    {
                        var p = PawnOf(args);
                        string name = Str(args, "name");
                        if (p?.jobs == null || string.IsNullOrEmpty(name)) return Err("bad pawn/name");
                        if (NativeAbi.rimlua_job_call(name, "can_do", ObjectHandles.GetOrAdd(p), out int can) != 0)
                        {
                            return Err("job can_do failed");
                        }
                        if (can == 0) return OkBool(false);
                        JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("RimLua_Scripted");
                        if (def == null) return Err("missing RimLua_Scripted JobDef");
                        Job job = JobMaker.MakeJob(def);
                        LuaJobBridge.Bind(job, name);
                        p.jobs.StartJob(job, JobCondition.InterruptForced);
                        return OkBool(true);
                    }

                    case "ui.message":
                    {
                        Messages.Message(Str(args, "text"), MessageTypeDefOf.NeutralEvent, false);
                        return OkBool(true);
                    }
                    case "ui.letter":
                    {
                        Find.LetterStack?.ReceiveLetter(Str(args, "label"), Str(args, "text"), LetterDefOf.NeutralEvent);
                        return OkBool(true);
                    }
                    case "ui.open_window":
                    {
                        LuaUiBridge.OpenWindow(Str(args, "title"), Str(args, "body"),
                            LuaUiBridge.ParseButtonBlob(Str(args, "buttons")));
                        return OkBool(true);
                    }
                    case "ui.float_menu":
                    {
                        LuaUiBridge.OpenFloatMenu(LuaUiBridge.ParseButtonBlob(Str(args, "buttons")));
                        return OkBool(true);
                    }

                    case "config.register":
                    {
                        LuaConfigBridge.Register(Str(args, "key"), Str(args, "type"), Str(args, "label"), Str(args, "default"));
                        return OkBool(true);
                    }
                    case "config.get": return OkStr(LuaConfigBridge.Get(Str(args, "key")));
                    case "config.set":
                    {
                        LuaConfigBridge.Set(Str(args, "key"), Str(args, "value"));
                        return OkBool(true);
                    }

                    // defs write (needs restart)
                    case "defs.write_thing":
                    {
                        string packageId = Str(args, "package_id");
                        if (string.IsNullOrEmpty(packageId)) packageId = "stratware.rimkit";
                        ModContentPack pack = LoadedModManager.RunningModsListForReading
                            .FirstOrDefault(m => m.PackageIdPlayerFacing == packageId || m.PackageId == packageId);
                        if (pack == null)
                        {
                            pack = LoadedModManager.RunningModsListForReading
                                .FirstOrDefault(m => string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
                        }
                        if (pack == null) return Err("mod not found: " + packageId);
                        string defName = Str(args, "defName");
                        if (!LuaSandbox.IsSafeDefName(defName)) return Err("sandbox: bad defName");
                        string label = Str(args, "label");
                        if (string.IsNullOrEmpty(label)) label = defName;
                        string desc = Str(args, "description");
                        if (string.IsNullOrEmpty(desc)) desc = label;
                        string tex = Str(args, "texPath");
                        if (string.IsNullOrEmpty(tex)) tex = "Things/Item/Resource/Steel";
                        // content path only, no traversal
                        if (tex.IndexOfAny(new[] { ':', '*', '?', '"', '<', '>', '|' }) >= 0 ||
                            tex.Contains(".."))
                        {
                            return Err("sandbox: bad texPath");
                        }
                        int stack = args.ContainsKey("stackLimit") ? Int(args, "stackLimit") : 75;
                        if (stack < 1) stack = 1;
                        if (stack > 9999) stack = 9999;
                        string dir = Path.Combine(pack.RootDir, "Defs", "ThingDefs");
                        string path = Path.Combine(dir, "RimLua_Generated_" + defName + ".xml");
                        if (!LuaSandbox.TryJailUnderRoot(pack.RootDir, path, out string fullPath, out string jailErr))
                        {
                            return Err(jailErr);
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? dir);
                        string xml =
                            "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n" +
                            "<Defs>\n" +
                            "  <!-- Generated by defs.register_thing. Restart RimWorld to load. -->\n" +
                            "  <ThingDef ParentName=\"ResourceBase\">\n" +
                            "    <defName>" + EscapeXml(defName) + "</defName>\n" +
                            "    <label>" + EscapeXml(label) + "</label>\n" +
                            "    <description>" + EscapeXml(desc) + "</description>\n" +
                            "    <graphicData>\n" +
                            "      <texPath>" + EscapeXml(tex) + "</texPath>\n" +
                            "      <graphicClass>Graphic_StackCount</graphicClass>\n" +
                            "    </graphicData>\n" +
                            "    <stackLimit>" + stack.ToString(CultureInfo.InvariantCulture) + "</stackLimit>\n" +
                            "    <statBases>\n" +
                            "      <MaxHitPoints>50</MaxHitPoints>\n" +
                            "      <MarketValue>1</MarketValue>\n" +
                            "      <Mass>0.05</Mass>\n" +
                            "    </statBases>\n" +
                            "    <thingCategories>\n" +
                            "      <li>ResourcesRaw</li>\n" +
                            "    </thingCategories>\n" +
                            "  </ThingDef>\n" +
                            "</Defs>\n";
                        File.WriteAllText(fullPath, xml);
                        Log.Message("[RimKit] Wrote ThingDef XML: " + fullPath + " (restart to load)");
                        return OkStr(fullPath);
                    }

                    case "input.binding_just_pressed":
                    {
                        var def = DefDatabase<KeyBindingDef>.GetNamedSilentFail(Str(args, "def"));
                        return OkBool(def != null && def.JustPressed);
                    }

                    case "harmony.has_patch":
                    {
                        Type t = AccessTools.TypeByName(Str(args, "type"));
                        MethodInfo m = t == null ? null : AccessTools.Method(t, Str(args, "method"));
                        return OkBool(m != null && Harmony.GetPatchInfo(m) != null);
                    }
                    case "harmony.type_exists": return OkBool(AccessTools.TypeByName(Str(args, "type")) != null);
                    case "harmony.method_exists":
                    {
                        Type t = AccessTools.TypeByName(Str(args, "type"));
                        return OkBool(t != null && AccessTools.Method(t, Str(args, "method")) != null);
                    }

                    // reflect (needs developer_reflect)
                    case "reflect_v1.type":
                    case "reflect_v1.get":
                    case "reflect_v1.set":
                    case "reflect_v1.call":
                    case "reflect_v1.static_get":
                    case "reflect_v1.static_call":
                    case "reflect_v1.members":
                    case "reflect_v1.handle_of_static":
                    {
                        if (!LuaSandbox.DeveloperReflectEnabled)
                        {
                            return Err("sandbox: reflect/cs disabled (enable RimKit setting developer_reflect)");
                        }
                        switch (op)
                        {
                            case "reflect_v1.type": return OkStr(ObjectHandles.Get<object>(Int(args, "h"))?.GetType().FullName ?? "");
                            case "reflect_v1.get": return ReflectGet(Int(args, "h"), Str(args, "member"));
                            case "reflect_v1.set": return ReflectSet(Int(args, "h"), Str(args, "member"), Str(args, "value"));
                            case "reflect_v1.call": return ReflectCall(Int(args, "h"), Str(args, "method"), Str(args, "args"));
                            case "reflect_v1.static_get": return ReflectStaticGet(Str(args, "type"), Str(args, "member"));
                            case "reflect_v1.static_call": return ReflectStaticCall(Str(args, "type"), Str(args, "method"), Str(args, "args"));
                            case "reflect_v1.members": return OkStringList(ListMembers(Int(args, "h")));
                            case "reflect_v1.handle_of_static": return ReflectHandleOfStatic(Str(args, "type"), Str(args, "member"));
                            default: return Err("unknown op: " + op);
                        }
                    }

                    default:
                        return Err("unknown op: " + op);
                }
            }
            catch (Exception e)
            {
                // The Lua error only has the message. The log gets the stack so the failing line can be found.
                try { Verse.Log.Warning("[RimKit] op " + op + " failed: " + e); } catch (Exception) { }
                return Err(e.GetType().Name + ": " + e.Message + " (op " + op + ")");
            }
        }

        private static string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static float? NeedPct(Pawn p, string needName)
        {
            if (p?.needs == null) return null;
            foreach (Need n in p.needs.AllNeeds)
            {
                if (n?.def?.defName == needName) return n.CurLevelPercentage;
            }
            return null;
        }

        private static SkillDef SkillDefOfNamed(string name)
        {
            return DefDatabase<SkillDef>.GetNamedSilentFail(name);
        }

        private static IEnumerable<Thing> EquipmentThings(Pawn p)
        {
            if (p?.equipment?.AllEquipmentListForReading == null) yield break;
            foreach (ThingWithComps t in p.equipment.AllEquipmentListForReading) yield return t;
        }

        private static IEnumerable<Thing> ApparelThings(Pawn p)
        {
            if (p?.apparel?.WornApparel == null) yield break;
            foreach (Apparel a in p.apparel.WornApparel) yield return a;
        }

        private static IEnumerable<Thing> InventoryThings(Pawn p)
        {
            if (p?.inventory?.innerContainer == null) yield break;
            foreach (Thing t in p.inventory.innerContainer) yield return t;
        }

        private static Def GetDef(string typeName, string defName)
        {
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(defName)) return null;
            Type t = AccessTools.TypeByName(typeName) ?? AccessTools.TypeByName("RimWorld." + typeName) ?? AccessTools.TypeByName("Verse." + typeName);
            if (t == null || !typeof(Def).IsAssignableFrom(t)) return null;
            MethodInfo getNamed = typeof(DefDatabase<>).MakeGenericType(t).GetMethod("GetNamedSilentFail", BindingFlags.Public | BindingFlags.Static);
            return getNamed?.Invoke(null, new object[] { defName }) as Def;
        }

        private static List<string> ListDefNames(string typeName)
        {
            var result = new List<string>();
            Type t = AccessTools.TypeByName(typeName) ?? AccessTools.TypeByName("RimWorld." + typeName) ?? AccessTools.TypeByName("Verse." + typeName);
            if (t == null || !typeof(Def).IsAssignableFrom(t)) return result;
            Type db = typeof(DefDatabase<>).MakeGenericType(t);
            PropertyInfo allDefs = db.GetProperty("AllDefs", BindingFlags.Public | BindingFlags.Static);
            if (allDefs?.GetValue(null) is System.Collections.IEnumerable en)
            {
                foreach (object o in en)
                {
                    if (o is Def d) result.Add(d.defName);
                }
            }
            return result;
        }

        private static string ReflectGet(int h, string member)
        {
            object obj = ObjectHandles.Get<object>(h);
            if (obj == null) return Err("null");
            string deny = LuaSandbox.DenyIfReflectBlocked(obj.GetType(), member, isCall: false);
            if (deny != null) return Err(deny);
            MemberInfo mi = FindMember(obj.GetType(), member);
            if (mi is PropertyInfo pi) return EncodeValue(pi.GetValue(obj, null));
            if (mi is FieldInfo fi) return EncodeValue(fi.GetValue(obj));
            return Err("member not found");
        }

        private static string ReflectSet(int h, string member, string value)
        {
            object obj = ObjectHandles.Get<object>(h);
            if (obj == null) return Err("null");
            string deny = LuaSandbox.DenyIfReflectBlocked(obj.GetType(), member, isCall: false);
            if (deny != null) return Err(deny);
            MemberInfo mi = FindMember(obj.GetType(), member);
            if (mi is PropertyInfo pi)
            {
                pi.SetValue(obj, Coerce(value, pi.PropertyType), null);
                return OkBool(true);
            }
            if (mi is FieldInfo fi)
            {
                fi.SetValue(obj, Coerce(value, fi.FieldType));
                return OkBool(true);
            }
            return Err("member not found");
        }

        private static string ReflectCall(int h, string method, string argsCsv)
        {
            object obj = ObjectHandles.Get<object>(h);
            if (obj == null) return Err("null");
            string deny = LuaSandbox.DenyIfReflectBlocked(obj.GetType(), method, isCall: true);
            if (deny != null) return Err(deny);
            return InvokeMethod(obj.GetType(), obj, method, argsCsv);
        }

        private static string ReflectStaticGet(string typeName, string member)
        {
            string denyName = LuaSandbox.DenyIfStaticTypeBlocked(typeName);
            if (denyName != null) return Err(denyName);
            Type t = AccessTools.TypeByName(typeName);
            if (t == null) return Err("type not found");
            if (!LuaSandbox.IsGameplayNamespace(t.Namespace)) return Err("sandbox: static get outside gameplay types");
            string deny = LuaSandbox.DenyIfReflectBlocked(t, member, isCall: false);
            if (deny != null) return Err(deny);
            MemberInfo mi = FindMember(t, member, staticOnly: true);
            if (mi is PropertyInfo pi) return EncodeValue(pi.GetValue(null, null));
            if (mi is FieldInfo fi) return EncodeValue(fi.GetValue(null));
            return Err("member not found");
        }

        private static string ReflectStaticCall(string typeName, string method, string argsCsv)
        {
            string denyName = LuaSandbox.DenyIfStaticTypeBlocked(typeName);
            if (denyName != null) return Err(denyName);
            Type t = AccessTools.TypeByName(typeName);
            if (t == null) return Err("type not found");
            if (!LuaSandbox.IsGameplayNamespace(t.Namespace)) return Err("sandbox: static call outside gameplay types");
            string deny = LuaSandbox.DenyIfReflectBlocked(t, method, isCall: true);
            if (deny != null) return Err(deny);
            return InvokeMethod(t, null, method, argsCsv);
        }

        private static string ReflectHandleOfStatic(string typeName, string member)
        {
            string denyName = LuaSandbox.DenyIfStaticTypeBlocked(typeName);
            if (denyName != null) return Err(denyName);
            Type t = AccessTools.TypeByName(typeName);
            if (t == null) return Err("type not found");
            if (!LuaSandbox.IsGameplayNamespace(t.Namespace)) return Err("sandbox: static handle outside gameplay types");
            string deny = LuaSandbox.DenyIfReflectBlocked(t, member, isCall: false);
            if (deny != null) return Err(deny);
            MemberInfo mi = FindMember(t, member, staticOnly: true);
            object val = null;
            if (mi is PropertyInfo pi) val = pi.GetValue(null, null);
            else if (mi is FieldInfo fi) val = fi.GetValue(null);
            else return Err("member not found");
            return OkHandle(val);
        }

        private static string InvokeMethod(Type type, object target, string method, string argsCsv)
        {
            string deny = LuaSandbox.DenyIfReflectBlocked(type, method, isCall: true);
            if (deny != null) return Err(deny);
            string[] parts = string.IsNullOrEmpty(argsCsv) ? Array.Empty<string>() : argsCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            object[] callArgs = new object[parts.Length];
            Type[] argTypes = new Type[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.StartsWith("#"))
                {
                    object o = ObjectHandles.Get<object>(int.Parse(p.Substring(1), CultureInfo.InvariantCulture));
                    callArgs[i] = o;
                    argTypes[i] = o?.GetType() ?? typeof(object);
                }
                else if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
                {
                    callArgs[i] = iv;
                    argTypes[i] = typeof(int);
                }
                else if (float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out float fv))
                {
                    callArgs[i] = fv;
                    argTypes[i] = typeof(float);
                }
                else if (p == "true" || p == "false")
                {
                    callArgs[i] = p == "true";
                    argTypes[i] = typeof(bool);
                }
                else
                {
                    callArgs[i] = p.Trim('"');
                    argTypes[i] = typeof(string);
                }
            }

            MethodInfo mi = AccessTools.Method(type, method, argTypes) ?? AccessTools.Method(type, method);
            if (mi == null) return Err("method not found");
            object result = mi.Invoke(target, callArgs);
            return EncodeValue(result);
        }

        private static MemberInfo FindMember(Type type, string name, bool staticOnly = false)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (staticOnly ? BindingFlags.Static : BindingFlags.Instance | BindingFlags.Static);
            return (MemberInfo)type.GetProperty(name, flags) ?? type.GetField(name, flags);
        }

        private static List<string> ListMembers(int h)
        {
            var list = new List<string>();
            object obj = ObjectHandles.Get<object>(h);
            if (obj == null) return list;
            if (LuaSandbox.DenyIfReflectBlocked(obj.GetType(), null, isCall: false) != null) return list;
            Type t = obj.GetType();
            foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)) list.Add("P:" + pi.Name);
            foreach (FieldInfo fi in t.GetFields(BindingFlags.Public | BindingFlags.Instance)) list.Add("F:" + fi.Name);
            foreach (MethodInfo mi in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!mi.IsSpecialName) list.Add("M:" + mi.Name);
            }
            return list;
        }

        private static object Coerce(string value, Type target)
        {
            if (target == typeof(string)) return value;
            if (target == typeof(bool)) return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (target == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
            if (target == typeof(float)) return float.Parse(value, CultureInfo.InvariantCulture);
            if (target == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
            if (typeof(Def).IsAssignableFrom(target))
            {
                MethodInfo getNamed = typeof(DefDatabase<>).MakeGenericType(target).GetMethod("GetNamedSilentFail", BindingFlags.Public | BindingFlags.Static);
                return getNamed?.Invoke(null, new object[] { value });
            }
            if (!target.IsValueType && int.TryParse(value, out int handle))
            {
                return ObjectHandles.Get<object>(handle);
            }
            return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }

        private static string EncodeValue(object val)
        {
            if (val == null) return OkStr("");
            if (val is bool b) return OkBool(b);
            if (val is int i) return OkInt(i);
            if (val is long l) return OkInt((int)l);
            if (val is float f) return OkFloat(f);
            if (val is double d) return OkFloat((float)d);
            if (val is string s) return OkStr(s);
            if (val is Def def) return OkStr(def.defName);
            if (val.GetType().IsEnum) return OkStr(val.ToString());
            return OkHandle(val);
        }

        private static Map MapOf(Dictionary<string, string> args) => ObjectHandles.Get<Map>(Int(args, "h"));
        private static Thing ThingOf(Dictionary<string, string> args) => ObjectHandles.Get<Thing>(Int(args, "h"));
        private static Pawn PawnOf(Dictionary<string, string> args) => ObjectHandles.Get<Pawn>(Int(args, "h"));

        private static ThingDef ResolveThingDef(string primary, params string[] fallbacks)
        {
            if (!string.IsNullOrEmpty(primary))
            {
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(primary);
                if (d != null) return d;
                if (primary.Equals("Component", StringComparison.OrdinalIgnoreCase))
                {
                    d = DefDatabase<ThingDef>.GetNamedSilentFail("ComponentIndustrial");
                    if (d != null) return d;
                }
            }
            if (fallbacks != null)
            {
                foreach (string name in fallbacks)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (d != null) return d;
                }
            }
            return null;
        }

        private static string Str(Dictionary<string, string> a, string k) => a.TryGetValue(k, out string v) ? v : "";
        private static int Int(Dictionary<string, string> a, string k) => int.TryParse(Str(a, k), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        private static float Float(Dictionary<string, string> a, string k) => float.TryParse(Str(a, k), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        private static bool Bool(Dictionary<string, string> a, string k)
        {
            string s = Str(a, k);
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static string OkStr(string s) => "{\"ok\":true,\"t\":\"s\",\"v\":" + JsonLite.Quote(s) + "}";
        private static string OkInt(int v) => "{\"ok\":true,\"t\":\"i\",\"v\":" + v.ToString(CultureInfo.InvariantCulture) + "}";
        private static string OkFloat(float v) => "{\"ok\":true,\"t\":\"f\",\"v\":" + v.ToString(CultureInfo.InvariantCulture) + "}";
        private static string OkBool(bool v) => "{\"ok\":true,\"t\":\"b\",\"v\":" + (v ? "true" : "false") + "}";
        private static string OkHandle(object o) => "{\"ok\":true,\"t\":\"h\",\"v\":" + ObjectHandles.GetOrAdd(o).ToString(CultureInfo.InvariantCulture) + "}";
        private static string OkHandles(System.Collections.IEnumerable list)
        {
            var sb = new StringBuilder("{\"ok\":true,\"t\":\"a\",\"v\":[");
            bool first = true;
            if (list != null)
            {
                foreach (object o in list)
                {
                    if (o == null) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(ObjectHandles.GetOrAdd(o).ToString(CultureInfo.InvariantCulture));
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string OkStringList(List<string> list)
        {
            var sb = new StringBuilder("{\"ok\":true,\"t\":\"sa\",\"v\":[");
            bool first = true;
            if (list != null)
            {
                foreach (string s in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(JsonLite.Quote(s));
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string Err(string msg) => "{\"ok\":false,\"e\":" + JsonLite.Quote(msg) + "}";
    }
}
