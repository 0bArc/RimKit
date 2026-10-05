using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace RimKit
{
    /// <summary>
    /// Integer handles for game objects, so Lua never holds the object itself.
    ///
    /// Lifetime rules:
    /// - Pawns, things, maps and factions are owned by the game. Their handles are weak: when the game drops the
    ///   object (a destroyed thing, a closed map) the handle stops resolving and the call fails with a stale handle
    ///   error instead of keeping the object alive.
    /// - Anything else (a job, a hediff, a list, a boxed struct) may have no other owner, so its handle pins it.
    ///   Pinned objects are released by <see cref="Release"/> or when a game is loaded or started.
    /// - Ids are never reused, so a stale handle can never resolve to a different object.
    /// </summary>
    internal static class ObjectHandles
    {
        private sealed class Box
        {
            public int Id;
        }

        private static readonly object Gate = new object();
        private static ConditionalWeakTable<object, Box> weakIds = new ConditionalWeakTable<object, Box>();
        private static readonly Dictionary<int, WeakReference> WeakTargets = new Dictionary<int, WeakReference>();
        private static readonly Dictionary<object, int> PinnedIds = new Dictionary<object, int>(ReferenceComparer.Instance);
        private static readonly Dictionary<int, object> PinnedTargets = new Dictionary<int, object>();
        private static int nextId = 1;
        private static int sincePurge;

        /// <summary>
        /// While true on the current thread, a handle issued for a game-owned object also pins it. Typed reflection sets
        /// this around its results, because an object that Lua just created (ThingMaker.MakeThing) has no other owner yet.
        /// </summary>
        [ThreadStatic]
        public static bool PinNewHandles;

        /// <summary>Raised when all handles are dropped (a game was loaded or started).</summary>
        public static int Generation { get; private set; } = 1;

        /// <summary>Handles that currently resolve. For diagnostics and tests.</summary>
        public static int LiveCount
        {
            get
            {
                lock (Gate)
                {
                    int n = PinnedTargets.Count;
                    foreach (WeakReference w in WeakTargets.Values)
                    {
                        if (w.IsAlive) n++;
                    }

                    return n;
                }
            }
        }

        public static int GetOrAdd(object obj)
        {
            if (obj == null)
            {
                return 0;
            }

            lock (Gate)
            {
                if (IsWeakKind(obj))
                {
                    if (weakIds.TryGetValue(obj, out Box existing))
                    {
                        if (PinNewHandles && !PinnedTargets.ContainsKey(existing.Id))
                        {
                            PinnedTargets[existing.Id] = obj;
                            PinnedIds[obj] = existing.Id;
                        }

                        return existing.Id;
                    }

                    int id = nextId++;
                    weakIds.Add(obj, new Box { Id = id });
                    WeakTargets[id] = new WeakReference(obj);
                    if (PinNewHandles)
                    {
                        PinnedTargets[id] = obj;
                        PinnedIds[obj] = id;
                    }

                    if (++sincePurge >= 4096)
                    {
                        sincePurge = 0;
                        PurgeDead();
                    }

                    return id;
                }

                if (PinnedIds.TryGetValue(obj, out int pinned))
                {
                    return pinned;
                }

                int newId = nextId++;
                PinnedIds[obj] = newId;
                PinnedTargets[newId] = obj;
                return newId;
            }
        }

        public static T Get<T>(int handle) where T : class
        {
            if (handle == 0)
            {
                return null;
            }

            lock (Gate)
            {
                if (PinnedTargets.TryGetValue(handle, out object pinned))
                {
                    return pinned as T;
                }

                if (WeakTargets.TryGetValue(handle, out WeakReference weak))
                {
                    return weak.Target as T;
                }

                return null;
            }
        }

        /// <summary>True when the handle was issued and its object is gone. Distinguishes stale from never issued.</summary>
        public static bool IsStale(int handle)
        {
            lock (Gate)
            {
                if (PinnedTargets.ContainsKey(handle))
                {
                    return false;
                }

                return WeakTargets.TryGetValue(handle, out WeakReference weak) ? !weak.IsAlive : handle > 0 && handle < nextId;
            }
        }

        /// <summary>Drops one object so its handle stops resolving (for example a pinned job, or a removed map).</summary>
        public static void Release(object obj)
        {
            if (obj == null)
            {
                return;
            }

            lock (Gate)
            {
                if (weakIds.TryGetValue(obj, out Box box))
                {
                    WeakTargets.Remove(box.Id);
                    weakIds.Remove(obj);
                }

                if (PinnedIds.TryGetValue(obj, out int id))
                {
                    PinnedIds.Remove(obj);
                    PinnedTargets.Remove(id);
                }
            }
        }

        // Older name, kept for the callers that predate weak handles.
        public static void Remove(object obj) => Release(obj);

        /// <summary>Drops all handles. Ids are not reused, so stale Lua handles resolve to nothing.</summary>
        public static void Clear()
        {
            lock (Gate)
            {
                weakIds = new ConditionalWeakTable<object, Box>();
                WeakTargets.Clear();
                PinnedIds.Clear();
                PinnedTargets.Clear();
                Generation++;
            }
        }

        // Objects the game owns and keeps in its own lists, so a handle need not keep them alive.
        private static bool IsWeakKind(object obj)
        {
            return obj is Thing || obj is Map || obj is Faction;
        }

        private static void PurgeDead()
        {
            List<int> dead = null;
            foreach (KeyValuePair<int, WeakReference> kv in WeakTargets)
            {
                if (!kv.Value.IsAlive)
                {
                    (dead ??= new List<int>()).Add(kv.Key);
                }
            }

            if (dead != null)
            {
                foreach (int id in dead)
                {
                    WeakTargets.Remove(id);
                }
            }
        }

        // Reference identity for classes (game objects may override Equals or GetHashCode), value equality for boxed structs.
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object x, object y)
            {
                if (ReferenceEquals(x, y)) return true;
                return x != null && x.GetType().IsValueType && x.Equals(y);
            }

            public int GetHashCode(object obj)
            {
                return obj.GetType().IsValueType ? obj.GetHashCode() : RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
