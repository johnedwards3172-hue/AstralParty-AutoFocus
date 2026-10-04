using System;
using System.Runtime.InteropServices;

namespace AstralFocus
{
    /// <summary>
    /// 直接从 GameAssembly.dll 绑定原始 il2cpp 导出。
    /// 刻意不使用 Il2CppInterop.Runtime.IL2CPP：本游戏用 HybridCLR 热更，
    /// Il2CppInterop 对部分 API 做了托管包装并挂了 native detour，
    /// 那些 detour 在 HybridCLR 上会失效并 AccessViolation（上游 issue #251）。
    /// P/Invoke 到原始导出可以完全绕开那层包装。
    /// 全部只读。
    /// </summary>
    internal static class Native
    {
        private const string DLL = "GameAssembly";

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_domain_get();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_get_corlib();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe IntPtr* il2cpp_domain_get_assemblies(IntPtr domain, ref uint size);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_assembly_get_image(IntPtr assembly);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_image_get_name(IntPtr image);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint il2cpp_image_get_class_count(IntPtr image);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_image_get_class(IntPtr image, uint index);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_class_get_name(IntPtr klass);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_class_get_namespace(IntPtr klass);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_class_get_parent(IntPtr klass);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_class_from_name(IntPtr image,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string namespaze,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_class_from_system_type(IntPtr type);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_object_get_class(IntPtr obj);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_class_get_field_from_name(IntPtr klass,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint il2cpp_field_get_offset(IntPtr field);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void il2cpp_field_static_get_value(IntPtr field, void* value);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_class_get_method_from_name(IntPtr klass,
            [MarshalAs(UnmanagedType.LPStr)] string name, int argsCount);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe IntPtr il2cpp_runtime_invoke(IntPtr method, IntPtr obj, void** param, ref IntPtr exc);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_object_unbox(IntPtr obj);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_string_new([MarshalAs(UnmanagedType.LPUTF8Str)] string text);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_thread_attach(IntPtr domain);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_object_new(IntPtr klass);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint il2cpp_array_length(IntPtr array);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_class_get_fields(IntPtr klass, ref IntPtr iter);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr il2cpp_field_get_name(IntPtr field);
    }
}
