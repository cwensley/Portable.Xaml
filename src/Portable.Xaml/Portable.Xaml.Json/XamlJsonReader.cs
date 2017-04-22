using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Portable.Xaml.Schema;

namespace Portable.Xaml.Json
{
	[EnhancedXaml]
	public class XamlJsonReader : XamlReader, IXamlLineInfo
	{
		int _ch;
		bool isEof;
		TextReader _reader;
		XamlSchemaContext _context;
		IEnumerator<StateObject> _enumerator;
		XamlJsonReaderSettings _settings;
		int _lineNumber;
		int _linePosition;
		StringBuilder sb_quoted = new StringBuilder(50);
		Stack<Dictionary<string, string>> namespaces;

		class StateObject
		{
			public XamlNodeType NodeType;
			public XamlType Type => Value as XamlType;
			public XamlMember Member => Value as XamlMember;
			public NamespaceDeclaration Namespace => Value as NamespaceDeclaration;
			public object Value;

			static StateObject Shared = new StateObject(XamlNodeType.None);

			public static readonly StateObject EndMember = new StateObject(XamlNodeType.EndMember);

			public static readonly StateObject EndObject = new StateObject(XamlNodeType.EndObject);

			public static readonly StateObject GetObject = new StateObject(XamlNodeType.GetObject);

			StateObject(XamlNodeType nodeType)
			{
				NodeType = nodeType;
			}

			StateObject With(XamlNodeType nodeType, object value)
			{
				NodeType = nodeType;
				Value = value;
				return this;
			}

			public static StateObject WithValue(object value) => Shared.With(XamlNodeType.Value, value);

			public static StateObject WithNamespace(string ns, string prefix) => Shared.With(XamlNodeType.NamespaceDeclaration, new NamespaceDeclaration(ns, prefix ?? string.Empty));

			public static StateObject WithStartObject(XamlType type) => Shared.With(XamlNodeType.StartObject, type);

			public static StateObject WithStartMember(XamlMember member) => Shared.With(XamlNodeType.StartMember, member);

			/* */

			public static StateObject WithStartOrGetObject(XamlMember member, XamlType type)
			{
				return member?.IsWritePublic == true ? WithStartObject(type) : GetObject;
					
			}
		}

		public XamlJsonReader(Stream stream)
			: this(stream, new XamlSchemaContext(), null)
		{
		}


		public XamlJsonReader(TextReader reader)
			: this(reader, new XamlSchemaContext(), null)
		{
		}

		public XamlJsonReader(TextReader reader, XamlSchemaContext context)
			: this(reader, context, null)
		{
		}

		public XamlJsonReader(Stream stream, XamlSchemaContext context)
			: this(stream, context, null)
		{
		}

		public XamlJsonReader(Stream stream, XamlSchemaContext context, XamlJsonReaderSettings settings)
			: this (new StreamReader(stream), context, settings)
		{
		}

		public XamlJsonReader(TextReader textReader, XamlSchemaContext context, XamlJsonReaderSettings settings)
		{
			_reader = textReader ?? throw new ArgumentNullException(nameof(textReader));
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_settings = settings ?? XamlJsonReaderSettings.Instance;
			//ReadChar = ReadCharNoLine;
		}

		StateObject CurrentState => _enumerator?.Current;

		public override bool IsEof => isEof;

		public override XamlMember Member => CurrentState?.Member;

		public override NamespaceDeclaration Namespace => CurrentState?.Namespace;

		public override XamlNodeType NodeType => CurrentState?.NodeType ?? XamlNodeType.None;

		public override XamlSchemaContext SchemaContext => _context;

		public override XamlType Type => CurrentState?.Type;

		public override object Value
		{
			get
			{
				var state = CurrentState;
				if (state.NodeType == XamlNodeType.Value)
					return state.Value;
				return null;
			}
		}

