using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LumiShift.Infrastructure
{
    internal sealed class LightweightJsonReader : IDisposable
    {
        private readonly StringReader _reader;
        private int _pos;

        public LightweightJsonReader(string json)
        {
            _reader = new StringReader(json);
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }

        public object ReadValue()
        {
            SkipWhitespace();
            int c = _reader.Peek();
            if (c == -1) return null;

            switch ((char)c)
            {
                case '"': return ReadString();
                case '{': return ReadObject();
                case '[': return ReadArray();
                case 't': case 'f': return ReadBoolean();
                case 'n': return ReadNull();
                default:
                    if (c == '-' || char.IsDigit((char)c))
                        return ReadNumber();
                    throw new FormatException($"Unexpected character '{(char)c}' at position {_pos}");
            }
        }

        public Dictionary<string, object> ReadObject()
        {
            Expect('{');
            SkipWhitespace();
            if (PeekChar() == '}')
            {
                ReadChar();
                return new Dictionary<string, object>();
            }

            var obj = new Dictionary<string, object>();
            while (true)
            {
                SkipWhitespace();
                string key = ReadString();
                SkipWhitespace();
                Expect(':');
                SkipWhitespace();
                object value = ReadValue();
                obj[key] = value;
                SkipWhitespace();
                int c = ReadChar();
                if (c == '}') break;
                if (c != ',')
                    throw new FormatException($"Expected ',' or '}}' but got '{(char)c}' at position {_pos}");
            }
            return obj;
        }

        public List<object> ReadArray()
        {
            Expect('[');
            SkipWhitespace();
            if (PeekChar() == ']')
            {
                ReadChar();
                return new List<object>();
            }

            var list = new List<object>();
            while (true)
            {
                SkipWhitespace();
                list.Add(ReadValue());
                SkipWhitespace();
                int c = ReadChar();
                if (c == ']') break;
                if (c != ',')
                    throw new FormatException($"Expected ',' or ']' but got '{(char)c}' at position {_pos}");
            }
            return list;
        }

        public string ReadString()
        {
            var sb = new StringBuilder();
            Expect('"');
            while (true)
            {
                int c = ReadChar();
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    int escaped = ReadChar();
                    switch ((char)escaped)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            string hex = new string(new char[] { (char)ReadChar(), (char)ReadChar(), (char)ReadChar(), (char)ReadChar() });
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber));
                            break;
                        default:
                            sb.Append((char)escaped);
                            break;
                    }
                }
                else
                {
                    sb.Append((char)c);
                }
            }
        }

        private object ReadNumber()
        {
            var sb = new StringBuilder();
            int c = PeekChar();
            while (c != -1 && (char.IsDigit((char)c) || (char)c == '.' || (char)c == '-' || (char)c == '+' || (char)c == 'e' || (char)c == 'E'))
            {
                sb.Append((char)ReadChar());
                c = PeekChar();
            }
            string numStr = sb.ToString();
            if (numStr.Contains(".") || numStr.IndexOf('e') >= 0 || numStr.IndexOf('E') >= 0)
                return double.Parse(numStr, CultureInfo.InvariantCulture);
            return int.Parse(numStr, CultureInfo.InvariantCulture);
        }

        private bool ReadBoolean()
        {
            int c = ReadChar();
            if (c == 't')
            {
                ReadChar(); ReadChar(); ReadChar();
                return true;
            }
            if (c == 'f')
            {
                ReadChar(); ReadChar(); ReadChar(); ReadChar();
                return false;
            }
            throw new FormatException($"Expected boolean but got '{(char)c}'");
        }

        private object ReadNull()
        {
            ReadChar(); ReadChar(); ReadChar(); ReadChar();
            return null;
        }

        private void Expect(char expected)
        {
            int c = ReadChar();
            if (c != expected)
                throw new FormatException($"Expected '{expected}' but got '{(char)c}' at position {_pos}");
        }

        private int ReadChar()
        {
            int c = _reader.Read();
            _pos++;
            if (c == -1)
                throw new FormatException("Unexpected end of JSON input");
            return c;
        }

        private int PeekChar()
        {
            return _reader.Peek();
        }

        private void SkipWhitespace()
        {
            while (true)
            {
                int c = _reader.Peek();
                if (c == -1) return;
                if (!char.IsWhiteSpace((char)c)) return;
                _reader.Read();
                _pos++;
            }
        }
    }

    internal sealed class LightweightJsonWriter : IDisposable
    {
        private readonly StringBuilder _sb = new StringBuilder(1024);
        private readonly StringWriter _writer;

        public LightweightJsonWriter()
        {
            _writer = new StringWriter(_sb, CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }

        public override string ToString()
        {
            _writer.Flush();
            return _sb.ToString();
        }

        public void WriteObjectStart() { _writer.Write('{'); }
        public void WriteObjectEnd() { _writer.Write('}'); }
        public void WriteArrayStart() { _writer.Write('['); }
        public void WriteArrayEnd() { _writer.Write(']'); }
        public void WriteComma() { _writer.Write(','); }
        public void WriteKey(string key) { WriteValue(key); _writer.Write(':'); }

        public void WriteValue(string value)
        {
            if (value == null)
            {
                _writer.Write("null");
                return;
            }
            _writer.Write('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': _writer.Write("\\\""); break;
                    case '\\': _writer.Write("\\\\"); break;
                    case '\b': _writer.Write("\\b"); break;
                    case '\f': _writer.Write("\\f"); break;
                    case '\n': _writer.Write("\\n"); break;
                    case '\r': _writer.Write("\\r"); break;
                    case '\t': _writer.Write("\\t"); break;
                    default:
                        if (c < 0x20)
                            _writer.Write($"\\u{(int)c:x4}");
                        else
                            _writer.Write(c);
                        break;
                }
            }
            _writer.Write('"');
        }

        public void WriteValue(int value) { _writer.Write(value); }
        public void WriteValue(long value) { _writer.Write(value); }
        public void WriteValue(bool value) { _writer.Write(value ? "true" : "false"); }
        public void WriteValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                _writer.Write("0.0");
            else
                _writer.Write(value.ToString("R", CultureInfo.InvariantCulture));
        }

        public void WriteNull() { _writer.Write("null"); }
    }
}