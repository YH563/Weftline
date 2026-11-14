using Weftline.Models;
using Weftline.Player;

namespace Weftline.Player.Tests;

public sealed class PlayerSessionTests
{
    [Fact]
    public void StartsAtEntryAndRevealsFirstContent()
    {
        var session = new PlayerSession(Project(
        [
            Module("A", Auto(AutoTo("END")), Narration("a1"), Narration("a2")),
        ]));

        Assert.Equal("A", session.CurrentModuleId);
        Assert.Single(session.VisibleContent);
        Assert.True(session.CanAdvance);

        Assert.True(session.Advance());
        Assert.Equal(2, session.VisibleContent.Count);

        Assert.True(session.Advance());
        Assert.True(session.IsEnded);
        Assert.False(session.CanAdvance);
        Assert.Null(session.CurrentModuleId);
    }

    [Fact]
    public void AutoBranchAppliesEffectsBeforeEnteringTarget()
    {
        var session = new PlayerSession(Project(
        [
            Module(
                "A",
                Auto(AutoTo("B", Effect("x", true))),
                Narration("a1")),
            Module("B", Auto(AutoTo("END")), Narration("b1")),
        ],
        globals: [new RuntimeGlobal("x", false)]));

        Assert.False(session.Variables["x"]);

        Assert.True(session.Advance());
        Assert.Equal("B", session.CurrentModuleId);
        Assert.True(session.Variables["x"]);
        Assert.Single(session.VisibleContent);
        Assert.Equal("b1", Assert.Single(session.VisibleContent[0].Segments).Text);
    }

    [Fact]
    public void ChoiceFiltersByConditionAndAppliesEffect()
    {
        var session = new PlayerSession(Project(
        [
            Module(
                "A",
                Choice(
                    ChoiceIf(IrBoolExpr.Ref("flag"), "隐藏", "B"),
                    ChoiceTo("离开", "C", Effect("flag", true))),
                Narration("a1")),
            Module("B", Auto(AutoTo("END")), Narration("b1")),
            Module("C", Auto(AutoTo("END")), Narration("c1")),
        ],
        globals: [new RuntimeGlobal("flag", false)]));

        Assert.True(session.Advance());
        Assert.Single(session.VisibleChoices);
        Assert.Equal("离开", session.VisibleChoices[0].Label);
        Assert.False(session.CanAdvance);

        Assert.True(session.Choose(0));
        Assert.True(session.Variables["flag"]);
        Assert.Equal("C", session.CurrentModuleId);
        Assert.Single(session.VisibleContent);
    }

    [Fact]
    public void EmptyModuleShowsChoicesImmediately()
    {
        var session = new PlayerSession(Project(
        [
            Module("A", Choice(ChoiceTo("继续", "END"))),
        ]));

        Assert.Empty(session.VisibleContent);
        Assert.Single(session.VisibleChoices);
        Assert.False(session.CanAdvance);
    }

    [Fact]
    public void EvaluatesAndOrNot()
    {
        var condition = IrBoolExpr.And(
            IrBoolExpr.Ref("a"),
            IrBoolExpr.Not(IrBoolExpr.Ref("b")));

        var session = new PlayerSession(Project(
        [
            Module(
                "A",
                Auto(AutoIf(condition, "B"), AutoTo("C")),
                Narration("a1")),
            Module("B", Auto(AutoTo("END")), Narration("b1")),
            Module("C", Auto(AutoTo("END")), Narration("c1")),
        ],
        globals:
        [
            new RuntimeGlobal("a", true),
            new RuntimeGlobal("b", false),
        ]));

        Assert.True(session.Advance());
        Assert.Equal("B", session.CurrentModuleId);
    }

    [Fact]
    public void EvaluatesOrAndFallsBackWhenFalse()
    {
        var condition = IrBoolExpr.Or(
            IrBoolExpr.Ref("a"),
            IrBoolExpr.Ref("b"));

        var session = new PlayerSession(Project(
        [
            Module(
                "A",
                Auto(AutoIf(condition, "B"), AutoTo("C")),
                Narration("a1")),
            Module("B", Auto(AutoTo("END")), Narration("b1")),
            Module("C", Auto(AutoTo("END")), Narration("c1")),
        ],
        globals:
        [
            new RuntimeGlobal("a", false),
            new RuntimeGlobal("b", false),
        ]));

        Assert.True(session.Advance());
        Assert.Equal("C", session.CurrentModuleId);
    }

    [Fact]
    public void RestartResetsVariablesAndStartModule()
    {
        var session = new PlayerSession(Project(
        [
            Module(
                "A",
                Choice(ChoiceTo("设置", "B", Effect("x", true))),
                Narration("a1")),
            Module("B", Auto(AutoTo("END")), Narration("b1")),
        ],
        globals: [new RuntimeGlobal("x", false)]));

        Assert.True(session.Advance());
        Assert.True(session.Choose(0));
        Assert.True(session.Variables["x"]);
        Assert.Equal("B", session.CurrentModuleId);

        session.Restart();

        Assert.False(session.Variables["x"]);
        Assert.Equal("A", session.CurrentModuleId);
        Assert.Single(session.VisibleContent);
        Assert.Empty(session.VisibleChoices);
    }

