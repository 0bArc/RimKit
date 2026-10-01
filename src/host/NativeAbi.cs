using System;
using System.IO;
using System.Runtime.InteropServices;
using Verse;

namespace RimLuaKit
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

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rimlua_init(ref RimLuaCallbacks cb);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rimlua_shutdown();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int rimlua_load_script(string path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int rimlua_load_directory(string dir);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rimlua_call_on_load();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rimlua_call_on_tick();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rimlua_invoke_hook(int hookId, int arg0Handle, out int outContinue);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void rimlua_emit_event(string name, int handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int rimlua_job_call(string name, string phase, int pawnHandle, out int outResult);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rimlua_ui_invoke(int callbackId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogFn(IntPtr msg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MessageFn(IntPtr msg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public delegate int RegisterHookFn(string typeName, string methodName, int isPrefix, int hookId);

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
