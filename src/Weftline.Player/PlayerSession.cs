using Weftline.Models;

namespace Weftline.Player;

/// <summary>
/// 纯剧情播放状态机：逐句显示、条件分支、效果和结束。
///
/// 不依赖 Avalonia 或 native core；输入是 <see cref="RuntimeProjection"/>。
/// 每次进入模块会立即显示第一条正文；正文耗尽后再解析出口。
/// 全局变量只在选择/自动分支被选中后修改，条件使用修改前的状态。
/// </summary>
public sealed class PlayerSession
{
    private const int MaxAutoTransitions = 64;

    private readonly Dictionary<string, RuntimeModule> _modules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RuntimeChapter> _chapters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _variables = new(StringComparer.Ordinal);
    private readonly List<RuntimeContent> _visibleContent = [];
    private readonly string _startModuleId;

    private RuntimeModule? _currentModule;
    private IReadOnlyList<RuntimeBranch> _visibleChoices = Array.Empty<RuntimeBranch>();
    private int _nextContentIndex;

    /// <summary>
    /// 从项目入口或指定模块开始播放。
    /// </summary>
    /// <param name="projection">播放器载荷。</param>
    /// <param name="startModuleId">
    /// 起点模块；为空时使用 <see cref="RuntimeProjection.Entry"/>。
    /// 模块不存在或项目没有入口时进入错误态，不抛异常。
    /// </param>
    public PlayerSession(RuntimeProjection projection, string? startModuleId = null)
    {
        ArgumentNullException.ThrowIfNull(projection);

        Projection = projection;

        foreach (var module in projection.Modules)
        {
            _modules[module.Id] = module;
        }

        foreach (var chapter in projection.Chapters)
        {
            _chapters[chapter.Id] = chapter;
        }

        _startModuleId = string.IsNullOrWhiteSpace(startModuleId)
            ? projection.Entry ?? string.Empty
            : startModuleId;

        if (string.IsNullOrWhiteSpace(_startModuleId))
        {
            SetError("项目没有入口模块");
            return;
        }

        Restart();
    }

    /// <summary>
    /// 当前播放使用的载荷。
    /// </summary>
    public RuntimeProjection Projection { get; }

    /// <summary>
    /// 本次播放的起点模块；重开会回到这里。
    /// </summary>
    public string StartModuleId => _startModuleId;

    /// <summary>
    /// 当前模块；结束时为 null。
    /// </summary>
    public RuntimeModule? CurrentModule => _currentModule;

    /// <summary>
    /// 当前模块 ID；结束时为 null。
    /// </summary>
    public string? CurrentModuleId => _currentModule?.Id;

    /// <summary>
    /// 当前章节标题；找不到章节时回退到 chapterId。
    /// </summary>
    public string? CurrentChapterTitle
    {
        get
        {
            if (_currentModule is not { } module)
            {
                return null;
            }

            return _chapters.TryGetValue(module.ChapterId, out var chapter)
                ? chapter.Title
                : module.ChapterId;
        }
    }

    /// <summary>
    /// 当前模块的场景标签；没有时为 null。
    /// </summary>
    public string? CurrentScene => _currentModule?.Scene;

    /// <summary>
    /// 当前模块已经显示的正文块，按播放顺序排列。
    /// </summary>
    public IReadOnlyList<RuntimeContent> VisibleContent => _visibleContent;

    /// <summary>
    /// 当前可见选项；非选择出口或尚未到达出口时为空。
    /// </summary>
    public IReadOnlyList<RuntimeBranch> VisibleChoices => _visibleChoices;

    /// <summary>
    /// 当前全局变量；仅供播放器逻辑和调试使用。
    /// </summary>
    public IReadOnlyDictionary<string, bool> Variables => _variables;

    /// <summary>
    /// 是否已经到达 END。
    /// </summary>
    public bool IsEnded { get; private set; }

    /// <summary>
    /// 是否进入错误态（缺失模块、无可用出口等）。
    /// </summary>
    public bool HasError { get; private set; }

    /// <summary>
    /// 错误态下的人类可读信息。
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 当前是否可以继续推进；出现选项或结束时为 false。
    /// </summary>
    public bool CanAdvance
        => !IsEnded
            && !HasError
            && _currentModule is not null
            && _visibleChoices.Count == 0;

    /// <summary>
    /// 显示下一块正文；正文耗尽时解析出口。
    /// </summary>
    /// <returns>状态是否发生变化。</returns>
    public bool Advance()
    {
        if (!CanAdvance || _currentModule is not { } module)
        {
            return false;
        }

        if (_nextContentIndex < module.Content.Count)
        {
            _visibleContent.Add(module.Content[_nextContentIndex]);
            _nextContentIndex++;
            return true;
        }

        ResolveExit();
        return true;
    }

    /// <summary>
    /// 选择当前可见选项中的一项。
    /// </summary>
    /// <param name="index">选项下标。</param>
    /// <returns>是否成功选中。</returns>
    public bool Choose(int index)
    {
        if (index < 0 || index >= _visibleChoices.Count || IsEnded || HasError)
        {
            return false;
        }

        var branch = _visibleChoices[index];
        _visibleChoices = Array.Empty<RuntimeBranch>();
        ApplyEffects(branch);
        EnterModule(branch.Target);
        return true;
    }

