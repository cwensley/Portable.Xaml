using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;
using Portable.Xaml.Markup;
using Portable.Xaml.Schema;

namespace Portable.Xaml.Json
{
	/// <summary>
	/// Reads JSON as XAML nodes.
	/// </summary>
	/// <remarks>
	/// Objects name their type with <c>"$type"</c>, or take the type of the member they are set to.
	/// <c>"$ns"</c> and <c>"$ns:prefix"</c> declare namespaces, <c>"$name"</c> sets x:Name, and <c>"$ref"</c> refers to a named object.
	/// Strings such as <c>"{Binding Path}"</c> are markup extensions when they name one, and <c>"{}"</c> escapes the rest of a string.
	///
	/// The JSON can be relaxed: comments, unquoted property names, single quotes, and trailing commas are allowed.
	///
	/// Subclass to fill in what the JSON leaves out, e.g. <see cref="GetObjectType"/>.
	/// </remarks>
	[EnhancedXaml]
	public class XamlJsonReader : XamlReader, IXamlLineInfo
	{
		readonly TextReader _reader;
		readonly XamlSchemaContext _context;
		readonly XamlJsonReaderSettings _settings;
		readonly StringBuilder _text = new StringBuilder(50);
		readonly Stack<Dictionary<string, string>> _namespaces = new Stack<Dictionary<string, string>>();
		IEnumerator<NodeInfo> _enumerator;
		NodeInfo _current;
		bool _isEof;
		int _ch;
		int _line = 1;
		int _column;
		bool _newLine;

		enum JsonKind
		{
			Object,
			Array,
			String,
			Number,
			Boolean,
			Null
		}

		class JsonNode
		{
			public JsonKind Kind;
			public string Text;
			public List<JsonProperty> Properties;
			public List<JsonNode> Items;
			public int Line;
			public int Column;

			public JsonProperty Find(string name) => Properties?.FirstOrDefault(r => r.Name == name);
		}

		class JsonProperty
		{
			public string Name;
			public JsonNode Value;
			public int Line;
			public int Column;
		}

		class NodeInfo
		{
			public XamlNodeType NodeType;
			public object Value;
			public int Line;
			public int Column;
		}

		class NamespaceResolver : IXamlNamespaceResolver
		{
			readonly XamlJsonReader _reader;

			public NamespaceResolver(XamlJsonReader reader) => _reader = reader;

			public string GetNamespace(string prefix) => _reader.ResolveNamespace(prefix ?? string.Empty);

			public IEnumerable<NamespaceDeclaration> GetNamespacePrefixes() => _reader._namespaces.Peek().Select(r => new NamespaceDeclaration(_reader.ResolveNamespace(r.Key), r.Key));
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
			: this(new StreamReader(stream), context, settings)
		{
		}

		public XamlJsonReader(TextReader textReader, XamlSchemaContext context, XamlJsonReaderSettings settings)
		{
			_reader = textReader ?? throw new ArgumentNullException(nameof(textReader));
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_settings = settings ?? XamlJsonReaderSettings.Instance;
		}

		public override bool IsEof => _isEof;

		public override XamlMember Member => _current?.Value as XamlMember;

		public override NamespaceDeclaration Namespace => _current?.Value as NamespaceDeclaration;

		public override XamlNodeType NodeType => _current?.NodeType ?? XamlNodeType.None;

		public override XamlSchemaContext SchemaContext => _context;

		public override XamlType Type => _current?.NodeType == XamlNodeType.StartObject ? _current.Value as XamlType : null;

		public override object Value => _current?.NodeType == XamlNodeType.Value ? _current.Value : null;

		public bool HasLineInfo => _settings.ProvideLineInfo;

		public int LineNumber => _current?.Line ?? 0;

		public int LinePosition => _current?.Column ?? 0;

		public override bool Read()
		{
			if (_enumerator == null)
				_enumerator = GetNodes().GetEnumerator();
			if (_enumerator.MoveNext())
			{
				_current = _enumerator.Current;
				return true;
			}
			_current = null;
			_isEof = true;
			return false;
		}

		/// <summary>Gets the type to create for an object that has no <c>$type</c>.</summary>
		/// <param name="expectedType">Type of the member or collection item the object is for, or the root type.</param>
		/// <param name="propertyNames">Names of the object's properties, e.g. to tell a table from one of its rows.</param>
		/// <returns>The type to create, or null when it can't be known.</returns>
		protected virtual XamlType GetObjectType(XamlType expectedType, IList<string> propertyNames) => expectedType;

