using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProjectTools.MenuTool
{
    /// <summary>
    /// JSON codec for .menutool source files.
    ///
    /// MenuToolNode is intentionally a recursive plain C# type. Unity's serializer and
    /// JsonUtility inspect recursive serializable type graphs up front and emit the
    /// "Serialization depth limit 10 exceeded" warning even when every children list is
    /// empty. Keeping .menutool serialization here lets the source format remain a natural
    /// tree without exposing that recursive graph to Unity serialization.
    /// </summary>
    internal static class MenuToolJson
    {
        public static MenuToolData Deserialize(string json)
        {
            var reader = new Reader(json);
            var data = ReadData(reader);
            reader.SkipWhitespace();
            if (!reader.IsEnd)
                throw reader.Error("Unexpected content after the root object.");
            return data;
        }

        public static string Serialize(MenuToolData data, bool prettyPrint = true)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var builder = new StringBuilder(2048);
            var writer = new Writer(builder, prettyPrint);
            writer.WriteData(data);
            return builder.ToString();
        }

        private static MenuToolData ReadData(Reader reader)
        {
            var data = new MenuToolData();
            reader.Expect('{');
            reader.SkipWhitespace();

            if (reader.TryConsume('}'))
                return data;

            while (true)
            {
                var property = reader.ReadString();
                reader.SkipWhitespace();
                reader.Expect(':');
                reader.SkipWhitespace();

                switch (property)
                {
                    case "version":
                        data.version = reader.ReadInt32();
                        break;
                    case "identifier":
                        data.identifier = reader.ReadNullableString() ?? string.Empty;
                        break;
                    case "label":
                        data.label = reader.ReadNullableString() ?? string.Empty;
                        break;
                    case "defaultFileName":
                        data.defaultFileName = reader.ReadNullableString() ?? string.Empty;
                        break;
                    case "nodes":
                        data.nodes = ReadNodes(reader);
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }

                reader.SkipWhitespace();
                if (reader.TryConsume('}'))
                    break;

                reader.Expect(',');
                reader.SkipWhitespace();
            }

            data.nodes ??= new List<MenuToolNode>();
            return data;
        }

        private static List<MenuToolNode> ReadNodes(Reader reader)
        {
            if (reader.TryConsumeNull())
                return new List<MenuToolNode>();

            var nodes = new List<MenuToolNode>();
            reader.Expect('[');
            reader.SkipWhitespace();

            if (reader.TryConsume(']'))
                return nodes;

            while (true)
            {
                nodes.Add(ReadNode(reader));
                reader.SkipWhitespace();
                if (reader.TryConsume(']'))
                    break;

                reader.Expect(',');
                reader.SkipWhitespace();
            }

            return nodes;
        }

        private static MenuToolNode ReadNode(Reader reader)
        {
            if (reader.TryConsumeNull())
                return null;

            var node = new MenuToolNode();
            reader.Expect('{');
            reader.SkipWhitespace();

            if (reader.TryConsume('}'))
                return node;

            while (true)
            {
                var property = reader.ReadString();
                reader.SkipWhitespace();
                reader.Expect(':');
                reader.SkipWhitespace();

                switch (property)
                {
                    case "identifier":
                        node.identifier = reader.ReadNullableString() ?? string.Empty;
                        break;
                    case "label":
                        node.label = reader.ReadNullableString() ?? string.Empty;
                        break;
                    case "fileName":
                        node.fileName = reader.ReadNullableString() ?? string.Empty;
                        break;
                    case "children":
                        node.children = ReadNodes(reader);
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }

                reader.SkipWhitespace();
                if (reader.TryConsume('}'))
                    break;

                reader.Expect(',');
                reader.SkipWhitespace();
            }

            node.children ??= new List<MenuToolNode>();
            return node;
        }

        private sealed class Reader
        {
            private readonly string text;
            private int index;

            public Reader(string text)
            {
                this.text = text ?? throw new ArgumentNullException(nameof(text));
            }

            public bool IsEnd => index >= text.Length;

            public void SkipWhitespace()
            {
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                    index++;
            }

            public bool TryConsume(char value)
            {
                SkipWhitespace();
                if (index >= text.Length || text[index] != value)
                    return false;

                index++;
                return true;
            }

            public void Expect(char value)
            {
                SkipWhitespace();
                if (index >= text.Length || text[index] != value)
                    throw Error($"Expected '{value}'.");
                index++;
            }

            public bool TryConsumeNull()
            {
                SkipWhitespace();
                if (!MatchesLiteral("null"))
                    return false;

                index += 4;
                return true;
            }

            public string ReadNullableString()
            {
                SkipWhitespace();
                return TryConsumeNull() ? null : ReadString();
            }

            public string ReadString()
            {
                SkipWhitespace();
                if (index >= text.Length || text[index] != '"')
                    throw Error("Expected a JSON string.");

                index++;
                var builder = new StringBuilder();
                while (index < text.Length)
                {
                    var c = text[index++];
                    if (c == '"')
                        return builder.ToString();

                    if (c != '\\')
                    {
                        if (c < 0x20)
                            throw Error("Unescaped control character in JSON string.");
                        builder.Append(c);
                        continue;
                    }

                    if (index >= text.Length)
                        throw Error("Incomplete JSON escape sequence.");

                    var escape = text[index++];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(ReadUnicodeEscape()); break;
                        default: throw Error($"Unsupported JSON escape '\\{escape}'.");
                    }
                }

                throw Error("Unterminated JSON string.");
            }

            public int ReadInt32()
            {
                SkipWhitespace();
                var start = index;
                if (index < text.Length && text[index] == '-')
                    index++;

                var digitStart = index;
                while (index < text.Length && char.IsDigit(text[index]))
                    index++;

                if (digitStart == index)
                    throw Error("Expected an integer.");

                var token = text.Substring(start, index - start);
                if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    throw Error($"Integer '{token}' is outside the Int32 range.");
                return value;
            }

            public void SkipValue()
            {
                SkipWhitespace();
                if (index >= text.Length)
                    throw Error("Expected a JSON value.");

                switch (text[index])
                {
                    case '{':
                        SkipObject();
                        return;
                    case '[':
                        SkipArray();
                        return;
                    case '"':
                        ReadString();
                        return;
                    case 't':
                        ConsumeLiteral("true");
                        return;
                    case 'f':
                        ConsumeLiteral("false");
                        return;
                    case 'n':
                        ConsumeLiteral("null");
                        return;
                    default:
                        SkipNumber();
                        return;
                }
            }

            public FormatException Error(string message)
            {
                return new FormatException($"{message} (at character {index})");
            }

            private char ReadUnicodeEscape()
            {
                if (index + 4 > text.Length)
                    throw Error("Incomplete Unicode escape sequence.");

                var value = 0;
                for (var i = 0; i < 4; i++)
                {
                    var c = text[index++];
                    value <<= 4;
                    if (c >= '0' && c <= '9') value += c - '0';
                    else if (c >= 'a' && c <= 'f') value += c - 'a' + 10;
                    else if (c >= 'A' && c <= 'F') value += c - 'A' + 10;
                    else throw Error($"Invalid hexadecimal character '{c}' in Unicode escape.");
                }

                return (char)value;
            }

            private void SkipObject()
            {
                Expect('{');
                SkipWhitespace();
                if (TryConsume('}'))
                    return;

                while (true)
                {
                    ReadString();
                    SkipWhitespace();
                    Expect(':');
                    SkipValue();
                    SkipWhitespace();
                    if (TryConsume('}'))
                        return;
                    Expect(',');
                }
            }

            private void SkipArray()
            {
                Expect('[');
                SkipWhitespace();
                if (TryConsume(']'))
                    return;

                while (true)
                {
                    SkipValue();
                    SkipWhitespace();
                    if (TryConsume(']'))
                        return;
                    Expect(',');
                }
            }

            private void SkipNumber()
            {
                var start = index;
                if (index < text.Length && text[index] == '-') index++;
                while (index < text.Length && char.IsDigit(text[index])) index++;
                if (index < text.Length && text[index] == '.')
                {
                    index++;
                    while (index < text.Length && char.IsDigit(text[index])) index++;
                }
                if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
                {
                    index++;
                    if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                    while (index < text.Length && char.IsDigit(text[index])) index++;
                }

                if (start == index)
                    throw Error("Expected a JSON value.");
            }

            private void ConsumeLiteral(string literal)
            {
                SkipWhitespace();
                if (!MatchesLiteral(literal))
                    throw Error($"Expected '{literal}'.");
                index += literal.Length;
            }

            private bool MatchesLiteral(string literal)
            {
                if (index + literal.Length > text.Length)
                    return false;

                for (var i = 0; i < literal.Length; i++)
                {
                    if (text[index + i] != literal[i])
                        return false;
                }

                return true;
            }
        }

        private sealed class Writer
        {
            private readonly StringBuilder builder;
            private readonly bool pretty;
            private int indent;

            public Writer(StringBuilder builder, bool pretty)
            {
                this.builder = builder;
                this.pretty = pretty;
            }

            public void WriteData(MenuToolData data)
            {
                BeginObject();
                WritePropertyName("version");
                builder.Append(data.version.ToString(CultureInfo.InvariantCulture));
                NextProperty();
                WritePropertyName("identifier");
                WriteString(data.identifier);
                NextProperty();
                WritePropertyName("label");
                WriteString(data.label);
                NextProperty();
                WritePropertyName("defaultFileName");
                WriteString(data.defaultFileName);
                NextProperty();
                WritePropertyName("nodes");
                WriteNodes(data.nodes ?? new List<MenuToolNode>());
                EndObject();
            }

            private void WriteNodes(List<MenuToolNode> nodes)
            {
                builder.Append('[');
                if (nodes.Count == 0)
                {
                    builder.Append(']');
                    return;
                }

                indent++;
                NewLine();
                for (var i = 0; i < nodes.Count; i++)
                {
                    WriteNode(nodes[i]);
                    if (i < nodes.Count - 1)
                    {
                        builder.Append(',');
                        NewLine();
                    }
                }
                indent--;
                NewLine();
                builder.Append(']');
            }

            private void WriteNode(MenuToolNode node)
            {
                if (node == null)
                {
                    builder.Append("null");
                    return;
                }

                BeginObject();
                WritePropertyName("identifier");
                WriteString(node.identifier);
                NextProperty();
                WritePropertyName("label");
                WriteString(node.label);
                NextProperty();
                WritePropertyName("fileName");
                WriteString(node.fileName);
                NextProperty();
                WritePropertyName("children");
                WriteNodes(node.children ?? new List<MenuToolNode>());
                EndObject();
            }

            private void BeginObject()
            {
                builder.Append('{');
                indent++;
                NewLine();
            }

            private void EndObject()
            {
                indent--;
                NewLine();
                builder.Append('}');
            }

            private void NextProperty()
            {
                builder.Append(',');
                NewLine();
            }

            private void WritePropertyName(string name)
            {
                WriteString(name);
                builder.Append(pretty ? ": " : ":");
            }

            private void WriteString(string value)
            {
                if (value == null)
                {
                    builder.Append("null");
                    return;
                }

                builder.Append('"');
                foreach (var c in value)
                {
                    switch (c)
                    {
                        case '"': builder.Append("\\\""); break;
                        case '\\': builder.Append("\\\\"); break;
                        case '\b': builder.Append("\\b"); break;
                        case '\f': builder.Append("\\f"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            if (c < 0x20)
                                builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else
                                builder.Append(c);
                            break;
                    }
                }
                builder.Append('"');
            }

            private void NewLine()
            {
                if (!pretty)
                    return;

                builder.AppendLine();
                builder.Append(' ', indent * 2);
            }
        }
    }
}
