using System.Runtime.InteropServices;

namespace Weftline.Native;

internal static class NativeMethods
{
    private const string Library = "weftline_core";

    [DllImport(Library)]
    internal static extern IntPtr weft_version();

    [DllImport(Library)]
    internal static extern IntPtr weft_project_create();

    [DllImport(Library)]
    internal static extern void weft_project_destroy(IntPtr project);

    [DllImport(Library)]
    internal static extern void weft_project_set_folder(
        IntPtr project,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string folder);

    [DllImport(Library)]
    internal static extern void weft_project_set_document(
        IntPtr project,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(Library)]
    internal static extern void weft_project_remove_document(
        IntPtr project,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Library)]
    internal static extern IntPtr weft_project_compile(
        IntPtr project,
        int allowPartial,
        int buildProjection);

    [DllImport(Library)]
    internal static extern IntPtr weft_parse_json(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        long version);

    [DllImport(Library)]
    internal static extern void weft_free(IntPtr pointer);
}