		/// <summary>Gets an object to create and its collection to fill when an array is given where <paramref name="expectedType"/> isn't a collection.</summary>
		/// <remarks>E.g. a layout and its rows for a cell given as an array.</remarks>
		/// <param name="expectedType">Type of the member or collection item the array is for.</param>
		/// <param name="objectType">Type of object to create, or null for the member's declaring type.</param>
		/// <returns>The collection member, or null when an array isn't allowed.</returns>
		protected virtual XamlMember GetArrayMember(XamlType expectedType, out XamlType objectType)
		{
			objectType = null;
			return null;
		}

		/// <summary>Gets an object to create and its member to set when a string is given where <paramref name="expectedType"/> can't be made from text.</summary>
		/// <remarks>E.g. a label and its text for a cell given as a string.</remarks>
		/// <param name="expectedType">Type of the member or collection item the string is for.</param>
		/// <param name="objectType">Type of object to create, or null for the member's declaring type.</param>
		/// <returns>The member, or null to pass the text on as is.</returns>
		protected virtual XamlMember GetTextMember(XamlType expectedType, out XamlType objectType)
		{
			objectType = null;
			return null;
		}

		IEnumerable<NodeInfo> GetNodes()
		{
			_namespaces.Push(_settings.DefaultNamespaces.ToDictionary(r => r.Key, r => r.Value));
			ReadChar();
			SkipWhitespace();
			var root = ReadValue();
			SkipWhitespace();
			if (_ch != -1)
				throw CreateException("Expected the end of the document");
			if (root.Kind != JsonKind.Object && root.Kind != JsonKind.Array)
				throw CreateException("Expected an object {} or array []", root);
			return WriteValue(root, _settings.RootType, null);
		}

		#region Parsing

		void ReadChar()
		{
			if (_newLine)
			{
				_line++;
				_column = 0;
			}
			_ch = _reader.Read();
			_column++;
			_newLine = _ch == '\n';
		}

		void SkipWhitespace()
		{
			while (true)
			{
				if (_ch == '﻿' || (_ch != -1 && char.IsWhiteSpace((char)_ch)))
					ReadChar();
				else if (_ch == '/')
				{
					ReadChar();
					if (_ch == '/')
					{
						while (_ch != -1 && _ch != '\n')
							ReadChar();
					}
					else if (_ch == '*')
					{
						ReadChar();
						while (true)
						{
							if (_ch == -1)
								throw CreateException("Expected the end of the comment");
							var star = _ch == '*';
							ReadChar();
							if (star && _ch == '/')
							{
								ReadChar();
								break;
							}
						}
					}
					else
						throw CreateException("Expected a comment");
				}
				else
					return;
			}
		}

		JsonNode ReadValue()
		{
			var node = new JsonNode { Line = _line, Column = _column };
			if (_ch == '{')
			{
				node.Kind = JsonKind.Object;
				node.Properties = new List<JsonProperty>();
				ReadChar();
				while (true)
				{
					SkipWhitespace();
					if (_ch == '}')
						break;
					var property = new JsonProperty { Line = _line, Column = _column };
					property.Name = _ch == '"' || _ch == '\'' ? ReadQuotedString() : ReadName();
					SkipWhitespace();
					if (_ch != ':')
						throw CreateException("Expected a colon");
					ReadChar();
					SkipWhitespace();
					property.Value = ReadValue();
					node.Properties.Add(property);
					if (!ReadSeparator('}'))
						break;
				}
				ReadChar();
			}
			else if (_ch == '[')
			{
				node.Kind = JsonKind.Array;
				node.Items = new List<JsonNode>();
				ReadChar();
				while (true)
				{
					SkipWhitespace();
					if (_ch == ']')
						break;
					node.Items.Add(ReadValue());
					if (!ReadSeparator(']'))
						break;
				}
				ReadChar();
			}
			else if (_ch == '"' || _ch == '\'')
			{
				node.Kind = JsonKind.String;
				node.Text = ReadQuotedString();
			}
			else if (_ch == '-' || _ch == '+' || _ch == '.' || (_ch >= '0' && _ch <= '9'))
			{
				node.Kind = JsonKind.Number;
				node.Text = ReadNumber();
			}
			else
			{
				var word = ReadName();
				if (word == "true" || word == "false")
				{
					node.Kind = JsonKind.Boolean;
					node.Text = word;
				}
				else if (word == "null")
					node.Kind = JsonKind.Null;
				else
					throw CreateException($"Unexpected '{word}'", node);
			}
			return node;
		}

