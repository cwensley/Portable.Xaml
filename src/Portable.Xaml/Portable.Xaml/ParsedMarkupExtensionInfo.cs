//
// Copyright (C) 2010 Novell Inc. http://novell.com
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Portable.Xaml.Schema;
using System.Text;

namespace Portable.Xaml
{
    internal class ParsedMarkupExtensionInfo
    {
        Dictionary<XamlMember,object> args = new Dictionary<XamlMember,object> ();
        IXamlNamespaceResolver nsResolver;
        XamlSchemaContext sctx;

        string value;
        int index;
        List<object> positionalParameters;
        XamlMember member;

        public string Name { get; set; }
        public XamlType Type { get; set; }

        public Dictionary<XamlMember,object> Arguments
        {
            get { return args; }
        }


        public ParsedMarkupExtensionInfo(string value, IXamlNamespaceResolver nsResolver, XamlSchemaContext sctx)
        {
            if (value == null)
                throw new ArgumentNullException ("value");
            this.value = value;
            this.nsResolver = nsResolver;
            this.sctx = sctx;
        }

        public ParsedMarkupExtensionInfo()
        {
        }

        public ParsedMarkupExtensionInfo(ParsedMarkupExtensionInfo info)
        {
            this.value = info.value;
            this.index = info.index;
            this.nsResolver = info.nsResolver;
            this.sctx = info.sctx;
        }

        string ReadRest()
        {
            var endidx = value.IndexOf ('}', index);
            string val;
            if (endidx >= 0)
            {
                val = value.Substring(index, endidx - index);
                index = endidx;
            }
            else
            {
                val = value.Substring(index);
                index = value.Length;
            }
            return val;
        }

        string ReadUntil (char ch, bool readToEnd = false, bool skip = true, char? escape = null)
        {
            return ReadUntil (new [] { ch }, readToEnd, skip, escape);
        }

        string ReadUntil (char[] ch, bool readToEnd = false, bool skip = true, char? escape = null)
        {
            var endidx = IndexOfAnyEscaped (new char[] { '}' }, value, escape, index);
            var idx = IndexOfAnyEscaped (ch, value, escape, index);
            string val = null;
            if (idx >= 0 && idx <= endidx)
            {
                val = SubstringWithEscape (value, index, idx - index, escape);
                index = skip ? idx + 1 : idx;
            }
            else if (readToEnd)
            {
                if (endidx >= 0)
                    val = SubstringWithEscape (value, index, endidx - index, escape);
                else
                    val = SubstringWithEscape (value, index, escape);
                index = endidx == -1 ? value.Length : endidx;
            }

            return val;
        }

        string SubstringWithEscape (string s, int startIndex, char? escape)
        {
            return SubstringWithEscape (s, startIndex, s.Length - startIndex, escape);
        }

        string SubstringWithEscape (string s, int startIndex, int numCharacters, char? escape)
        {
            // If we aren't even using escapes, use the faster method
            if (escape == null)
                return s.Substring (startIndex, numCharacters);

            int idx = s.IndexOf (escape.Value, startIndex, numCharacters);

            // If the string contains no escape characters, use the faster method
            if (idx < 0 || idx >= startIndex + numCharacters)
                return s.Substring (startIndex, numCharacters);

            StringBuilder sb = new StringBuilder (s.Length);
            while (idx >= 0 && idx < startIndex + numCharacters)
            {
                if (idx - startIndex > 0)
                    sb.Append (s.Substring (startIndex, idx - startIndex));
                if (idx + 1 < s.Length)
                    sb.Append (s[idx + 1]);
                numCharacters -= (idx - startIndex + 2);
                startIndex = idx + 2;
                idx = s.IndexOf (escape.Value, startIndex, numCharacters);
            }

            if (idx > 0)
                sb.Append (s.Substring (startIndex, numCharacters));

            return sb.ToString ();
        }

