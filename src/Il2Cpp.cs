using System;
using System.Runtime.InteropServices;

namespace AstralParty.AutoFocus
{
    /// <summary>
    /// 极薄的 IL2CPP 反射层，直接使用 GameAssembly.dll 导出的原始 il2cpp API。
    /// 之所以不用 Il2CppInterop 的高层封装（如 Il2cppObject/Il2CppType），
    /// 是因为本游戏使用 HybridCLR 热更：Il2CppInterop 的 native detour 在
    /// GetTypeInfoFromTypeDefinitionIndex 上会失效（上游 issue #251），
    /// 任何“按名字/索引查类型”的 API 一调用就会 AccessViolation。
    /// 这里只做只读访问。
    /// </summary>
    internal static unsafe class Il2Cpp
    {
        // ---------- 域 / 程序集 / 映像 ----------
        public static IntPtr Domain() => Native.il2cpp_domain_get();

        public static uint AssemblyCount()
        {
            uint size = 0;
            Native.il2cpp_domain_get_assemblies(Domain(), ref size);
            return size;
        }

        public static IntPtr[] Assemblies(out uint count)
        {
            uint size = 0;
            var raw = Native.il2cpp_domain_get_assemblies(Domain(), ref size);
            var result = new IntPtr[size];
            for (uint i = 0; i < size; i++) result[i] = raw[i];
            count = size;
            return result;
        }

        public static IntPtr ImageOf(IntPtr assembly) => Native.il2cpp_assembly_get_image(assembly);

        public static string NameOf(IntPtr ptr)
            => ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);

        public static string ImageName(IntPtr image) => NameOf(Native.il2cpp_image_get_name(image));

        public static IntPtr Corlib() => Native.il2cpp_get_corlib();

        public static uint ClassCount(IntPtr image)
            => image == IntPtr.Zero ? 0 : Native.il2cpp_image_get_class_count(image);

        public static IntPtr ClassAt(IntPtr image, uint index)
            => image == IntPtr.Zero ? IntPtr.Zero : Native.il2cpp_image_get_class(image, index);

        // ---------- 类 ----------
        public static IntPtr ClassFromName(IntPtr image, string ns, string name)
            => Native.il2cpp_class_from_name(image, ns, name);

        public static IntPtr ClassParent(IntPtr klass)
            => klass == IntPtr.Zero ? IntPtr.Zero : Native.il2cpp_class_get_parent(klass);

        public static IntPtr ClassFromSystemType(IntPtr typeObj)
            => typeObj == IntPtr.Zero ? IntPtr.Zero : Native.il2cpp_class_from_system_type(typeObj);

        public static IntPtr ClassOf(IntPtr obj)
            => obj == IntPtr.Zero ? IntPtr.Zero : Native.il2cpp_object_get_class(obj);

        public static string ClassName(IntPtr klass)
            => klass == IntPtr.Zero ? null : NameOf(Native.il2cpp_class_get_name(klass));

        public static string ClassNamespace(IntPtr klass)
            => klass == IntPtr.Zero ? null : NameOf(Native.il2cpp_class_get_namespace(klass));

        /// <summary>按 image 类表逐项查找（不触发 Il2CppInterop 的坏 hook）。</summary>
        public static IntPtr FindClassInImage(IntPtr image, string ns, string name)
        {
            if (image == IntPtr.Zero) return IntPtr.Zero;
            uint count = Native.il2cpp_image_get_class_count(image);
            for (uint i = 0; i < count; i++)
            {
                var klass = Native.il2cpp_image_get_class(image, i);
                if (klass == IntPtr.Zero) continue;
                if (ClassName(klass) != name) continue;
                if (ClassNamespace(klass) != ns) continue;
                return klass;
            }
            return IntPtr.Zero;
        }

        /// <summary>在全部已加载程序集（含 HybridCLR 热更）的 image 类表里按 命名空间+类名 查找。</summary>
        public static IntPtr FindClass(string ns, string name)
        {
            uint n;
            var asms = Assemblies(out n);
            for (uint i = 0; i < n; i++)
            {
                var klass = FindClassInImage(ImageOf(asms[i]), ns, name);
                if (klass != IntPtr.Zero) return klass;
            }
            return IntPtr.Zero;
        }

        // ---------- 字段 ----------
        public static IntPtr Field(IntPtr klass, string fieldName)
            => klass == IntPtr.Zero ? IntPtr.Zero : Native.il2cpp_class_get_field_from_name(klass, fieldName);

        public static uint FieldOffset(IntPtr field) => Native.il2cpp_field_get_offset(field);

        public static IntPtr StaticObject(IntPtr field)
        {
            IntPtr value;
            Native.il2cpp_field_static_get_value(field, &value);
            return value;
        }

        public static IntPtr FieldObject(IntPtr obj, IntPtr field)
            => *(IntPtr*)((byte*)(void*)obj + FieldOffset(field));

        public static T FieldValue<T>(IntPtr obj, IntPtr field) where T : unmanaged
            => *(T*)((byte*)(void*)obj + FieldOffset(field));

        // ---------- 方法 ----------
        public static IntPtr Method(IntPtr klass, string name, int argCount)
            => klass == IntPtr.Zero ? IntPtr.Zero : Native.il2cpp_class_get_method_from_name(klass, name, argCount);

        public static IntPtr Invoke(IntPtr method, IntPtr instance, params IntPtr[] args)
        {
            IntPtr exc = IntPtr.Zero;
            IntPtr result;
            fixed (IntPtr* p = args)
            {
                result = Native.il2cpp_runtime_invoke(method, instance, (void**)p, ref exc);
            }
            if (exc != IntPtr.Zero) throw new InvalidOperationException("il2cpp_runtime_invoke 抛出异常");
            return result;
        }

        public static string InvokeString(IntPtr method, IntPtr instance, params IntPtr[] args)
            => ReadString(Invoke(method, instance, args));

        // ---------- 值 ----------
        public static long AsInt64(IntPtr boxed)
            => boxed == IntPtr.Zero ? 0 : *(long*)(void*)Native.il2cpp_object_unbox(boxed);

        public static int AsInt32(IntPtr boxed)
            => boxed == IntPtr.Zero ? 0 : *(int*)(void*)Native.il2cpp_object_unbox(boxed);

        public static IntPtr StringNew(string text) => Native.il2cpp_string_new(text);

        /// <summary>读取 il2cpp System.String（头 16 字节 + 长度 4 字节 + UTF-16）。</summary>
        /// <summary>il2cpp 数组长度（System.Array 布局：头 16 字节后是 length）。</summary>
        public static uint ArrayLength(IntPtr array)
            => array == IntPtr.Zero ? 0u : Native.il2cpp_array_length(array);

        /// <summary>il2cpp 数组取元素（对象数组，头 32 字节后是元素区）。</summary>
        public static IntPtr ArrayGet(IntPtr array, int index)
            => *(IntPtr*)((byte*)(void*)array + 0x20 + index * IntPtr.Size);

        public static string ReadString(IntPtr s)
        {
            if (s == IntPtr.Zero) return null;
            int len = *(int*)((byte*)(void*)s + 0x10);
            if (len <= 0 || len > 8192) return null;
            return new string((char*)((byte*)(void*)s + 0x14), 0, len);
        }
    }
}