    /// <summary>
    /// 回到本次播放的起点模块，并重置全部变量为声明初始值。
    /// </summary>
    public void Restart()
    {
        if (string.IsNullOrWhiteSpace(_startModuleId))
        {
            SetError("项目没有入口模块");
            return;
        }

        _visibleContent.Clear();
        _visibleChoices = Array.Empty<RuntimeBranch>();
        _currentModule = null;
        _nextContentIndex = 0;
        IsEnded = false;
        HasError = false;
        ErrorMessage = null;

        _variables.Clear();
        foreach (var global in Projection.Globals)
        {
            _variables[global.Name] = global.Initial;
        }

        EnterModule(_startModuleId);
    }

    /// <summary>
    /// 进入模块并立即显示第一条正文；空模块会直接解析出口。
    ///
    /// 自动分支可能连续穿过多个空模块，因此用循环加深度上限避免空环死循环。
    /// </summary>
    private void EnterModule(string moduleId)
    {
        for (var depth = 0; depth < MaxAutoTransitions; depth++)
        {
            if (string.Equals(moduleId, Projection.End, StringComparison.Ordinal))
            {
                End();
                return;
            }

            if (!_modules.TryGetValue(moduleId, out var module))
            {
                SetError($"找不到模块：{moduleId}");
                return;
            }

            _currentModule = module;
            _visibleContent.Clear();
            _visibleChoices = Array.Empty<RuntimeBranch>();
            _nextContentIndex = 0;

            if (module.Content.Count > 0)
            {
                _visibleContent.Add(module.Content[0]);
                _nextContentIndex = 1;
                return;
            }

            if (module.Exit is null)
            {
                SetError($"模块 {module.Id} 没有出口");
                return;
            }

            if (module.Exit.Mode == ExitMode.Choice)
            {
                ShowChoices(module.Exit);
                return;
            }

            var branch = FirstSatisfied(module.Exit);
            if (branch is null)
            {
                SetError($"模块 {module.Id} 没有可用的自动出口");
                return;
            }

            ApplyEffects(branch);
            moduleId = branch.Target;
        }

        SetError("自动分支链过长，可能存在环");
    }

    /// <summary>
    /// 正文耗尽后解析当前模块的出口。
    /// </summary>
    private void ResolveExit()
    {
        if (_currentModule is not { } module)
        {
            SetError("没有当前模块");
            return;
        }

        if (module.Exit is null)
        {
            SetError($"模块 {module.Id} 没有出口");
            return;
        }

        if (module.Exit.Mode == ExitMode.Choice)
        {
            ShowChoices(module.Exit);
            return;
        }

        var branch = FirstSatisfied(module.Exit);
        if (branch is null)
        {
            SetError($"模块 {module.Id} 没有可用的自动出口");
            return;
        }

        ApplyEffects(branch);
        EnterModule(branch.Target);
    }

    /// <summary>
    /// 过滤出条件成立且带文案的选择分支。
    /// </summary>
    private void ShowChoices(RuntimeExit exit)
    {
        var choices = new List<RuntimeBranch>();

        foreach (var branch in exit.Branches)
        {
            if (string.IsNullOrWhiteSpace(branch.Label))
            {
                continue;
            }

            if (!Evaluate(branch.Condition))
            {
                continue;
            }

            choices.Add(branch);
        }

        if (choices.Count == 0)
        {
            SetError($"模块 {_currentModule?.Id ?? "?"} 没有可用选项");
            return;
        }

        _visibleChoices = choices;
    }

    /// <summary>
    /// 自动模式按书写顺序取第一条条件成立的分支。
    /// </summary>
    private RuntimeBranch? FirstSatisfied(RuntimeExit exit)
    {
        foreach (var branch in exit.Branches)
        {
            if (Evaluate(branch.Condition))
            {
                return branch;
            }
        }

        return null;
    }

    private void ApplyEffects(RuntimeBranch branch)
    {
        foreach (var effect in branch.Effects)
        {
            _variables[effect.Name] = effect.Value;
        }
    }

    /// <summary>
    /// 求值布尔表达式；缺失变量按 false 处理，null 表示无条件成立。
    /// </summary>
    private bool Evaluate(IrBoolExpr? expression)
    {
        if (expression is null)
        {
            return true;
        }

        return expression.Kind switch
        {
            "literal" => expression.Value ?? false,
            "ref" => expression.Name is { } name
                && _variables.TryGetValue(name, out var value)
                && value,
            "not" => !Evaluate(expression.Expr),
            "and" => Evaluate(expression.Left) && Evaluate(expression.Right),
            "or" => Evaluate(expression.Left) || Evaluate(expression.Right),
            _ => false,
        };
    }

    private void End()
    {
        IsEnded = true;
        HasError = false;
        ErrorMessage = null;
        _currentModule = null;
        _visibleContent.Clear();
        _visibleChoices = Array.Empty<RuntimeBranch>();
    }

    private void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        _visibleChoices = Array.Empty<RuntimeBranch>();
    }
}