        int IndexOfAnyEscaped (char[] ch, string value, char? escape, int startIdx)
        {
            if (escape == null)
                return value.IndexOfAny(ch, startIdx);

            int idx = 0, nextStart = startIdx;

            do
            {
                idx = ch.Length == 1 ? value.IndexOf (ch[0], nextStart) : value.IndexOfAny (ch, nextStart);
                nextStart = idx + 1;

                // If no good match, return; otherwise, check for escape characters
                if (idx < 0 || idx >= value.Length)
                    break;
                else
                {
                    // We need to handle repetitions. If there are an odd number of escape characters behind us, we
                    // are escaped; if an even number, we aren't escaped.
                    int numEscapes = 0;
                    for (int i = idx - 1; i >=0 && value [i] == escape.Value; i--)
                    {
                        ++numEscapes;
                    }

                    if (numEscapes % 2 == 0)
                        break;
                }
            }
            while (true);

            return idx;
        }

        void ReadWhitespace ()
        {
            while (index < value.Length && char.IsWhiteSpace (value [index])) {
                index++;
            }
        }

        bool ReadWhitespaceUntil (char ch)
        {
            var old = index;
            while (index < value.Length && char.IsWhiteSpace (value [index])) {
                index++;
            }
            if (Current == ch)
            {
                index++;
                return true;
            }
            index = old;
            return false;
        }

        bool Read(char ch)
        {
            if (Current == ch) {
                index++;
                return true;
            }
            return false;
        }

        void AddPositionalParameter (object value)
        {
            if (positionalParameters == null) {
                positionalParameters = new List<object> ();
                if (Arguments.Count > 0)
                {
                    // positional parameters can't come after non-positional parameters
                    throw Error("Unexpected positional parameter in expression '{0}'", this.value);
                }
                Arguments.Add (XamlLanguage.PositionalParameters, positionalParameters);
            }
            positionalParameters.Add (value);
        }

        object ParseEscapedValue()
        {
            switch (Current)
            {
                case '{':
                    var markup = ReadMarkup();
                    if (markup != null)
                    {
                        ReadUntil(',', true, escape: '\\');
                        return markup;
                    }
                    break;
                case '\'':
                case '"':
                    var idx = index;
                    var endch = Current;
                    index++;
                    var val = ReadUntil(endch, escape: '\\');
                    if (val != null)
                    {
                        ReadUntil(',', true, escape: '\\');
                        return val;
                    }
                    index = idx;
                    break;
            }
            return null;
        }

        bool ParseArgument ()
        {
            ReadWhitespace();
            var escapedValue = ParseEscapedValue ();
            if (escapedValue != null)
            {
                AddPositionalParameter(escapedValue);
                ParseArgument();
                return true;
            }

            var name = ReadUntil(new [] { '=', ' ', ',' }, readToEnd: true, skip: false, escape: '\\');
            if (string.IsNullOrEmpty(name))
                return false;
            if (!ReadWhitespaceUntil('='))
            {
                AddPositionalParameter(name + ReadUntil(',', true, escape: '\\').TrimEnd());
                ParseArgument();
                return true;
            }
            member = Type.GetMember (name) ?? new XamlMember(name, Type, false);
            ReadWhitespace ();
            ParseValue ();
            return true;
        }

        char Current { get { return index < value.Length ? value [index] : unchecked((char)-1); } }

        bool Finished { get { return index >= value.Length; } }

        ParsedMarkupExtensionInfo ReadMarkup()
        {
            var info = new ParsedMarkupExtensionInfo (this);
            try {
                info.Parse ();
                index = info.index;
                return info;
            } catch {
            }
            return null;
        }

        void ParseValue()
        {
            var escapedValue = ParseEscapedValue ();
            if (escapedValue != null) {
                Arguments.Add (member, escapedValue);
                ParseArgument();
                return;
            }

            var val = ReadUntil(',', true, escape: '\\');
			val = val.Trim ();
            Arguments.Add (member, val);
            if (!ParseArgument()) {
                Arguments [member] = val + ReadRest ();
            }
        }

        public void Parse ()
        {
            if (!Read('{'))
                throw Error ("Invalid markup extension attribute. It should begin with '{{', but was {0}", value);
            Name = ReadUntil (' ', true);
            XamlTypeName xtn;
            if (!XamlTypeName.TryParse (Name, nsResolver, out xtn))
                throw Error ("Failed to parse type name '{0}'", Name);
            Type = sctx.GetXamlType (xtn);

            ParseArgument();
            if (!Read('}'))
                throw Error ("Expected '}}' in the markup extension attribute: '{0}'", value);
        }

        static Exception Error (string format, params object[] args)
        {
            return new XamlParseException (string.Format (format, args));
        }
    }
}
