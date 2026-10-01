using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Verse;

namespace RimLuaKit
{
    internal static class ObjectHandles
    {
        private static readonly Dictionary<object, int> ObjectToHandle = new Dictionary<object, int>();
        private static readonly Dictionary<int, object> HandleToObject = new Dictionary<int, object>();
        private static int nextId = 1;

        public static int GetOrAdd(object obj)
        {
            if (obj == null)
            {
                return 0;
            }

            if (ObjectToHandle.TryGetValue(obj, out int existing))
            {
                return existing;
            }

            int id = nextId++;
            ObjectToHandle[obj] = id;
            HandleToObject[id] = obj;
            return id;
        }

        public static T Get<T>(int handle) where T : class
        {
            if (handle == 0)
            {
                return null;
            }

            return HandleToObject.TryGetValue(handle, out object obj) ? obj as T : null;
        }

        public static void Clear()
        {
            ObjectToHandle.Clear();
            HandleToObject.Clear();
        }
    }
}
