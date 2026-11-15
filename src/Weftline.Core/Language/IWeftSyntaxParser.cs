namespace Weftline.Core.Language;

/// <summary>
/// 单文件语法解析端口：只做 lexer/parser 并返回语法诊断。
/// 由 native core 实现，Core 内的语言服务通过它取语法诊断而不直接依赖 P/Invoke。
/// </summary>
public interface IWeftSyntaxParser
{
    ValueTask<IReadOnlyList<WeftDiagnostic>> ParseAsync(
        string filePath,
        string text,
        long version,
        CancellationToken cancellationToken);
}
