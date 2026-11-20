using Weftline.App.Features.Editor.Services;

namespace Weftline.App.Tests;

public sealed class WStoryEntryEditorTests
{
    [Fact]
    public void InsertsEntryAfterProject()
    {
        var updated = SetEntry(
            "@project 雨夜\n    @summary [一个故事。]\n",
            "序");

        Assert.Equal(
            "@project 雨夜\n    @entry 序\n    @summary [一个故事。]\n",
            updated);
    }

    [Fact]
    public void ReplacesExistingEntryAndRemovesDuplicates()
    {
        var updated = SetEntry(
            "@project 雨夜\n    @entry 旧入口\n    @entries 不是指令\n    @entry 另一个\n",
            "序");

        Assert.Equal(
            "@project 雨夜\n    @entry 序\n    @entries 不是指令\n",
            updated);
    }

    [Fact]
    public void PreservesCrLfAndTrailingNewline()
    {
        var updated = SetEntry(
            "@project 雨夜\r\n    @summary [一个故事。]\r\n",
            "序");

        Assert.Equal(
            "@project 雨夜\r\n    @entry 序\r\n    @summary [一个故事。]\r\n",
            updated);
    }

    [Fact]
    public void FailsWithoutProject()
    {
        var ok = WStoryEntryEditor.TrySetEntry(
            "@summary [没有项目。]\n",
            "序",
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("@project", error);
    }

    private static string SetEntry(string text, string moduleId)
    {
        var ok = WStoryEntryEditor.TrySetEntry(
            text, moduleId, out var updated, out var error);

        Assert.True(ok, error);
        return updated;
    }
}
