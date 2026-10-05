using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimKit
{
    // Debug: right-click walks the controlled pawn.
    internal static class ControlBridge
    {
        public static int ControlledHandle;
        public static bool ClickToWalk;

        public static Pawn ControlledPawn => ObjectHandles.Get<Pawn>(ControlledHandle);

        public static void Set(Pawn pawn, bool clickToWalk)
        {
            ControlledHandle = pawn == null ? 0 : ObjectHandles.GetOrAdd(pawn);
            ClickToWalk = clickToWalk && pawn != null;
            if (pawn?.drafter != null)
            {
                pawn.drafter.Drafted = true;
            }
        }

        public static void Clear()
        {
            ControlledHandle = 0;
            ClickToWalk = false;
        }
    }

    [HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
    internal static class ControlBridge_ClickToWalk
    {
        public static void Postfix()
        {
            if (!ControlBridge.ClickToWalk)
            {
                return;
            }

            if (Event.current.type != EventType.MouseDown || Event.current.button != 1)
            {
                return;
            }

            Pawn p = ControlBridge.ControlledPawn;
            if (p == null || p.Dead || p.Map == null || p.jobs == null)
            {
                return;
            }

            if (Find.WindowStack != null && Find.WindowStack.Count > 0)
            {
                Window top = Find.WindowStack[Find.WindowStack.Count - 1];
                if (top != null && top.IsOpen)
                {
                    if (top is FloatMenu || top.absorbInputAroundWindow)
                    {
                        return;
                    }
                    if (top is RimLuaDebugWindow && Mouse.IsOver(top.windowRect))
                    {
                        return;
                    }
                }
            }

            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(p.Map) || !p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
            {
                return;
            }

            try
            {
                if (p.drafter != null)
                {
                    p.drafter.Drafted = true;
                }
                Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                job.locomotionUrgency = LocomotionUrgency.Sprint;
                if (!p.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder))
                {
                    return;
                }
                Event.current.Use();
                Messages.Message("[RimKit] " + p.LabelShort + " go " + cell, MessageTypeDefOf.SilentInput, false);
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] click-to-walk failed: " + e);
            }
        }
    }
}