		/// <summary>Reads a comma, if any, and returns true when there may be more items before the end.</summary>
		bool ReadSeparator(char end)
		{
			SkipWhitespace();
			if (_ch == ',')
			{
				ReadChar();
				return true;
			}
			if (_ch == end)
				return false;
			throw CreateException(_ch == -1 ? $"Expected ',' or '{end}'" : $"Expected ',' or '{end}' but got '{(char)_ch}'");
		}

		string ReadName()
		{
			_text.Clear();
			while (_ch != -1 && (char.IsLetterOrDigit((char)_ch) || _ch == '_' || _ch == '$' || _ch == '.'))
			{
				_text.Append((char)_ch);
				ReadChar();
			}
			if (_text.Length == 0)
				throw CreateException(_ch == -1 ? "Unexpected end of the document" : $"Unexpected '{(char)_ch}'");
			return _text.ToString();
		}

		string ReadNumber()
		{
			_text.Clear();
			while (_ch != -1 && (char.IsLetterOrDigit((char)_ch) || _ch == '-' || _ch == '+' || _ch == '.'))
			{
				_text.Append((char)_ch);
				ReadChar();
			}
			return _text.ToString();
		}

		string ReadQuotedString()
		{
			var quote = _ch;
			_text.Clear();
			ReadChar();
			while (_ch != quote)
			{
				if (_ch == -1)
					throw CreateException("Expected the end of the string");
				if (_ch != '\\')
				{
					_text.Append((char)_ch);
					ReadChar();
					continue;
				}
				ReadChar();
				switch (_ch)
				{
					case 'b': _text.Append('\b'); break;
					case 'f': _text.Append('\f'); break;
					case 'n': _text.Append('\n'); break;
					case 'r': _text.Append('\r'); break;
					case 't': _text.Append('\t'); break;
					case 'u':
						var hex = new char[4];
						for (int i = 0; i < hex.Length; i++)
						{
							ReadChar();
							hex[i] = (char)_ch;
						}
						if (!int.TryParse(new string(hex), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
							throw CreateException("Expected four hex digits after \\u");
						_text.Append((char)code);
						break;
					case -1:
						throw CreateException("Expected the end of the string");
					default:
						_text.Append((char)_ch);
						break;
				}
				ReadChar();
			}
			ReadChar();
			return _text.ToString();
		}

		#endregion

		#region Nodes

		static NodeInfo Node(XamlNodeType nodeType, object value, int line, int column)
			=> new NodeInfo { NodeType = nodeType, Value = value, Line = line, Column = column };

		static NodeInfo Node(XamlNodeType nodeType, object value, JsonNode node) => Node(nodeType, value, node.Line, node.Column);

		IEnumerable<NodeInfo> WriteValue(JsonNode node, XamlType expectedType, XamlMember member, string key = null)
		{
			switch (node.Kind)
			{
				case JsonKind.Object:
					return WriteObject(node, expectedType, member, key);
				case JsonKind.Array:
					return WriteArray(node, expectedType, member, key);
				case JsonKind.Null:
					return WriteNull(node, key);
				default:
					return WriteText(node, expectedType, key);
			}
		}

		IEnumerable<NodeInfo> WriteNull(JsonNode node, string key)
		{
			yield return Node(XamlNodeType.StartObject, XamlLanguage.Null, node);
			foreach (var n in WriteKey(node, key))
				yield return n;
			yield return Node(XamlNodeType.EndObject, null, node);
		}

		IEnumerable<NodeInfo> WriteKey(JsonNode node, string key)
		{
			if (key == null)
				yield break;
			yield return Node(XamlNodeType.StartMember, XamlLanguage.Key, node);
			yield return Node(XamlNodeType.Value, key, node);
			yield return Node(XamlNodeType.EndMember, null, node);
		}

		IEnumerable<NodeInfo> WriteObject(JsonNode node, XamlType expectedType, XamlMember member, string key)
		{
			var hasNamespaces = false;
			foreach (var property in node.Properties)
			{
				if (property.Name != "$ns" && !property.Name.StartsWith("$ns:", StringComparison.Ordinal))
					continue;
				if (property.Value.Kind != JsonKind.String)
					throw CreateException("Expected a namespace string", property.Value);
				if (!hasNamespaces)
				{
					_namespaces.Push(new Dictionary<string, string>(_namespaces.Peek()));
					hasNamespaces = true;
				}
				var prefix = property.Name.Length > 4 ? property.Name.Substring(4) : string.Empty;
				_namespaces.Peek()[prefix] = property.Value.Text;
				yield return Node(XamlNodeType.NamespaceDeclaration, new NamespaceDeclaration(property.Value.Text, prefix), property.Line, property.Column);
			}

			var reference = node.Find("$ref");
			if (reference != null)
			{
				yield return Node(XamlNodeType.StartObject, XamlLanguage.Reference, node);
				yield return Node(XamlNodeType.StartMember, XamlLanguage.PositionalParameters, reference.Line, reference.Column);
				yield return Node(XamlNodeType.Value, GetText(reference.Value), reference.Value);
				yield return Node(XamlNodeType.EndMember, null, reference.Value);
			}
			else
			{
				XamlType type;
				var typeProperty = node.Find("$type");
				var isGetObject = false;
				if (typeProperty != null)
				{
					if (IsReadOnly(member))
						throw CreateException($"Cannot specify a '$type' for read only member '{member}'", typeProperty.Value);
					type = LookupType(GetText(typeProperty.Value), typeProperty.Value);
				}
				else
				{
					var names = node.Properties.Select(r => r.Name).Where(r => !r.StartsWith("$", StringComparison.Ordinal)).ToList();
					type = GetObjectType(expectedType, names);
					if (ReferenceEquals(type, null))
						throw CreateException("Add a '$type' to say what type of object to create", node);
					isGetObject = IsReadOnly(member);
				}

				yield return isGetObject
					? Node(XamlNodeType.GetObject, null, node)
					: Node(XamlNodeType.StartObject, type, node);

				var itemsStarted = false;
				foreach (var property in node.Properties)
				{
					if (property.Name == "$name")
					{
						yield return Node(XamlNodeType.StartMember, XamlLanguage.Name, property.Line, property.Column);
						yield return Node(XamlNodeType.Value, GetText(property.Value), property.Value);
						yield return Node(XamlNodeType.EndMember, null, property.Value);
						continue;
					}
					if (property.Name.StartsWith("$", StringComparison.Ordinal))
						continue;

					if (type.IsDictionary)
					{
						if (!itemsStarted)
						{
							yield return Node(XamlNodeType.StartMember, XamlLanguage.Items, property.Line, property.Column);
							itemsStarted = true;
						}
						foreach (var n in WriteValue(property.Value, type.ItemType, XamlLanguage.Items, property.Name))
							yield return n;
						continue;
					}

					var propertyMember = LookupMember(type, property);
					yield return Node(XamlNodeType.StartMember, propertyMember, property.Line, property.Column);
					foreach (var n in WriteValue(property.Value, propertyMember.Type, propertyMember))
						yield return n;
					yield return Node(XamlNodeType.EndMember, null, property.Value);
				}
				if (itemsStarted)
					yield return Node(XamlNodeType.EndMember, null, node);
			}

			foreach (var n in WriteKey(node, key))
				yield return n;
			yield return Node(XamlNodeType.EndObject, null, node);

			if (hasNamespaces)
				_namespaces.Pop();
		}

		static bool IsReadOnly(XamlMember member) => member != null && !member.IsDirective && !member.IsWritePublic;

		IEnumerable<NodeInfo> WriteArray(JsonNode node, XamlType expectedType, XamlMember member, string key)
		{
			if (ReferenceEquals(expectedType, null))
				throw CreateException("Add a '$type' to say what type of collection to create", node);

			if (!expectedType.IsCollection)
			{
				var collectionMember = GetArrayMember(expectedType, out var objectType);
				if (ReferenceEquals(collectionMember, null))
					throw CreateException($"An array can't be used for '{expectedType}'", node);

				yield return Node(XamlNodeType.StartObject, objectType ?? collectionMember.DeclaringType, node);
				yield return Node(XamlNodeType.StartMember, collectionMember, node);
				foreach (var n in WriteArray(node, collectionMember.Type, collectionMember, null))
					yield return n;
				yield return Node(XamlNodeType.EndMember, null, node);
				foreach (var n in WriteKey(node, key))
					yield return n;
				yield return Node(XamlNodeType.EndObject, null, node);
				yield break;
			}

			// like XAML, fill the collection a property already has rather than replacing it
			yield return member != null && !member.IsDirective
				? Node(XamlNodeType.GetObject, null, node)
				: Node(XamlNodeType.StartObject, expectedType, node);
			if (node.Items.Count > 0)
			{
				yield return Node(XamlNodeType.StartMember, XamlLanguage.Items, node);
				foreach (var item in node.Items)
				{
					foreach (var n in WriteValue(item, expectedType.ItemType, XamlLanguage.Items))
						yield return n;
				}
				yield return Node(XamlNodeType.EndMember, null, node);
			}
			foreach (var n in WriteKey(node, key))
				yield return n;
			yield return Node(XamlNodeType.EndObject, null, node);
		}

		IEnumerable<NodeInfo> WriteText(JsonNode node, XamlType expectedType, string key)
		{
			var text = node.Text;
			if (node.Kind == JsonKind.String && text.Length > 0 && text[0] == '{')
			{
				if (text.Length > 1 && text[1] == '}')
					text = text.Substring(2);
				else
				{
					var markup = ParseMarkup(text);
					if (markup != null)
					{
						foreach (var n in WriteMarkup(markup, node, key))
							yield return n;
						yield break;
					}
				}
			}

			XamlType textObjectType = null;
			var textMember = node.Kind == JsonKind.String && !ReferenceEquals(expectedType, null) ? GetTextMember(expectedType, out textObjectType) : null;
			if (!ReferenceEquals(textMember, null))
			{
				yield return Node(XamlNodeType.StartObject, textObjectType ?? textMember.DeclaringType, node);
				yield return Node(XamlNodeType.StartMember, textMember, node);
				yield return Node(XamlNodeType.Value, text, node);
				yield return Node(XamlNodeType.EndMember, null, node);
			}
			else if (key != null)
			{
				if (ReferenceEquals(expectedType, null))
					throw CreateException("Unknown type for dictionary item", node);
				yield return Node(XamlNodeType.StartObject, expectedType, node);
				yield return Node(XamlNodeType.StartMember, XamlLanguage.Initialization, node);
				yield return Node(XamlNodeType.Value, text, node);
				yield return Node(XamlNodeType.EndMember, null, node);
			}
			else
			{
				yield return Node(XamlNodeType.Value, text, node);
				yield break;
			}

			foreach (var n in WriteKey(node, key))
				yield return n;
			yield return Node(XamlNodeType.EndObject, null, node);
		}

		/// <summary>Parses a markup extension, or returns null when the text doesn't name one so it is used as is.</summary>
		ParsedMarkupExtensionInfo ParseMarkup(string text)
		{
			try
			{
				var info = new ParsedMarkupExtensionInfo(text, new NamespaceResolver(this), _context);
				info.Parse();
				return info.Type.IsUnknown || !info.Type.IsMarkupExtension ? null : info;
			}
			catch (XamlParseException)
			{
				return null;
			}
		}

		IEnumerable<NodeInfo> WriteMarkup(ParsedMarkupExtensionInfo info, JsonNode node, string key)
		{
			yield return Node(XamlNodeType.StartObject, info.Type, node);
			foreach (var argument in info.Arguments)
			{
				yield return Node(XamlNodeType.StartMember, argument.Key, node);
				var values = argument.Value as List<object> ?? new List<object> { argument.Value };
				foreach (var value in values)
				{
					if (value is ParsedMarkupExtensionInfo nested)
					{
						foreach (var n in WriteMarkup(nested, node, null))
							yield return n;
					}
					else
						yield return Node(XamlNodeType.Value, value, node);
				}
				yield return Node(XamlNodeType.EndMember, null, node);
			}
			foreach (var n in WriteKey(node, key))
				yield return n;
			yield return Node(XamlNodeType.EndObject, null, node);
		}

		string GetText(JsonNode node)
		{
			if (node.Kind == JsonKind.Object || node.Kind == JsonKind.Array || node.Kind == JsonKind.Null)
				throw CreateException("Expected a string", node);
			return node.Text;
		}

		#endregion

		#region Lookup

		string ResolveNamespace(string prefix)
		{
			if (_namespaces.Peek().TryGetValue(prefix, out var ns))
			{
				// like XamlXmlReader, a clr-namespace without an assembly is in the local assembly
				if (_settings.LocalAssembly != null && ns.StartsWith("clr-namespace:", StringComparison.Ordinal) && ns.IndexOf(';') == -1)
					ns += ";assembly=" + _settings.LocalAssembly.GetName().Name;
				return ns;
			}
			return null;
		}

		/// <summary>Looks up "Type", "prefix:Type", "Namespace.Type" or "Namespace.Type, Assembly".</summary>
		XamlType LookupType(string typeName, JsonNode node)
		{
			string ns;
			string name;
			var assemblyIndex = typeName.IndexOf(',');
			var prefixIndex = typeName.IndexOf(':');
			if (assemblyIndex > 0 || (prefixIndex < 0 && typeName.IndexOf('.') > 0))
			{
				var fullName = assemblyIndex > 0 ? typeName.Substring(0, assemblyIndex).Trim() : typeName;
				var dotIndex = fullName.LastIndexOf('.');
				name = fullName.Substring(dotIndex + 1);
				ns = "clr-namespace:" + (dotIndex > 0 ? fullName.Substring(0, dotIndex) : string.Empty);
				if (assemblyIndex > 0)
					ns += ";assembly=" + typeName.Substring(assemblyIndex + 1).Trim();
				else if (_settings.LocalAssembly != null)
					ns += ";assembly=" + _settings.LocalAssembly.GetName().Name;
			}
			else
			{
				var prefix = prefixIndex > 0 ? typeName.Substring(0, prefixIndex) : string.Empty;
				name = prefixIndex > 0 ? typeName.Substring(prefixIndex + 1) : typeName;
				ns = ResolveNamespace(prefix);
				if (ns == null)
				{
					// without a namespace, the type can only be found by its CLR name
					var xamlType = prefixIndex < 0 ? _context.GetXamlType(typeName) : null;
					if (!ReferenceEquals(xamlType, null))
						return xamlType;
					throw CreateException($"Prefix '{prefix}' is unknown in this context", node);
				}
			}

			var type = _context.GetXamlType(new XamlTypeName(ns, name));
			if (ReferenceEquals(type, null) || type.IsUnknown)
				throw CreateException($"Type '{typeName}' is unknown", node);
			return type;
		}

		/// <summary>Looks up "Member", "Type.AttachedMember", "prefix:Type.AttachedMember" or a directive like "x:Key".</summary>
		XamlMember LookupMember(XamlType type, JsonProperty property)
		{
			var name = property.Name;
			var prefixIndex = name.IndexOf(':');
			string ns = null;
			if (prefixIndex > 0)
			{
				var prefix = name.Substring(0, prefixIndex);
				ns = ResolveNamespace(prefix) ?? throw CreateException($"Prefix '{prefix}' is unknown in this context", property);
				name = name.Substring(prefixIndex + 1);
			}

			XamlMember member = null;
			var dotIndex = name.IndexOf('.');
			if (dotIndex > 0)
			{
				var ownerName = name.Substring(0, dotIndex);
				var owner = ns != null ? _context.GetXamlType(new XamlTypeName(ns, ownerName)) : LookupType(ownerName, property.Value);
				member = owner?.GetAttachableMember(name.Substring(dotIndex + 1));
			}
			else if (ns != null)
			{
				member = ns == XamlLanguage.Xaml2006Namespace
					? XamlLanguage.AllDirectives.FirstOrDefault(r => r.Name == name)
					: new XamlDirective(ns, name);
			}
			else
				member = type.GetMember(name) ?? type.GetAttachableMember(name);

			if (ReferenceEquals(member, null))
				throw CreateException($"Could not find member '{property.Name}' in type '{type}'", property);
			return member;
		}

		#endregion

		Exception CreateException(string message, JsonNode node) => CreateException(message, node.Line, node.Column);

		Exception CreateException(string message, JsonProperty property) => CreateException(message, property.Line, property.Column);

		Exception CreateException(string message) => CreateException(message, _line, _column);

		Exception CreateException(string message, int line, int column) => new XamlParseException(message, null, line, column);
	}
}