		public override bool Read()
		{
			if (_enumerator != null)
				return isEof = !_enumerator.MoveNext();

			_enumerator = GetNodes().GetEnumerator();
			return isEof = !_enumerator.MoveNext();
		}


		XamlType LookupType(string typeName)
		{
			var prefixIdx = typeName.IndexOf(':');
			var name = typeName;
			string prefix = string.Empty;
			XamlType xamlType = null;
			if (prefixIdx > 0)
			{
				prefix = name.Substring(0, prefixIdx);
				name = name.Substring(prefixIdx + 1);
			}
			else
			{
				xamlType = _context.GetXamlType(typeName);
				if (!ReferenceEquals(xamlType, null))
					return xamlType;
			}

			// lookup namespace from prefix
			var ns = ResolveNamespace(prefix);
			if (ns == null)
				throw CreateException($"Prefix '{prefix}' is unknown in this context");

			xamlType = _context.GetXamlType(new XamlTypeName(ns, name));
			if (xamlType == null || xamlType.IsUnknown)
				throw CreateException($"Type '{{{ns}}}{typeName}' is unknown");
			return xamlType;
		}

		string ReadQuotedString()
		{
			if (_ch == '"')
			{
				sb_quoted.Clear();
				while (true)
				{
					ReadChar();
					if (_ch == '"' || _ch == -1)
						break;

					if (_ch != '\\')
					{
						sb_quoted.Append((char)_ch);
						continue;
					}

					ReadChar();
					if (_ch == -1)
						break;
					if (_ch == 'b')
						sb_quoted.Append('\b');
					else if (_ch == 'n')
						sb_quoted.Append('\n');
					else if (_ch == 'f')
						sb_quoted.Append('\f');
					else if (_ch == 'r')
						sb_quoted.Append('\r');
					else if (_ch == 't')
						sb_quoted.Append('\t');
					else if (_ch == 'u')
					{
						// todo: parse unicode ordinal
					}
					else
					{
						sb_quoted.Append('\\');
						sb_quoted.Append((char)_ch);
					}
				}
				ReadChar(); // skip ending double quote
				return sb_quoted.ToString();
			}
			throw CreateException("Excpected a quoted string");

		}

		void ReadWhitespace()
		{
			while (char.IsWhiteSpace((char)_ch))
			{
				ReadChar();
			}
		}

		/* *
		void ReadCharNoLine() => _ch = _reader.Read();
		Action ReadChar;
		/* *
		string buffer;
		int idx;
		void ReadCharNoLine() => _ch = idx < buffer.Length ? buffer[idx++] : -1;

		Action ReadChar;
		/* */

		void ReadChar()
		{
			_ch = _reader.Read();
			if (_settings.ProvideLineInfo)
			{
				if (_ch == '\n')
				{
					_lineNumber++;
					_linePosition = 0;
				}
				else
					_linePosition++;
			}
		}
		/* */


		void AddNamespace(string prefix, string ns, bool newNamespace)
		{
			if (namespaces == null)
				namespaces = new Stack<Dictionary<string, string>>();
			Dictionary<string, string> nsdic;
			if (namespaces.Count == 0 || newNamespace)
			{
				nsdic = namespaces.Count > 0 ? new Dictionary<string, string>(namespaces.Peek()) : new Dictionary<string, string>();
				namespaces.Push(nsdic);
			}
			else
				nsdic = namespaces.Peek();
			nsdic[prefix] = ns;
		}

		Dictionary<string, string> CurrentNamespaces => namespaces?.Count > 0 ? namespaces.Peek() : null;

		public bool HasLineInfo => true;

		public int LineNumber => _lineNumber;

		public int LinePosition => _linePosition;

		string ResolveNamespace(string prefix)
		{
			var curns = CurrentNamespaces;
			if (curns != null && curns.TryGetValue(prefix, out var ns))
				return ns;
			return null;
		}

		Exception CreateException(string message)
		{
			if (_settings.ProvideLineInfo)
				return new XamlParseException(message, null, _lineNumber + 1, _linePosition + 1);
			return new XamlParseException(message);
		}

