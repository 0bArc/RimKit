using System;
using System.IO;
using System.Runtime.InteropServices;
using Verse;

namespace RimKit
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RimLuaCallbacks
    {
        public IntPtr log;
        public IntPtr message;
        public IntPtr register_hook;
        public IntPtr unregister_hook;
        public IntPtr host_invoke;
        public IntPtr host_free;
    }

    internal static class NativeAbi
    {
        private const string DllName = "rimlua_core";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rimlua_version")]
        private static extern IntPtr rimlua_version_raw();

        /// <summary>Version of the native core, from src/api/VERSION at build time. Empty when the export is missing.</summary>
        public static string NativeVersion()
        {
            try
            {
                return ReadCString(rimlua_version_raw());
            }
            catch (EntryPointNotFoundException)
            {
                return "";
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rimlua_init(ref RimLuaCallbacks cb);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rimlua_shutdown();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int rimlua_load_script(string path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int rimlua_load_directory(string dir);

        /// <summary>Names the mod whose Lua loads next so its callbacks are charged to it. Null clears.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void rimlua_set_mod_context(string packageId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int rimlua_mod_disabled(string packageId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rimlua_call_on_load();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rimlua_call_on_tick();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int rimlua_invoke_hook(int hookId, int arg0Handle, string ctxJson, out int outContinue);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rimlua_invoke_hook_ex")]
        private static extern IntPtr rimlua_invoke_hook_ex_raw(int hookId, byte[] ctxJsonUtf8);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rimlua_invoke_hook_fast(int hookId, int pawnHandle, int nargs, double[] args, byte[] types,
            int hasResult, double result, byte resultType, out double outResult, out int outFlags);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rimlua_set_hook_info")]
        private static extern void rimlua_set_hook_info_raw(int hookId, byte[] methodUtf8, byte[] phaseUtf8, byte[] namesJsonUtf8);

        public static void SetHookInfo(int hookId, string method, string phase, string namesJson)
        {
            rimlua_set_hook_info_raw(hookId, Utf8Z(method), Utf8Z(phase), Utf8Z(namesJson));
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rimlua_emit_event_ex")]
        private static extern void rimlua_emit_event_ex_raw(byte[] nameUtf8, byte[] payloadJsonUtf8);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void rimlua_emit_event(string name, int handle);

        private static byte[] Utf8Z(string s)
        {
            byte[] raw = System.Text.Encoding.UTF8.GetBytes(s ?? "");
            var z = new byte[raw.Length + 1];
            Buffer.BlockCopy(raw, 0, z, 0, raw.Length);
            return z;
        }

        /// <summary>Calls a Lua hook with a UTF-8 context and returns its JSON response, or null.</summary>
        public static string InvokeHookEx(int hookId, string ctxJson)
        {
            IntPtr p = rimlua_invoke_hook_ex_raw(hookId, Utf8Z(ctxJson));
            if (p == IntPtr.Zero)
            {
                return null;
            }

            int len = 0;
            while (Marshal.ReadByte(p, len) != 0)
            {
                len++;
            }

            if (len == 0)
            {
                return null;
            }

            var buf = new byte[len];
            Marshal.Copy(p, buf, 0, len);
            return System.Text.Encoding.UTF8.GetString(buf);
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rimlua_ui_call")]
        private static extern IntPtr rimlua_ui_call_raw(int callbackId, byte[] argJsonUtf8);

        /// <summary>Calls a Lua function registered through a kit callback argument. Returns its JSON result or null.</summary>
        public static string UiCall(int callbackId, string argJson)
        {
            IntPtr p = rimlua_ui_call_raw(callbackId, Utf8Z(argJson));
            if (p == IntPtr.Zero) return null;
            int len = 0;
            while (Marshal.ReadByte(p, len) != 0) len++;
            if (len == 0) return null;
            var buf = new byte[len];
            Marshal.Copy(p, buf, 0, len);
            return System.Text.Encoding.UTF8.GetString(buf);
        }

        public static void EmitEventEx(string name, string payloadJson)
        {
            rimlua_emit_event_ex_raw(Utf8Z(name), Utf8Z(payloadJson));
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int rimlua_job_call(string name, string phase, int pawnHandle, out int outResult);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rimlua_ui_invoke(int callbackId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr rimlua_collect_map_float_menu(int clickedHandle, int haulerHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogFn(IntPtr msg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MessageFn(IntPtr msg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public delegate int RegisterHookFn(string typeName, string methodName, int kind, int hookId, string optsJson);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void UnregisterHookFn(int hookId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public delegate IntPtr HostInvokeFn(string op, string argsJson);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void HostFreeFn(IntPtr p);

        public static LogFn KeepLog;
        public static MessageFn KeepMessage;
        public static RegisterHookFn KeepRegister;
        public static UnregisterHookFn KeepUnregister;
        public static HostInvokeFn KeepInvoke;
        public static HostFreeFn KeepFree;

        public static string ReadCString(IntPtr ptr)
        {
            return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
        }

        public static string ResolveDllDirectory()
        {
            string asm = typeof(NativeAbi).Assembly.Location;
            return string.IsNullOrEmpty(asm) ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(asm);
        }
    }
}
