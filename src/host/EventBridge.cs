using System;
using System.Collections.Concurrent;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimKit
{
    // SpawnSetup can run off the main thread; queue and drain later.
    internal static class LuaEventQueue
    {
        private const int DrainBudgetPerFrame = 256;
        private const int MaxPending = 8192;

        // payload is null for legacy single handle events.
        private static readonly ConcurrentQueue<(string name, int handle, string payload)> Pending =
            new ConcurrentQueue<(string, int, string)>();

        private static int pendingCount;
        private static long dropped;

        /// <summary>Events dropped because the queue was full. Exposed by events.stats.</summary>
        public static long Dropped => System.Threading.Interlocked.Read(ref dropped);

        public static int PendingCount => System.Threading.Volatile.Read(ref pendingCount);

        public static void Enqueue(string name, int handle)
        {
            if (string.IsNullOrEmpty(name) || handle == 0)
            {
                return;
            }

            Push((name, handle, null));
        }

        public static void EnqueuePayload(string name, string payloadJson)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            Push((name, 0, payloadJson ?? "{}"));
        }

        private static void Push((string name, int handle, string payload) item)
        {
            if (System.Threading.Volatile.Read(ref pendingCount) >= MaxPending)
            {
                System.Threading.Interlocked.Increment(ref dropped);
                return;
            }

            System.Threading.Interlocked.Increment(ref pendingCount);
            Pending.Enqueue(item);
        }

        public static void Drain()
        {
            int n = 0;
            while (n < DrainBudgetPerFrame && Pending.TryDequeue(out var ev))
            {
                n++;
                System.Threading.Interlocked.Decrement(ref pendingCount);
                try
                {
                    if (ev.payload != null)
                    {
                        NativeAbi.EmitEventEx(ev.name, ev.payload);
                    }
                    else
                    {
                        NativeAbi.rimlua_emit_event(ev.name, ev.handle);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("[RimKit] event drain " + ev.name + " failed: " + e);
                }
            }
        }
    }

}