    [Fact]
    public void StartsFromExplicitModuleWithInitialGlobals()
    {
        var session = new PlayerSession(
            Project(
            [
                Module("A", Auto(AutoTo("B")), Narration("a1")),
                Module("B", Auto(AutoTo("END")), Narration("b1")),
            ],
            globals: [new RuntimeGlobal("x", false)]),
            startModuleId: "B");

        Assert.Equal("B", session.StartModuleId);
        Assert.Equal("B", session.CurrentModuleId);
        Assert.False(session.Variables["x"]);
        Assert.Single(session.VisibleContent);
    }

    [Fact]
    public void MissingStartModuleEntersError()
    {
        var session = new PlayerSession(
            Project([Module("A", Auto(AutoTo("END")))]),
            startModuleId: "missing");

        Assert.True(session.HasError);
        Assert.Contains("missing", session.ErrorMessage);
        Assert.False(session.CanAdvance);
    }

    [Fact]
    public void MissingTargetEntersError()
    {
        var session = new PlayerSession(Project(
        [
            Module("A", Auto(AutoTo("missing")), Narration("a1")),
        ]));

        Assert.True(session.Advance());
        Assert.True(session.HasError);
        Assert.Contains("missing", session.ErrorMessage);
    }

    [Fact]
    public void NoVisibleChoiceEntersError()
    {
        var session = new PlayerSession(Project(
        [
            Module(
                "A",
                Choice(ChoiceIf(IrBoolExpr.Ref("flag"), "隐藏", "END")),
                Narration("a1")),
        ],
        globals: [new RuntimeGlobal("flag", false)]));

        Assert.True(session.Advance());
        Assert.True(session.HasError);
    }

    [Fact]
    public void AutoCycleStopsWithError()
    {
        var session = new PlayerSession(Project(
        [
            Module("A", Auto(AutoTo("B"))),
            Module("B", Auto(AutoTo("A"))),
        ]));

        Assert.True(session.HasError);
        Assert.Contains("环", session.ErrorMessage);
    }

    [Fact]
    public void ResolvesChapterTitleAndScene()
    {
        var module = new RuntimeModule(
            "A",
            "第一章",
            [new RuntimeScene("医院", 0)],
            [Narration("a1")],
            Auto(AutoTo("END")));

        var projection = new RuntimeProjection(
            1,
            "测试",
            "A",
            "END",
            [new RuntimeChapter("第一章", "第一章")],
            [],
            [module]);

        var session = new PlayerSession(projection);

        Assert.Equal("第一章", session.CurrentChapterTitle);
        Assert.Equal("医院", session.CurrentScene);
    }

    [Fact]
    public void UpdatesSceneWhenContentCrossesMarker()
    {
        var module = new RuntimeModule(
            "A",
            "第一章",
            [new RuntimeScene("街道", 0), new RuntimeScene("医院", 1)],
            [Narration("a1"), Narration("a2")],
            Auto(AutoTo("END")));

        var session = new PlayerSession(Project([module]));

        Assert.Equal("街道", session.CurrentScene);
        Assert.True(session.Advance());
        Assert.Equal("医院", session.CurrentScene);
    }

    private static RuntimeProjection Project(
        IReadOnlyList<RuntimeModule> modules,
        string? entry = "A",
        IReadOnlyList<RuntimeGlobal>? globals = null)
    {
        var chapters = modules
            .Select(static module => module.ChapterId)
            .Distinct(StringComparer.Ordinal)
            .Select(static chapterId => new RuntimeChapter(chapterId, chapterId))
            .ToList();

        return new RuntimeProjection(
            1,
            "测试",
            entry,
            "END",
            chapters,
            globals ?? [],
            modules);
    }

    private static RuntimeModule Module(
        string id,
        RuntimeExit? exit,
        params RuntimeContent[] content)
        => new(id, "第一章", [], content, exit);

    private static RuntimeExit Auto(params RuntimeBranch[] branches)
        => new(ExitMode.Auto, null, branches);

    private static RuntimeExit Choice(params RuntimeBranch[] branches)
        => new(ExitMode.Choice, null, branches);

    private static RuntimeBranch AutoTo(string target, params RuntimeEffect[] effects)
        => new(null, null, target, effects);

    private static RuntimeBranch AutoIf(IrBoolExpr condition, string target)
        => new(condition, null, target, []);

    private static RuntimeBranch ChoiceTo(
        string label,
        string target,
        params RuntimeEffect[] effects)
        => new(null, label, target, effects);

    private static RuntimeBranch ChoiceIf(
        IrBoolExpr condition,
        string label,
        string target)
        => new(condition, label, target, []);

    private static RuntimeContent Narration(string text)
        => new(
            RuntimeContentKind.Narration,
            [new RuntimeTextSegment(RuntimeTextSegmentKind.Text, text)]);

    private static RuntimeEffect Effect(string name, bool value)
        => new(name, value);
}
