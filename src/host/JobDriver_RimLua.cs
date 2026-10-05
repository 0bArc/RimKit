using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimKit
{
    // Job handle -> Lua job name.
    internal static class LuaJobBridge
    {
        private static readonly Dictionary<int, string> NamesByJobHandle = new Dictionary<int, string>();

        public static void Bind(Job job, string name)
        {
            if (job == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            NamesByJobHandle[ObjectHandles.GetOrAdd(job)] = name;
        }

        public static string GetName(Job job)
        {
            if (job == null)
            {
                return null;
            }

            int h = ObjectHandles.GetOrAdd(job);
            return NamesByJobHandle.TryGetValue(h, out string name) ? name : null;
        }

        public static bool HasLua(string name)
        {
            return !string.IsNullOrEmpty(name);
        }
    }

    public class JobDriver_RimLua : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil work = ToilMaker.MakeToil("RimLuaJob");
            work.tickAction = () =>
            {
                string name = LuaJobBridge.GetName(job);
                if (string.IsNullOrEmpty(name))
                {
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                try
                {
                    if (NativeAbi.rimlua_job_call(name, "execute", ObjectHandles.GetOrAdd(pawn), out int done) != 0)
                    {
                        EndJobWith(JobCondition.Errored);
                        return;
                    }

                    if (done != 0)
                    {
                        EndJobWith(JobCondition.Succeeded);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("[RimKit] JobDriver_RimLua tick failed: " + e);
                    EndJobWith(JobCondition.Errored);
                }
            };
            work.defaultCompleteMode = ToilCompleteMode.Never;
            yield return work;
        }
    }
}
