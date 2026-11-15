using Weftline.Core;
using Weftline.Core.Language;

namespace Weftline.Native;

/// <summary>
/// 用 native core 的 weft_parse_json 实现单文件语法解析端口。
/// </summary>
public sealed class NativeWeftSyntaxParser : IWeftSyntaxParser
{
    public async ValueTask<IReadOnlyList<WeftDiagnostic>> ParseAsync(
        string filePath,
        string text,
        long version,
        CancellationToken cancellationToken)
    {
        var response = await Task.Run(
            () => NativeWeftlineClient.Parse(filePath, text, version),
            cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            return [];
        }

        var resolvedPath = string.IsNullOrEmpty(response.FilePath)
            ? filePath
            : response.FilePath;

        return response.Diagnostics
            .Select(diagnostic => new WeftDiagnostic(
                resolvedPath,
                new TextSpan(diagnostic.Start, diagnostic.Length),
                WeftDiagnosticSeverityMapping.FromNative(diagnostic.Severity),
                diagnostic.Code,
                diagnostic.Message))
            .ToList();
    }
}
