using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using static RimLuaKit.ApiHelpers;

namespace RimLuaKit
{
    // Anomaly ops for Lua (menus are authored in Lua).
    internal static class ApiAnomaly
    {
        public static void Register()
        {
            ApiRegistry.Register("anomaly.dlc_active", DlcActive, "gameplay");
            ApiRegistry.Register("anomaly.is_entity", IsEntity, "gameplay");
            ApiRegistry.Register("anomaly.list_on_map", ListOnMap, "gameplay");
            ApiRegistry.Register("anomaly.try_set_faction_player", TrySetFactionPlayer, "gameplay");
            ApiRegistry.Register("anomaly.release_to_hostile", ReleaseToHostile, "gameplay");
            ApiRegistry.Register("anomaly.knock_out", KnockOut, "gameplay");
            ApiRegistry.Register("anomaly.find_platform", FindPlatform, "gameplay");
            ApiRegistry.Register("anomaly.start_capture", StartCapture, "gameplay");
            ApiRegistry.Register("anomaly.recruit", Recruit, "gameplay");
        }

        private static bool AnomalyActive()
        {
            try
            {
                return ModsConfig.AnomalyActive;
            }
            catch
            {
                return false;
            }
        }

        private static string DlcActive(Dictionary<string, string> args) => OkBool(AnomalyActive());

