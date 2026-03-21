using System;
using System.Collections.Generic;
using System.Globalization;

namespace YoungBob.Prototype.Data
{
    public abstract class SExpressionNode
    {
    }

    public sealed class SExpressionSymbolNode : SExpressionNode
    {
        public string Value;
    }

    public sealed class SExpressionNumberNode : SExpressionNode
    {
        public double Value;
    }

    public sealed class SExpressionListNode : SExpressionNode
    {
        public string Head;
        public SExpressionNode[] Arguments;
    }

    internal static class SExpressionParser
    {
        public static SExpressionNode Parse(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new InvalidOperationException("effectsSExpr 不能为空。");
            }

            var parser = new Parser(source);
            var result = parser.ParseNode();
            parser.SkipWhitespace();
            if (!parser.IsAtEnd)
            {
                throw new InvalidOperationException("effectsSExpr 存在多余内容。");
            }

            return result;
        }

        private sealed class Parser
        {
            private readonly string _source;
            private int _index;

            public Parser(string source)
            {
                _source = source;
            }

            public bool IsAtEnd => _index >= _source.Length;

            public void SkipWhitespace()
            {
                while (!IsAtEnd && char.IsWhiteSpace(_source[_index]))
                {
                    _index += 1;
                }
            }

            public SExpressionNode ParseNode()
            {
                SkipWhitespace();
                if (IsAtEnd)
                {
                    throw new InvalidOperationException("effectsSExpr 不能为空。");
                }

                return _source[_index] == '(' ? ParseList() : ParseAtom();
            }

            private SExpressionNode ParseList()
            {
                _index += 1;
                SkipWhitespace();

                var head = ParseToken();
                if (string.IsNullOrWhiteSpace(head))
                {
                    throw new InvalidOperationException("effectsSExpr 缺少列表头。");
                }

                var arguments = new List<SExpressionNode>();
                while (true)
                {
                    SkipWhitespace();
                    if (IsAtEnd)
                    {
                        throw new InvalidOperationException("effectsSExpr 缺少右括号。");
                    }

                    if (_source[_index] == ')')
                    {
                        _index += 1;
                        break;
                    }

                    arguments.Add(ParseNode());
                }

                return new SExpressionListNode
                {
                    Head = head,
                    Arguments = arguments.ToArray()
                };
            }

            private SExpressionNode ParseAtom()
            {
                var token = ParseToken();
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException("effectsSExpr 存在空 token。");
                }

                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
                {
                    return new SExpressionNumberNode { Value = numeric };
                }

                return new SExpressionSymbolNode { Value = token };
            }

            private string ParseToken()
            {
                SkipWhitespace();
                var start = _index;
                while (!IsAtEnd)
                {
                    var current = _source[_index];
                    if (char.IsWhiteSpace(current) || current == '(' || current == ')')
                    {
                        break;
                    }

                    _index += 1;
                }

                return _source.Substring(start, _index - start);
            }
        }
    }
}
