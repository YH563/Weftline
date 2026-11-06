namespace Weftline.Models;

/// <summary>
/// 出口模式。
/// </summary>
public enum ExitMode
{
    /// <summary>自动出口：按条件/顺序执行，不显示选项。</summary>
    Auto,

    /// <summary>选择出口：显示文案供玩家选择。</summary>
    Choice,
}

/// <summary>
/// 扁平布尔表达式，供图投影消费。
/// </summary>
public sealed record IrBoolExpr(
    string Kind,
    bool? Value = null,
    string? Name = null,
    IrBoolExpr? Expr = null,
    IrBoolExpr? Left = null,
    IrBoolExpr? Right = null)
{
    /// <summary>
    /// 创建布尔字面量。
    /// </summary>
    public static IrBoolExpr Literal(bool value) => new("literal", Value: value);

    /// <summary>
    /// 创建变量引用。
    /// </summary>
    public static IrBoolExpr Ref(string name) => new("ref", Name: name);

    /// <summary>
    /// 创建逻辑非。
    /// </summary>
    public static IrBoolExpr Not(IrBoolExpr expression) => new("not", Expr: expression);

    /// <summary>
    /// 创建逻辑与。
    /// </summary>
    public static IrBoolExpr And(IrBoolExpr left, IrBoolExpr right)
        => new("and", Left: left, Right: right);

    /// <summary>
    /// 创建逻辑或。
    /// </summary>
    public static IrBoolExpr Or(IrBoolExpr left, IrBoolExpr right)
        => new("or", Left: left, Right: right);
}