        internal static bool LooksLikeEntity(Pawn p)
        {
            if (p?.def == null) return false;
            try
            {
                if (p.RaceProps != null && p.RaceProps.IsAnomalyEntity) return true;
            }
            catch
            {
            }

            string d = p.def.defName ?? "";
            string k = p.kindDef?.defName ?? "";
            if (d.IndexOf("Entity", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (k.IndexOf("Entity", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Shambler", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Ghoul", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Sightstealer", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Gorehulk", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Noctol", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Toughspike", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Trispike", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Fingerspike", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Bulbfreak", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Devourer", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (d.IndexOf("Chimera", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (p is ThingWithComps twc && twc.AllComps != null)
            {
                foreach (ThingComp c in twc.AllComps)
                {
                    string n = c?.GetType().Name ?? "";
                    if (n.IndexOf("Entity", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (n.IndexOf("HoldingPlatform", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (n.IndexOf("Mutant", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            return false;
        }

        private static string IsEntity(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            return OkBool(p != null && LooksLikeEntity(p));
        }

        private static string TrySetFactionPlayer(Dictionary<string, string> args)
        {
            if (!AnomalyActive()) return Err("Anomaly DLC not active");
            Pawn p = PawnOf(args);
            if (p == null) return Err("no pawn");
            PawnControl.MakeControllable(p);
            return OkBool(p.Faction == Faction.OfPlayer && p.drafter != null);
        }

        private static string Recruit(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            if (p == null) return Err("no pawn");
            PawnControl.MakeControllable(p);
            return OkBool(PawnControl.IsControlled(p));
        }

        private static string ReleaseToHostile(Dictionary<string, string> args)
        {
            if (!AnomalyActive()) return Err("Anomaly DLC not active");
            Pawn p = PawnOf(args);
            if (p == null) return Err("no pawn");
            PawnControl.ReleaseControl(p);
            Faction hostile = Find.FactionManager?.RandomEnemyFaction(allowHidden: true, allowDefeated: false, allowNonHumanlike: true)
                              ?? Faction.OfAncientsHostile;
            if (hostile == null) return Err("no hostile faction");
            p.SetFaction(hostile);
            return OkBool(true);
        }

        private static string KnockOut(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            if (p?.health == null) return Err("no pawn");
            HediffDef anes = DefDatabase<HediffDef>.GetNamedSilentFail("Anesthetic");
            if (anes == null) return Err("Anesthetic def missing");
            Hediff h = HediffMaker.MakeHediff(anes, p);
            h.Severity = args.ContainsKey("severity") ? Float(args, "severity") : 1f;
            p.health.AddHediff(h);
            return OkBool(p.Downed || true);
        }

        private static string FindPlatform(Dictionary<string, string> args)
        {
            Pawn hauler = PawnOf(args);
            Map map = MapOf(args) ?? hauler?.Map ?? Find.CurrentMap;
            if (map == null) return Err("no map");
            Pawn entity = null;
            if (args.ContainsKey("entity"))
            {
                entity = ObjectHandles.Get<Pawn>(Int(args, "entity"));
            }

            Building_HoldingPlatform plat = LuaFloatMenuBridge.FindFreePlatform(map, hauler, entity);
            if (plat == null) return OkInt(0);
            return OkHandle(plat);
        }

        private static string StartCapture(Dictionary<string, string> args)
        {
            Pawn hauler = PawnOf(args);
            Pawn entity = ObjectHandles.Get<Pawn>(Int(args, "entity"));
            if (hauler == null || entity == null) return Err("need hauler h= and entity=");
            Building_HoldingPlatform plat = null;
            if (args.ContainsKey("platform"))
            {
                plat = ObjectHandles.Get<Building_HoldingPlatform>(Int(args, "platform"));
            }

            plat ??= LuaFloatMenuBridge.FindFreePlatform(hauler.Map, hauler, entity);
            if (plat == null) return Err("no holding platform");
            return OkBool(LuaFloatMenuBridge.TryStartCaptureJob(hauler, entity, plat));
        }

        private static string ListOnMap(Dictionary<string, string> args)
        {
            Map map = MapOf(args) ?? Find.CurrentMap;
            var list = new List<object>();
            if (map?.mapPawns?.AllPawns != null)
            {
                foreach (Pawn p in map.mapPawns.AllPawns)
                {
                    if (p != null && LooksLikeEntity(p)) list.Add(p);
                }
            }
            return OkHandles(list);
        }
    }

    // Collects options from Lua ui.on_map_float_menu.
    internal static class LuaFloatMenuBridge
    {
        public static Building_HoldingPlatform FindFreePlatform(Map map, Pawn hauler, Pawn entity)
        {
            if (map?.listerBuildings?.allBuildingsColonist == null)
            {
                return null;
            }

            Building_HoldingPlatform best = null;
            float bestDist = float.MaxValue;
            IntVec3 from = entity?.Position ?? hauler?.Position ?? IntVec3.Zero;
            Pawn reserver = hauler;

            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (!(buildings[i] is Building_HoldingPlatform platform))
                {
                    continue;
                }

                if (platform.Destroyed || !platform.Spawned || platform.Occupied)
                {
                    continue;
                }

                if (reserver != null && !reserver.CanReserveAndReach(platform, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    continue;
                }

                float d = platform.Position.DistanceToSquared(from);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = platform;
                }
            }

            return best;
        }

        public static bool TryStartCaptureJob(Pawn hauler, Pawn entity, Building_HoldingPlatform platform)
        {
            if (hauler?.jobs == null || entity == null || platform == null)
            {
                return false;
            }

            CompHoldingPlatformTarget hold = entity.TryGetComp<CompHoldingPlatformTarget>();
            if (hold != null)
            {
                hold.targetHolder = platform;
            }

            Job job = JobMaker.MakeJob(JobDefOf.CarryToEntityHolder, platform, entity);
            job.playerForced = true;
            return hauler.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public static void AppendLuaOptions(FloatMenuContext context, List<FloatMenuOption> options)
        {
            if (options == null || context == null)
            {
                return;
            }

            // Prefer a human colonist as the menu actor.
            Pawn hauler = null;
            if (context.allSelectedPawns != null)
            {
                for (int i = 0; i < context.allSelectedPawns.Count; i++)
                {
                    Pawn p = context.allSelectedPawns[i];
                    if (p == null || p.Dead || !p.Spawned)
                    {
                        continue;
                    }

                    if (p.RaceProps != null && p.RaceProps.Humanlike && !ApiAnomaly.LooksLikeEntity(p))
                    {
                        hauler = p;
                        break;
                    }
                }
            }

            if (hauler == null)
            {
                return;
            }

            List<Pawn> clicked = context.ClickedPawns;
            if (clicked == null || clicked.Count == 0)
            {
                return;
            }

            int haulerH = ObjectHandles.GetOrAdd(hauler);
            for (int i = 0; i < clicked.Count; i++)
            {
                Pawn target = clicked[i];
                if (target == null || target.Dead)
                {
                    continue;
                }

                int clickedH = ObjectHandles.GetOrAdd(target);
                string blob = null;
                try
                {
                    IntPtr p = NativeAbi.rimlua_collect_map_float_menu(clickedH, haulerH);
                    if (p != IntPtr.Zero)
                    {
                        blob = Marshal.PtrToStringAnsi(p);
                    }
                }
                catch (Exception e)
                {
                    Log.Warning("[RimKit] collect_map_float_menu: " + e.Message);
                    continue;
                }

                if (string.IsNullOrEmpty(blob))
                {
                    continue;
                }

                        foreach (var (label, id) in LuaUiBridge.ParseButtonBlob(blob))
                {
                    if (id == 0)
                    {
                        options.Add(new FloatMenuOption(label, null));
                    }
                    else
                    {
                        int captured = id;
                        options.Add(new FloatMenuOption(label, () =>
                        {
                            try
                            {
                                NativeAbi.rimlua_ui_invoke(captured);
                            }
                            catch (Exception e)
                            {
                                Log.Error("[RimKit] lua float option failed: " + e);
                            }
                        }));
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "GetProviderOptions")]
    internal static class Patch_LuaFloatMenu_GetProviderOptions
    {
        public static void Postfix(FloatMenuContext context, List<FloatMenuOption> options)
        {
            try
            {
                LuaFloatMenuBridge.AppendLuaOptions(context, options);
            }
            catch (Exception e)
            {
                Log.Warning("[RimKit] lua float menu: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(CompHoldingPlatformTarget), "get_CanBeCaptured")]
    internal static class Patch_AnomalyCapture_CanBeCaptured
    {
        public static void Postfix(CompHoldingPlatformTarget __instance, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            Pawn p = __instance?.parent as Pawn;
            if (p == null || p.Dead || !p.Downed || p.Faction == Faction.OfPlayer)
            {
                return;
            }

            if (ApiAnomaly.LooksLikeEntity(p) || p.RaceProps?.IsAnomalyEntity == true)
            {
                __result = true;
            }
        }
    }
}
