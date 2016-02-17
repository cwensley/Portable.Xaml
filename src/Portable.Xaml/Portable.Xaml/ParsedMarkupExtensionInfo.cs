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

		string ReadUntil (char ch, bool readToEnd = false, bool skip = true)
		{
			return ReadUntil (new [] { ch }, readToEnd, skip);
		}

		string ReadUntil (char[] ch, bool readToEnd = false, bool skip = true)
		{
			var endidx = value.IndexOf ('}', index);
			var idx = value.IndexOfAny (ch, index);
			string val = null;
			if (idx >= 0 && idx <= endidx) {
				val = value.Substring (index, idx - index);
				index = skip ? idx + 1 : idx;
			}
			else if (readToEnd) {
				if (endidx >= 0)
					val = value.Substring (index, endidx - index);
				else
					val = value.Substring (index);
				index = endidx == -1 ? value.Length : endidx;
			}
			return val;
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
					ReadUntil(',', true);
					return markup;
				}
				break;
			case '\'':
			case '"':
				var idx = index;
				var endch = Current;
				index++;
				var val = ReadUntil(endch);
				if (val != null)
				{
					ReadUntil (',', true);
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
			if (escapedValue != null) {
				AddPositionalParameter(escapedValue);
				ParseArgument();
				return true;
			}

			var name = ReadUntil(new [] { '=', ' ', ',' }, readToEnd: true, skip: false);
			if (string.IsNullOrEmpty(name))
				return false;
			if (!ReadWhitespaceUntil('='))
			{
				AddPositionalParameter(name + ReadUntil(',', true).TrimEnd());
				ParseArgument();
				return true;
			}
			member = Type.GetMember (name) ?? new XamlMember (name, Type, false);
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

			var val = ReadUntil (',', true);
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