		IEnumerable<StateObject> GetObjectOrArrayNodes(XamlType parentType, XamlMember parentMember, object keyValue = null)
		{
			ReadWhitespace();
			if (_ch == '{')
				return GetObjectNodes(parentType, parentMember, keyValue);
			else if (_ch == '[')
				return GetArrayNodes(parentType, parentMember, keyValue);
			else
				throw CreateException("Expected object {} or array []");
		}
		IEnumerable<StateObject> GetObjectNodes(XamlType parentType, XamlMember parentMember, object keyValue = null)
		{
			bool newNamespace = true;
			XamlType type = null;
			bool dictionaryStarted = false;
			// object!
			ReadChar(); // skip opening character
			while (true)
			{
				ReadWhitespace();
				var property = ReadQuotedString();
				if (string.IsNullOrEmpty(property))
					throw CreateException("Property name cannot be blank");
				ReadWhitespace();
				if (_ch != ':')
					throw CreateException("Expected a colon");
				ReadChar();
				ReadWhitespace();
				if (property[0] != '$')
				{
					if (ReferenceEquals(type, null))
					{
						type = parentType;
						yield return StateObject.WithStartOrGetObject(parentMember, type);
					}

					if (!type.IsDictionary)
					{
						var member = type.GetMember(property);
						if (ReferenceEquals(member, null))
							throw CreateException($"Could not find member '{property}' in type '{type}'");
						yield return StateObject.WithStartMember(member);
						foreach (var node in GetValueNodes(member.Type, member))
							yield return node;
						yield return StateObject.EndMember;
					}
					else
					{
						if (!dictionaryStarted)
						{
							yield return StateObject.WithStartMember(XamlLanguage.Items);
							dictionaryStarted = true;
						}
						foreach (var node in GetValueNodes(type.ItemType, XamlLanguage.Items, property))
							yield return node;
					}
				}
				else
				{
					if (string.Equals(property, "$ns", StringComparison.Ordinal) || property.StartsWith("$ns:", StringComparison.Ordinal))
					{
						var ns = ReadQuotedString();
						var prefix = property.Length > 4 ? property.Substring(4) : string.Empty;

						AddNamespace(prefix, ns, newNamespace);
						newNamespace = false;
						yield return StateObject.WithNamespace(ns, prefix);
					}
					else if (string.Equals(property, "$type", StringComparison.Ordinal))
					{
						var typeName = ReadQuotedString();
						var obj = StateObject.WithStartObject(LookupType(typeName));
						if (parentMember?.IsWritePublic == false)
							throw CreateException($"Cannot specify a '$type' for read only member '{parentMember}'");
						type = obj.Type;
						yield return obj;
					}
					else if (string.Equals(property, "$ref", StringComparison.Ordinal))
					{
						yield return StateObject.WithStartObject(XamlLanguage.Reference);
						yield return StateObject.WithStartMember(XamlLanguage.PositionalParameters);
						yield return StateObject.WithValue(ReadQuotedString());
						yield return StateObject.EndMember;
					}
				}
				ReadWhitespace();
				if (_ch == ',')
				{
					ReadChar();
					continue;
				}
				if (_ch == '}')
					break;
				throw CreateException($"Expecting ',' or '}}' but got '{(char)_ch}");
			}
			ReadChar(); // skip ending bracket

			if (dictionaryStarted)
				yield return StateObject.EndMember;

			if (keyValue != null)
			{
				yield return StateObject.WithStartMember(XamlLanguage.Key);
				yield return StateObject.WithValue(keyValue);
				yield return StateObject.EndMember;
			}

			yield return StateObject.EndObject;

			// we added namespaces in the context of this object, remove them!
			if (!newNamespace)
				namespaces.Pop();
		}

