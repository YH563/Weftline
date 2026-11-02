using Weftline.Language.Diagnostics;
using Weftline.Language.Source;

namespace Weftline.Language.Syntax;

/// <summary>
/// 布尔条件的递归下降解析器。
///
/// 文法：
///   expr    := or
///   or      := and ("or" and)*
///   and     := unary ("and" unary)*
///   unary   := "not" unary | primary
///   primary := "(" expr ")" | identifier | "true" | "false"
///
/// 外层的 Parser 会去掉条件最外层的括号，因此这里处理的是内部文本。
/// offset 是该文本在源码中的绝对位置，用于产生精确诊断。
///
/// Recursive-descent parser for boolean conditions.
/// The grammar is shown above. The outer Parser strips the condition's outer
/// parentheses, so this parser works on the inner text. offset is the absolute
/// source position of that text and is used to produce precise diagnostics.
/// </summary>
internal static class ConditionParser
{
    public static BoolExprSyntax? Parse(string text, int offset, DiagnosticBag diagnostics)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return new Impl(text, offset, diagnostics).ParseAll();
    }

    private sealed class Impl
    {
        private readonly string _text;
        private readonly int _offset;
        private readonly DiagnosticBag _diagnostics;
        private int _position;

        public Impl(string text, int offset, DiagnosticBag diagnostics)
        {
            _text = text;
            _offset = offset;
            _diagnostics = diagnostics;
        }

        public BoolExprSyntax? ParseAll()
        {
            var expression = ParseOr();

            SkipWhitespace();
            if (_position < _text.Length)
            {
                Report("条件表达式后存在多余内容。", _position, 1);
            }

            return expression;
        }

        private BoolExprSyntax? ParseOr()
        {
            var left = ParseAnd();

            while (left is not null)
            {
                SkipWhitespace();
                var keywordStart = _position;

                if (!TryConsumeKeyword("or"))
                {
                    break;
                }

                var right = ParseAnd();
                if (right is null)
                {
                    Report("or 后缺少操作数。", keywordStart, 2);
                    return left;
                }

                left = new OrExprSyntax(
                    left,
                    right,
                    new TextSpan(left.Span.Start, right.Span.End - left.Span.Start));
            }

            return left;
        }

        private BoolExprSyntax? ParseAnd()
        {
            var left = ParseUnary();

            while (left is not null)
            {
                SkipWhitespace();
                var keywordStart = _position;

                if (!TryConsumeKeyword("and"))
                {
                    break;
                }

                var right = ParseUnary();
                if (right is null)
                {
                    Report("and 后缺少操作数。", keywordStart, 3);
                    return left;
                }

                left = new AndExprSyntax(
                    left,
                    right,
                    new TextSpan(left.Span.Start, right.Span.End - left.Span.Start));
            }

            return left;
        }

        private BoolExprSyntax? ParseUnary()
        {
            SkipWhitespace();
            var start = _position;

            if (TryConsumeKeyword("not"))
            {
                var operand = ParseUnary();
                if (operand is null)
                {
                    Report("not 后缺少操作数。", start, 3);
                    return null;
                }

                return new NotExprSyntax(
                    operand,
                    new TextSpan(ToAbsolute(start), operand.Span.End - ToAbsolute(start)));
            }

            return ParsePrimary();
        }

        private BoolExprSyntax? ParsePrimary()
        {
            SkipWhitespace();

            if (_position >= _text.Length)
            {
                Report("期望变量名、true、false 或左括号。", _position, 0);
                return null;
            }

            var start = _position;
            var current = _text[_position];

            if (current == '(')
            {
                _position++;

                var expression = ParseOr();
                SkipWhitespace();

                if (_position >= _text.Length || _text[_position] != ')')
                {
                    Report("缺少右括号。", _position, 0);
                    return expression;
                }

                _position++;
                return expression;
            }

            if (SyntaxFacts.IsIdentifierStart(current))
            {
                _position++;
                while (_position < _text.Length && SyntaxFacts.IsIdentifierPart(_text[_position]))
                {
                    _position++;
                }

                var name = _text[start.._position];
                if (name is "and" or "or" or "not")
                {
                    Report($"保留字 {name} 不能作为变量名。", start, name.Length);
                    return null;
                }

                var span = new TextSpan(ToAbsolute(start), _position - start);
                return name switch
                {
                    "true" => new BoolLiteralSyntax(true, span),
                    "false" => new BoolLiteralSyntax(false, span),
                    _ => new BoolRefSyntax(name, span),
                };
            }

            Report($"无法识别的字符 '{current}'。", _position, 1);
            _position++;
            return null;
        }

        private bool TryConsumeKeyword(string keyword)
        {
            if (_position + keyword.Length > _text.Length)
            {
                return false;
            }

            if (string.CompareOrdinal(_text, _position, keyword, 0, keyword.Length) != 0)
            {
                return false;
            }

            var end = _position + keyword.Length;
            if (end < _text.Length && SyntaxFacts.IsIdentifierPart(_text[end]))
            {
                return false;
            }

            _position = end;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }

        private int ToAbsolute(int localPosition) => _offset + localPosition;

        private void Report(string message, int localPosition, int length)
        {
            _diagnostics.Error(
                DiagnosticCode.InvalidCondition,
                new TextSpan(ToAbsolute(localPosition), Math.Max(0, length)),
                message);
        }
    }
}
