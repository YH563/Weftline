using System.Runtime.InteropServices;

namespace Weftline.Native;

// LibraryImport 在编译期生成 P/Invoke 封送代码；C ABI 字符串统一按 UTF-8 传递。
internal static partial class NativeMethods
{
    private const string Library = "weftline_core";

    [LibraryImport(Library)]
    internal static partial IntPtr weft_version();

    [LibraryImport(Library)]
    internal static partial IntPtr weft_project_create();

    [LibraryImport(Library)]
    internal static partial void weft_project_destroy(IntPtr project);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void weft_project_set_folder(
        IntPtr project,
        string folder);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void weft_project_set_document(
        IntPtr project,
        string path,
        string text);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void weft_project_remove_document(
        IntPtr project,
        string path);

    [LibraryImport(Library)]
    internal static partial IntPtr weft_project_compile(
        IntPtr project,
        int allowPartial,
        int buildProjection);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr weft_parse_json(
        string path,
        string text,
        long version);

    [LibraryImport(Library)]
    internal static partial void weft_free(IntPtr pointer);
}