		IEnumerable<StateObject> GetArrayNodes(XamlType parentType, XamlMember parentMember, object keyValue = null)
		{
			if (ReferenceEquals(parentType, null))
				throw CreateException("Unknown type for array");

			yield return StateObject.WithStartOrGetObject(parentMember, parentType);

			// list/array
			yield return StateObject.WithStartMember(XamlLanguage.Items);
			ReadChar(); // skip opening character
			while (true)
			{
				ReadWhitespace();
				foreach (var node in GetValueNodes(parentType.ItemType, XamlLanguage.Items))
					yield return node;
				ReadWhitespace();
				if (_ch == ',')
				{
					ReadChar();
					continue;
				}
				if (_ch == ']')
					break;
				throw CreateException($"Expecting ',' but got '{(char)_ch}");
			}
			ReadChar(); // skip ending bracket
			yield return StateObject.EndMember;
			if (keyValue != null)
			{
				yield return StateObject.WithStartMember(XamlLanguage.Key);
				yield return StateObject.WithValue(keyValue);
				yield return StateObject.EndMember;
			}

			yield return StateObject.EndObject;
		}

		string ReadNumber()
		{
			sb_quoted.Clear();
			if (_ch == '-' || _ch == '+')
			{
				sb_quoted.Append((char)_ch);
				ReadChar();
			}
			while (char.IsNumber((char)_ch) || _ch == '.')
			{
				sb_quoted.Append((char)_ch);
				// numeric part
				ReadChar();
			}
			// read exponent
			if (_ch == 'e' || _ch == 'E')
			{
				sb_quoted.Append((char)_ch);
				ReadChar();
				if (_ch == '-' || _ch == '+')
				{
					sb_quoted.Append((char)_ch);
					// exponent
					ReadChar();
				}
				while (char.IsNumber((char)_ch))
				{
					sb_quoted.Append((char)_ch);
					// numeric part of exponent
					ReadChar();
				}
			}

			return sb_quoted.ToString();
		}

		bool ReadString(string val)
		{
			for (int i = 0; i < val.Length; i++)
			{
				if (_ch != val[i])
					return false;
				ReadChar();
			}
			return true;
		}


		bool ReadBoolean()
		{
			if (ReadString("true"))
			{
				ReadChar();
				return true;
			}
			if (ReadString("false"))
			{
				ReadChar();
				return false;
			}
			throw CreateException("Expected boolean value");
		}

		IEnumerable<StateObject> GetValueNodes(XamlType type, XamlMember member, object keyValue = null)
		{
			if (_ch == '{' || _ch == '[')
			{
				return GetObjectOrArrayNodes(type, member, keyValue);
			}
			return GetSimpleValueNodes(type, member, keyValue);
		}

		IEnumerable<StateObject> GetSimpleValueNodes(XamlType type, XamlMember member, object keyValue = null)
		{
			var hasKeyValue = !ReferenceEquals(keyValue, null);
			if (hasKeyValue)
			{
				yield return StateObject.WithStartObject(type);
				yield return StateObject.WithStartMember(XamlLanguage.Initialization);
			}
			if (_ch == '"')
			{
				yield return StateObject.WithValue(ReadQuotedString());
			}
			else if (_ch == 't' || _ch == 'f')
			{
				yield return StateObject.WithValue(ReadBoolean());
			}
			else if (char.IsNumber((char)_ch) || _ch == '-' || _ch == '+')
			{
				yield return StateObject.WithValue(ReadNumber());
			}
			else throw CreateException($"Invalid character {(char)_ch}");

			if (hasKeyValue)
			{
				yield return StateObject.EndMember;

				yield return StateObject.WithStartMember(XamlLanguage.Key);
				yield return StateObject.WithValue(keyValue);
				yield return StateObject.EndMember;
				yield return StateObject.EndObject;
			}
		}

		IEnumerable<StateObject> GetNodes()
		{
			//buffer = _reader.ReadToEnd();
			ReadChar(); // read first character
			return GetObjectOrArrayNodes(null, null);
		}
	}
}
