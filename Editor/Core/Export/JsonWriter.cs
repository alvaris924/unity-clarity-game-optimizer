using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Writes JSON one token at a time, with the commas, quoting and escaping handled here so exporters only
    /// say what to write. The output is deterministic: the same calls produce the same text on every
    /// machine, with LF line breaks, two-space indentation and invariant-culture numbers. Kept in the
    /// package instead of a serializer dependency (ADR-0005); reports are written, never read back.
    /// </summary>
    internal sealed class JsonWriter
    {
        private struct Frame
        {
            public bool IsArray;
            public bool HasItems;
        }

        private readonly StringBuilder _text = new StringBuilder();
        private readonly List<Frame> _frames = new List<Frame>();
        private readonly bool _indented;
        private bool _namePending;
        private bool _rootWritten;

        public JsonWriter(bool indented = true)
        {
            _indented = indented;
        }

        public JsonWriter BeginObject()
        {
            BeginValue();
            _text.Append('{');
            _frames.Add(new Frame { IsArray = false });
            return this;
        }

        public JsonWriter EndObject()
        {
            EndContainer(false, '}');
            return this;
        }

        public JsonWriter BeginArray()
        {
            BeginValue();
            _text.Append('[');
            _frames.Add(new Frame { IsArray = true });
            return this;
        }

        public JsonWriter EndArray()
        {
            EndContainer(true, ']');
            return this;
        }

        /// <summary>Writes a property name; the next call writes its value.</summary>
        public JsonWriter Name(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (_frames.Count == 0 || Top.IsArray)
            {
                throw new InvalidOperationException("A name can only be written inside an object.");
            }

            if (_namePending)
            {
                throw new InvalidOperationException("A value must follow the previous name before another name is written.");
            }

            Separate();
            AppendQuoted(name);
            _text.Append(_indented ? ": " : ":");
            _namePending = true;
            return this;
        }

        public JsonWriter Value(string value)
        {
            BeginValue();
            if (value == null)
            {
                _text.Append("null");
            }
            else
            {
                AppendQuoted(value);
            }

            EndValue();
            return this;
        }

        /// <summary>Writes the shortest round-trip form; NaN and infinities have no JSON form and become null.</summary>
        public JsonWriter Value(double value)
        {
            BeginValue();
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                _text.Append("null");
            }
            else
            {
                _text.Append(value.ToString("R", CultureInfo.InvariantCulture));
            }

            EndValue();
            return this;
        }

        public JsonWriter Value(long value)
        {
            BeginValue();
            _text.Append(value.ToString(CultureInfo.InvariantCulture));
            EndValue();
            return this;
        }

        public JsonWriter Value(bool value)
        {
            BeginValue();
            _text.Append(value ? "true" : "false");
            EndValue();
            return this;
        }

        public JsonWriter Null()
        {
            BeginValue();
            _text.Append("null");
            EndValue();
            return this;
        }

        /// <summary>The JSON text. Throws while an object or array is still open, so a half-written document cannot leak out.</summary>
        public override string ToString()
        {
            if (_frames.Count > 0)
            {
                throw new InvalidOperationException(_frames.Count + " container(s) are still open.");
            }

            return _text.ToString();
        }

        private Frame Top
        {
            get { return _frames[_frames.Count - 1]; }
        }

        private void BeginValue()
        {
            if (_frames.Count == 0)
            {
                if (_rootWritten)
                {
                    throw new InvalidOperationException("A JSON document has one root value.");
                }

                return;
            }

            if (Top.IsArray)
            {
                Separate();
            }
            else if (!_namePending)
            {
                throw new InvalidOperationException("A value inside an object needs a name first.");
            }

            _namePending = false;
        }

        private void EndValue()
        {
            if (_frames.Count == 0)
            {
                _rootWritten = true;
            }
        }

        /// <summary>The comma, line break and indentation before an item of the current container.</summary>
        private void Separate()
        {
            Frame frame = Top;
            if (frame.HasItems)
            {
                _text.Append(',');
            }

            if (_indented)
            {
                _text.Append('\n');
                _text.Append(' ', 2 * _frames.Count);
            }

            frame.HasItems = true;
            _frames[_frames.Count - 1] = frame;
        }

        private void EndContainer(bool array, char close)
        {
            if (_frames.Count == 0 || Top.IsArray != array)
            {
                throw new InvalidOperationException("No " + (array ? "array" : "object") + " is open.");
            }

            if (_namePending)
            {
                throw new InvalidOperationException("A value must follow the name before the object closes.");
            }

            Frame frame = Top;
            _frames.RemoveAt(_frames.Count - 1);
            if (frame.HasItems && _indented)
            {
                _text.Append('\n');
                _text.Append(' ', 2 * _frames.Count);
            }

            _text.Append(close);
            EndValue();
        }

        private void AppendQuoted(string value)
        {
            _text.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        _text.Append("\\\"");
                        break;
                    case '\\':
                        _text.Append("\\\\");
                        break;
                    case '\n':
                        _text.Append("\\n");
                        break;
                    case '\r':
                        _text.Append("\\r");
                        break;
                    case '\t':
                        _text.Append("\\t");
                        break;
                    case '\b':
                        _text.Append("\\b");
                        break;
                    case '\f':
                        _text.Append("\\f");
                        break;
                    default:
                        if (c < ' ')
                        {
                            _text.Append("\\u");
                            _text.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _text.Append(c);
                        }

                        break;
                }
            }

            _text.Append('"');
        }
    }
}
