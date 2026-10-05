using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.jobs kit: give any vanilla job with targets, queue, interrupt, reservations, helpers. Ops are job.*.
    // The older game.jobs.make, start, register and start_job stay in GameApi.
    internal static class ApiJobs
    {
        public static void Register()
        {
            R("job.give", Give);
            R("job.current", Current);
            R("job.queue_list", QueueList);
            R("job.interrupt", Interrupt);
            R("job.clear_queue", ClearQueue);
            R("job.can_reserve", CanReserve);
            R("job.release_reservations", ReleaseReservations);
            R("job.haul", Haul);
            R("job.tend", Tend);
            R("job.rescue", Rescue);
            R("job.repair", Repair);
            R("job.clean", Clean);
            R("job.wait", WaitJob);
            R("job.go_to", GoTo);
            R("job.defs", Defs);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.6.0");

        private static string Bad() => Fail("RK2001", "pawn handle is stale or null");

        private static LocalTargetInfo Target(Opts o, string thingKey, string xKey, string zKey, Map map)
        {
            Thing t = o.Handle<Thing>(thingKey);
            if (t != null) return t;
            if (o.Has(xKey) && o.Has(zKey))
            {
                var c = new IntVec3(o.Int(xKey), 0, o.Int(zKey));
                if (map != null && c.InBounds(map)) return c;
            }

            return LocalTargetInfo.Invalid;
        }

        private static Pawn Worker(Dictionary<string, string> a, out string err)
        {
            Pawn p = PawnOf(a);
            err = null;
            if (p == null) { err = Bad(); return null; }
            if (p.jobs == null || p.Dead || !p.Spawned) { err = Fail("RK3001", "pawn cannot take jobs right now"); return null; }
            return p;
        }

        private static string Start(Pawn p, Job job, bool queue)
        {
            if (job == null) return Fail("RK3001", "no job could be made");
            bool ok = p.jobs.TryTakeOrderedJob(job, JobTag.Misc, queue);
            return OkBool(ok);
        }

        // opts: target (thing), x, z (cell), target2 (thing), x2, z2, count, queue (add to the end of the queue instead of replacing the job).
        private static string Give(Dictionary<string, string> a)
        {
            Pawn p = Worker(a, out string err);
            if (err != null) return err;
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown job def " + Str(a, "def"));
            Opts o = Opts.From(a, "opts");
            Job job = JobMaker.MakeJob(def, Target(o, "target", "x", "z", p.Map), Target(o, "target2", "x2", "z2", p.Map));
            if (o.Has("count")) job.count = o.Int("count");
            return Start(p, job, o.Bool("queue"));
        }

        private static Jb JobJson(Job j, Pawn p)
        {
            var o = Jb.Obj().S("def", j.def.defName).B("forced", j.playerForced).I("count", j.count);
            if (j.targetA.HasThing) o.H("target", j.targetA.Thing); else if (j.targetA.IsValid) o.I("x", j.targetA.Cell.x).I("z", j.targetA.Cell.z);
            if (j.targetB.HasThing) o.H("target2", j.targetB.Thing);
            return o;
        }

        private static string Current(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            Job j = p.CurJob;
            if (j == null) return OkJson("null");
            return JobJson(j, p).S("driver", p.jobs.curDriver?.GetType().Name).Ok();
        }

        private static string QueueList(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.jobs == null) return Bad();
            var arr = Jb.Arr();
            foreach (QueuedJob q in p.jobs.jobQueue) arr.Add(JobJson(q.job, p));
            return arr.Ok();
        }

        private static string Interrupt(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.jobs == null) return Bad();
            p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            return OkBool(true);
        }

        private static string ClearQueue(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.jobs == null) return Bad();
            p.jobs.ClearQueuedJobs();
            return OkBool(true);
        }

        private static string CanReserve(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            Thing t = ObjectHandles.Get<Thing>(Int(a, "thing"));
            if (t == null || !t.Spawned) return Fail("RK2001", "thing handle is stale or not spawned");
            return OkBool(p.CanReserve(t));
        }

        private static string ReleaseReservations(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.Map == null) return Bad();
            p.Map.reservationManager.ReleaseAllClaimedBy(p);
            return OkBool(true);
        }

        private static string Haul(Dictionary<string, string> a)
        {
            Pawn p = Worker(a, out string err);
            if (err != null) return err;
            Thing t = ObjectHandles.Get<Thing>(Int(a, "thing"));
            if (t == null || !t.Spawned) return Fail("RK2001", "thing handle is stale or not spawned");
            Job job = HaulAIUtility.HaulToStorageJob(p, t, false);
            if (job == null) return OkBool(false);
            return Start(p, job, Bool(a, "queue"));
        }

        private static string Simple(Dictionary<string, string> a, JobDef def, string targetKey, bool needsPawnTarget)
        {
            Pawn p = Worker(a, out string err);
            if (err != null) return err;
            Thing t = ObjectHandles.Get<Thing>(Int(a, targetKey));
            if (t == null || !t.Spawned) return Fail("RK2001", targetKey + " handle is stale or not spawned");
            if (needsPawnTarget && !(t is Pawn)) return Fail("RK1001", targetKey + " must be a pawn");
            return Start(p, JobMaker.MakeJob(def, t), Bool(a, "queue"));
        }

        private static string Tend(Dictionary<string, string> a) => Simple(a, JobDefOf.TendPatient, "patient", true);

        private static string Rescue(Dictionary<string, string> a) => Simple(a, JobDefOf.Rescue, "patient", true);

        private static string Repair(Dictionary<string, string> a) => Simple(a, JobDefOf.Repair, "building", false);

        private static string Clean(Dictionary<string, string> a) => Simple(a, JobDefOf.Clean, "filth", false);

        private static string WaitJob(Dictionary<string, string> a)
        {
            Pawn p = Worker(a, out string err);
            if (err != null) return err;
            Job job = JobMaker.MakeJob(JobDefOf.Wait);
            job.expiryInterval = Math.Max(1, Int(a, "ticks"));
            return Start(p, job, Bool(a, "queue"));
        }

        private static string GoTo(Dictionary<string, string> a)
        {
            Pawn p = Worker(a, out string err);
            if (err != null) return err;
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(p.Map)) return Fail("RK1001", "cell is outside the map");
            return Start(p, JobMaker.MakeJob(JobDefOf.Goto, c), Bool(a, "queue"));
        }

        private static string Defs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (JobDef d in DefDatabase<JobDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("report", d.reportString).B("casual_interruptible", d.casualInterruptible));
            return arr.Ok();
        }
    }
}
