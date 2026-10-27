using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Portable.Xaml.Yaml
{
	/// <summary>
	/// Reads YAML as XAML nodes.
	/// </summary>
	/// <remarks>
	/// Mappings name their type with <c>$type</c> or a tag like <c>!Button</c>, or take the type of the member they are set to.
	/// <c>$ns</c> and <c>$ns:prefix</c> declare namespaces, <c>$name</c> sets x:Name, and <c>$ref</c> refers to a named object.
	/// <c>$items</c> gives the items of a collection that also has properties, and <c>$value</c> gives the text of a value that needs a <c>$type</c>.
	/// Strings such as <c>'{Binding Path}'</c> are markup extensions when they name one, and <c>{}</c> escapes the rest of a string.
	///
	/// Block and flow styles, comments, anchors and aliases (which copy the node), and merge keys (<c>&lt;&lt;</c>) are supported.
	/// Only one document is read.
	///
	/// Subclass to fill in what the YAML leaves out, e.g. <see cref="GetObjectType"/>.
	/// </remarks>
	[EnhancedXaml]
	public class XamlYamlReader : XamlReader, IXamlLineInfo
	{
		readonly TextReader _reader;
		readonly XamlSchemaContext _context;
		readonly XamlYamlReaderSettings _settings;
		readonly StringBuilder _sb = new StringBuilder(50);
		Dictionary<string, XamlDataNode> _anchors;
		string _text;
		int _pos;
		int _line = 1;
		int _lineStart;
		List<XamlDataNodeInfo> _nodes;
		int _index = -1;
		int _nodeEstimate;
		XamlDataNodeInfo _current;
		bool _isEof;

		class Source : IXamlDataReaderSource
		{
			readonly XamlYamlReader _reader;

			public Source(XamlYamlReader reader) => _reader = reader;

			public XamlType GetObjectType(XamlType expectedType, IList<string> propertyNames) => _reader.GetObjectType(expectedType, propertyNames);

			public XamlMember GetArrayMember(XamlType expectedType, out XamlType objectType) => _reader.GetArrayMember(expectedType, out objectType);

			public XamlMember GetTextMember(XamlType expectedType, out XamlType objectType) => _reader.GetTextMember(expectedType, out objectType);
		}

		public XamlYamlReader(Stream stream)
			: this(stream, new XamlSchemaContext(), null)
		{
		}

		public XamlYamlReader(TextReader reader)
			: this(reader, new XamlSchemaContext(), null)
		{
		}

		public XamlYamlReader(TextReader reader, XamlSchemaContext context)
			: this(reader, context, null)
		{
		}

		public XamlYamlReader(Stream stream, XamlSchemaContext context)
			: this(stream, context, null)
		{
		}

		public XamlYamlReader(Stream stream, XamlSchemaContext context, XamlYamlReaderSettings settings)
			: this(new StreamReader(stream), context, settings)
		{
		}

		public XamlYamlReader(TextReader textReader, XamlSchemaContext context, XamlYamlReaderSettings settings)
		{
			_reader = textReader ?? throw new ArgumentNullException(nameof(textReader));
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_settings = settings ?? XamlYamlReaderSettings.Instance;
		}

		public override bool IsEof => _isEof;

		public override XamlMember Member => _current.Value as XamlMember;

		public override NamespaceDeclaration Namespace => _current.Value as NamespaceDeclaration;

		public override XamlNodeType NodeType => _current.NodeType;

		public override XamlSchemaContext SchemaContext => _context;

		public override XamlType Type => _current.NodeType == XamlNodeType.StartObject ? _current.Value as XamlType : null;

		public override object Value => _current.NodeType == XamlNodeType.Value ? _current.Value : null;

		public bool HasLineInfo => _settings.ProvideLineInfo;

		public int LineNumber => _current.Line;

		public int LinePosition => _current.Column;

		public override bool Read()
		{
			if (_isEof)
				return false;
			if (_nodes == null)
				_nodes = GetNodes();
			if (++_index < _nodes.Count)
			{
				_current = _nodes[_index];
				return true;
			}
			_current = default(XamlDataNodeInfo);
			_nodes = null;
			_isEof = true;
			return false;
		}

		/// <summary>Gets the type to create for a mapping that has no <c>$type</c>.</summary>
		/// <param name="expectedType">Type of the member or collection item the mapping is for, or the root type.</param>
		/// <param name="propertyNames">Names of the mapping's keys, e.g. to tell a table from one of its rows.</param>
		/// <returns>The type to create, or null when it can't be known.</returns>
		protected virtual XamlType GetObjectType(XamlType expectedType, IList<string> propertyNames) => expectedType;

		/// <summary>Gets an object to create and its collection to fill when a sequence is given where <paramref name="expectedType"/> isn't a collection.</summary>
		/// <remarks>E.g. a layout and its rows for a cell given as a sequence.</remarks>
		/// <param name="expectedType">Type of the member or collection item the sequence is for.</param>
		/// <param name="objectType">Type of object to create, or null for the member's declaring type.</param>
		/// <returns>The collection member, or null when a sequence isn't allowed.</returns>
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

		List<XamlDataNodeInfo> GetNodes()
		{
			_text = _reader.ReadToEnd().Replace("\r\n", "\n").Replace('\r', '\n');
			if (_text.Length > 0 && _text[0] == (char)0xFEFF)
				_pos = _lineStart = 1;
			var root = ReadDocument();
			var reader = new XamlDataNodeReader(_context, new Source(this), _settings.LocalAssembly, _settings.DefaultNamespaces);
			return reader.GetNodes(root, _settings.RootType, _nodeEstimate);
		}

		#region Parsing

		int Ch => _pos < _text.Length ? _text[_pos] : -1;

		int PeekAt(int offset) => _pos + offset < _text.Length ? _text[_pos + offset] : -1;

		int Column => _pos - _lineStart + 1;

		/// <summary>Spaces before the content of the current line.</summary>
		int Indent => _pos - _lineStart;

		static bool IsSpace(int ch) => ch == ' ' || ch == '\t';

		static bool IsBreakOrEnd(int ch) => ch == '\n' || ch == -1;

		static bool IsSpaceOrEnd(int ch) => IsSpace(ch) || IsBreakOrEnd(ch);

		static bool IsFlowIndicator(int ch) => ch == ',' || ch == '[' || ch == ']' || ch == '{' || ch == '}';

		bool AtLineEnd => IsBreakOrEnd(Ch);

		void Next()
		{
			if (_text[_pos] == '\n')
			{
				_line++;
				_lineStart = _pos + 1;
			}
			_pos++;
		}

		void SkipSpaces()
		{
			while (IsSpace(Ch))
				_pos++;
		}

		/// <summary>Skips spaces and a comment to the end of the line.</summary>
		void SkipToLineEnd()
		{
			SkipSpaces();
			if (Ch == '#')
			{
				while (!AtLineEnd)
					_pos++;
			}
		}

		/// <summary>Moves to the content of the next line that isn't blank or a comment, or to the end.</summary>
		void SkipToContent()
		{
			while (true)
			{
				SkipToLineEnd();
				if (Ch != '\n')
					break;
				Next();
			}
			CheckIndent();
		}

		/// <summary>Throws when the indent of the current line has a tab.</summary>
		void CheckIndent()
		{
			var tab = -1;
			for (int i = _lineStart; i < _pos; i++)
			{
				if (_text[i] == '\t')
					tab = i;
				else if (_text[i] != ' ')
					return;
			}
			if (tab >= 0)
				throw CreateException("Tabs can't be used to indent", _line, tab - _lineStart + 1);
		}

		bool IsDocumentMarker(char ch) => Indent == 0 && Ch == ch && PeekAt(1) == ch && PeekAt(2) == ch && IsSpaceOrEnd(PeekAt(3));

		bool IsDocumentMarker() => IsDocumentMarker('-') || IsDocumentMarker('.');

		bool IsSequenceEntry() => Ch == '-' && IsSpaceOrEnd(PeekAt(1));

		XamlDataNode ReadDocument()
		{
			SkipToContent();
			// directives like %YAML only matter to the version, which isn't checked
			while (Ch == '%' && Indent == 0)
			{
				while (!AtLineEnd)
					_pos++;
				SkipToContent();
			}
			if (IsDocumentMarker('-'))
				_pos += 3;
			else if (Ch == -1)
				throw CreateException("Expected a value");

			var root = ReadBlockNode(-1, false);
			SkipToContent();
			if (IsDocumentMarker('.'))
			{
				_pos += 3;
				SkipToContent();
			}
			if (Ch != -1)
				throw CreateException(IsDocumentMarker('-') ? "Only one document is supported" : "Expected the end of the document");
			if (root.Kind == XamlDataKind.Null && root.TypeName == null)
				throw CreateException("Expected a value", root.Line, root.Column);
			return root;
		}

		XamlDataNode NewNode(XamlDataKind kind, int line, int column)
		{
			// most values become a start and end node
			_nodeEstimate += 2;
			var node = new XamlDataNode { Kind = kind, Line = line, Column = column };
			if (kind == XamlDataKind.Object)
				node.Properties = new List<XamlDataProperty>();
			else if (kind == XamlDataKind.Array)
				node.Items = new List<XamlDataNode>();
			return node;
		}

		/// <summary>Reads a node after a key's colon, a sequence's dash, or at the start of the document.</summary>
		/// <param name="parentIndent">Indent of the mapping or sequence the node is in, which a block node must be indented past.</param>
		/// <param name="isMappingValue">True after a key, where a sequence can be at the same indent as the key.</param>
		XamlDataNode ReadBlockNode(int parentIndent, bool isMappingValue)
		{
			SkipSpaces();
			var line = _line;
			var column = Column;
			ReadProperties(false, out var anchor, out var tag);
			SkipToLineEnd();

			XamlDataNode node;
			if (AtLineEnd)
			{
				SkipToContent();
				var indent = Indent;
				if (Ch != -1 && !IsDocumentMarker() && (indent > parentIndent || (isMappingValue && indent == parentIndent && IsSequenceEntry())))
				{
					if (IsSequenceEntry())
						node = ReadBlockSequence(indent);
					else if (IsImplicitKey())
						node = ReadBlockMapping(indent);
					else
						node = ReadInlineNode(parentIndent);
				}
				else
					node = NewNode(XamlDataKind.Null, line, column);
			}
			else if (Ch == '|' || Ch == '>')
				node = ReadBlockScalar(parentIndent);
			else if (!isMappingValue && IsSequenceEntry())
				node = ReadBlockSequence(Indent);
			else if (!isMappingValue && IsImplicitKey())
				node = ReadBlockMapping(Indent);
			else
				node = ReadInlineNode(parentIndent);

			return ApplyProperties(node, anchor, tag);
		}

		/// <summary>Reads a flow collection, quoted or plain scalar, or alias in a block, which must end its line.</summary>
		XamlDataNode ReadInlineNode(int parentIndent)
		{
			XamlDataNode node;
			if (Ch == '[' || Ch == '{' || Ch == '"' || Ch == '\'' || Ch == '*')
			{
				node = ReadFlowNode();
				SkipToLineEnd();
				if (!AtLineEnd)
					throw CreateException($"Unexpected '{(char)Ch}' after the value");
			}
			else
			{
				node = NewNode(XamlDataKind.String, _line, Column);
				node.Text = ReadPlain(parentIndent, false, false);
				node.Kind = Resolve(ref node.Text);
				if (Ch == ':')
					throw CreateException("Unexpected ':'; check the indent, or quote the text");
			}
			return node;
		}

		/// <summary>Reads anchors (<c>&amp;name</c>) and tags (<c>!Type</c>) before a node.</summary>
		void ReadProperties(bool inFlow, out string anchor, out string tag)
		{
			anchor = null;
			tag = null;
			while (true)
			{
				if (Ch == '&')
				{
					_pos++;
					anchor = ReadAnchorName(inFlow);
				}
				else if (Ch == '!')
				{
					_pos++;
					if (Ch == '<')
					{
						var end = _text.IndexOf('>', _pos);
						if (end < 0)
							throw CreateException("Expected '>' to end the tag");
						tag = _text.Substring(_pos + 1, end - _pos - 1);
						_pos = end + 1;
					}
					else
						tag = "!" + ReadName(inFlow);
				}
				else
					return;
				if (inFlow)
					SkipFlowSpace();
				else
					SkipSpaces();
			}
		}

		string ReadName(bool inFlow)
		{
			var start = _pos;
			while (!IsSpaceOrEnd(Ch) && !(inFlow && IsFlowIndicator(Ch)))
				_pos++;
			return _text.Substring(start, _pos - start);
		}

		string ReadAnchorName(bool inFlow)
		{
			var name = ReadName(inFlow);
			if (name.Length == 0)
				throw CreateException("Expected the name of the anchor");
			return name;
		}

		XamlDataNode ApplyProperties(XamlDataNode node, string anchor, string tag)
		{
			if (tag != null)
			{
				// standard tags like !!int only say what the YAML already does, except !!str
				if (tag == "!!str")
				{
					if (node.Kind != XamlDataKind.Object && node.Kind != XamlDataKind.Array)
					{
						node.Kind = XamlDataKind.String;
						node.Text = node.Text ?? string.Empty;
					}
				}
				else if (!tag.StartsWith("!!", StringComparison.Ordinal))
				{
					node.TypeName = tag.StartsWith("!", StringComparison.Ordinal) ? tag.Substring(1) : tag;
					// a tag without a value is an object with no properties
					if (node.Kind == XamlDataKind.Null)
					{
						node.Kind = XamlDataKind.Object;
						node.Properties = new List<XamlDataProperty>();
					}
				}
			}
			if (anchor != null)
			{
				if (_anchors == null)
					_anchors = new Dictionary<string, XamlDataNode>();
				_anchors[anchor] = node;
			}
			return node;
		}

		/// <summary>Whether the current line has a key and colon from the current position.</summary>
		bool IsImplicitKey()
		{
			var i = _pos;
			if (i >= _text.Length)
				return false;
			var ch = _text[i];
			if (ch == '"' || ch == '\'')
			{
				i++;
				while (i < _text.Length && _text[i] != '\n')
				{
					if (_text[i] == '\\' && ch == '"')
						i++;
					else if (_text[i] == ch)
					{
						if (ch == '\'' && i + 1 < _text.Length && _text[i + 1] == '\'')
							i++;
						else
							break;
					}
					i++;
				}
				i++;
				while (i < _text.Length && IsSpace(_text[i]))
					i++;
				return i < _text.Length && _text[i] == ':' && (i + 1 >= _text.Length || IsSpaceOrEnd(_text[i + 1]));
			}
			if (IsFlowIndicator(ch) || ch == '#' || ch == '&' || ch == '*' || ch == '!' || ch == '|' || ch == '>' || ch == '%' || ch == '@' || ch == '`')
				return false;
			for (; i < _text.Length && _text[i] != '\n'; i++)
			{
				ch = _text[i];
				if (ch == ':' && (i + 1 >= _text.Length || IsSpaceOrEnd(_text[i + 1])))
					return true;
				if (ch == '#' && i > _pos && IsSpace(_text[i - 1]))
					return false;
			}
			return false;
		}

		XamlDataNode ReadBlockMapping(int indent)
		{
			var node = NewNode(XamlDataKind.Object, _line, Column);
			XamlDataProperty? merge = null;
			while (true)
			{
				var property = new XamlDataProperty { Line = _line, Column = Column };
				var quoted = Ch == '"' || Ch == '\'';
				property.Name = quoted ? ReadQuoted() : ReadPlain(indent, false, true);
				SkipSpaces();
				if (Ch != ':')
					throw CreateException("Expected a colon after the key");
				_pos++;
				property.Value = ReadBlockNode(indent, true);
				AddProperty(node, property, quoted, ref merge);

				SkipToContent();
				if (Ch == -1 || IsDocumentMarker() || Indent < indent)
					break;
				if (Indent > indent)
					throw CreateException("The indent doesn't match the keys before it");
				if (IsSequenceEntry())
					break;
				if (!IsImplicitKey())
					throw CreateException("Expected a key and colon");
			}
			ApplyMerges(node, merge);
			return node;
		}

		XamlDataNode ReadBlockSequence(int indent)
		{
			var node = NewNode(XamlDataKind.Array, _line, Column);
			while (true)
			{
				_pos++;
				node.Items.Add(ReadBlockNode(indent, false));

				SkipToContent();
				if (Ch == -1 || IsDocumentMarker() || Indent < indent)
					break;
				if (Indent > indent)
					throw CreateException("The indent doesn't match the items before it");
				if (!IsSequenceEntry())
					break;
			}
			return node;
		}

		/// <summary>Adds a mapping's key, or keeps it in <paramref name="merge"/> when it is a merge key.</summary>
		void AddProperty(XamlDataNode node, XamlDataProperty property, bool quoted, ref XamlDataProperty? merge)
		{
			// only a plain << merges, so a dictionary can still have it as a quoted key
			if (!quoted && property.Name == "<<")
			{
				if (merge != null)
					throw CreateException("Duplicate key '<<'", property.Line, property.Column);
				merge = property;
				return;
			}
			AddProperty(node, property);
		}

		void AddProperty(XamlDataNode node, XamlDataProperty property)
		{
			if (node.TryFind(property.Name, out _))
				throw CreateException($"Duplicate key '{property.Name}'", property.Line, property.Column);
			node.Properties.Add(property);
			// properties become a start and end member
			_nodeEstimate += 2;
		}

		/// <summary>Adds the keys of mappings given with <c>&lt;&lt;</c> that the mapping doesn't have itself.</summary>
		void ApplyMerges(XamlDataNode node, XamlDataProperty? mergeProperty)
		{
			if (mergeProperty == null)
				return;
			var merge = mergeProperty.Value;
			var sources = merge.Value.Kind == XamlDataKind.Array ? merge.Value.Items : new List<XamlDataNode> { merge.Value };
			foreach (var source in sources)
			{
				if (source.Kind != XamlDataKind.Object)
					throw CreateException("Expected a mapping to merge", merge.Value.Line, merge.Value.Column);
				foreach (var property in source.Properties)
				{
					if (!node.TryFind(property.Name, out _))
						node.Properties.Add(property);
				}
			}
		}

		/// <summary>Reads a scalar without quotes, which can continue on lines indented past <paramref name="parentIndent"/>.</summary>
		string ReadPlain(int parentIndent, bool inFlow, bool isKey)
		{
			_sb.Clear();
			var start = _pos;
			ReadPlainLine(inFlow);
			_sb.Append(_text, start, _pos - start);
			TrimEnd(_sb);
			if (isKey || inFlow)
				return _sb.ToString();

			while (AtLineEnd && Ch != -1)
			{
				var pos = _pos;
				var line = _line;
				var lineStart = _lineStart;
				var breaks = 0;
				while (Ch == '\n')
				{
					Next();
					breaks++;
					SkipSpaces();
				}
				CheckIndent();
				if (Ch == -1 || Ch == '#' || Indent <= parentIndent || IsDocumentMarker())
				{
					_pos = pos;
					_line = line;
					_lineStart = lineStart;
					break;
				}
				if (breaks == 1)
					_sb.Append(' ');
				else
					_sb.Append('\n', breaks - 1);
				start = _pos;
				ReadPlainLine(false);
				_sb.Append(_text, start, _pos - start);
				TrimEnd(_sb);
			}
			return _sb.ToString();
		}

		/// <summary>Moves to the end of a plain scalar on the current line.</summary>
		void ReadPlainLine(bool inFlow)
		{
			while (true)
			{
				var ch = Ch;
				if (IsBreakOrEnd(ch))
					return;
				if (ch == ':' && (IsSpaceOrEnd(PeekAt(1)) || (inFlow && IsFlowIndicator(PeekAt(1)))))
					return;
				if (ch == '#' && _pos > 0 && IsSpace(_text[_pos - 1]))
				{
					// leave the spaces before the comment to be trimmed
					return;
				}
				if (inFlow && IsFlowIndicator(ch))
					return;
				_pos++;
			}
		}

		static void TrimEnd(StringBuilder sb)
		{
			var length = sb.Length;
			while (length > 0 && IsSpace(sb[length - 1]))
				length--;
			sb.Length = length;
		}

		/// <summary>Gets the kind of a plain scalar like <c>null</c>, <c>true</c> or <c>5</c>, and changes YAML-only numbers like <c>.inf</c> to .NET text.</summary>
		internal static XamlDataKind Resolve(ref string text)
		{
			switch (text)
			{
				case "":
				case "~":
				case "null":
				case "Null":
				case "NULL":
					return XamlDataKind.Null;
				case "true":
				case "True":
				case "TRUE":
				case "false":
				case "False":
				case "FALSE":
					return XamlDataKind.Boolean;
				case ".inf":
				case ".Inf":
				case ".INF":
				case "+.inf":
				case "+.Inf":
				case "+.INF":
					text = "Infinity";
					return XamlDataKind.Number;
				case "-.inf":
				case "-.Inf":
				case "-.INF":
					text = "-Infinity";
					return XamlDataKind.Number;
				case ".nan":
				case ".NaN":
				case ".NAN":
					text = "NaN";
					return XamlDataKind.Number;
			}
			return IsNumber(text) ? XamlDataKind.Number : XamlDataKind.String;
		}

		/// <summary>Whether the text is an integer, hex, octal or decimal number as YAML reads them.</summary>
		static bool IsNumber(string text)
		{
			var i = 0;
			if (text.Length > 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'o'))
			{
				var hex = text[1] == 'x';
				for (i = 2; i < text.Length; i++)
				{
					var c = text[i];
					if (!(c >= '0' && c <= (hex ? '9' : '7')) && !(hex && ((c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))))
						return false;
				}
				return true;
			}
			if (i < text.Length && (text[i] == '-' || text[i] == '+'))
				i++;
			var digits = 0;
			while (i < text.Length && char.IsDigit(text[i]))
			{
				i++;
				digits++;
			}
			if (i < text.Length && text[i] == '.')
			{
				i++;
				while (i < text.Length && char.IsDigit(text[i]))
				{
					i++;
					digits++;
				}
			}
			if (digits == 0)
				return false;
			if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
			{
				i++;
				if (i < text.Length && (text[i] == '-' || text[i] == '+'))
					i++;
				var exponent = i;
				while (i < text.Length && char.IsDigit(text[i]))
					i++;
				if (i == exponent)
					return false;
			}
			return i == text.Length;
		}

		/// <summary>Reads a literal (<c>|</c>) or folded (<c>&gt;</c>) scalar.</summary>
		XamlDataNode ReadBlockScalar(int parentIndent)
		{
			var node = NewNode(XamlDataKind.String, _line, Column);
			var literal = Ch == '|';
			_pos++;
			var chomp = ' ';
			var indentIndicator = 0;
			for (int i = 0; i < 2; i++)
			{
				if (Ch == '-' || Ch == '+')
					chomp = (char)Ch;
				else if (Ch >= '1' && Ch <= '9')
					indentIndicator = Ch - '0';
				else
					break;
				_pos++;
			}
			SkipToLineEnd();
			if (!AtLineEnd)
				throw CreateException($"Unexpected '{(char)Ch}' after the block scalar indicator");

			var lines = new List<string>();
			var contentIndent = indentIndicator > 0 ? Math.Max(parentIndent, 0) + indentIndicator : -1;
			var hasFinalBreak = false;
			while (Ch == '\n')
			{
				var pos = _pos;
				var line = _line;
				var lineStart = _lineStart;
				Next();
				if (Ch == -1)
					break;
				var spaces = 0;
				while (Ch == ' ' && (contentIndent < 0 || spaces < contentIndent))
				{
					_pos++;
					spaces++;
				}
				if (AtLineEnd)
				{
					// blank lines don't set the indent, but can have spaces past it
					lines.Add(string.Empty);
					continue;
				}
				if (contentIndent < 0)
				{
					while (Ch == ' ')
					{
						_pos++;
						spaces++;
					}
					contentIndent = spaces;
				}
				if (spaces < contentIndent || spaces <= parentIndent || (spaces == 0 && IsDocumentMarker()))
				{
					_pos = pos;
					_line = line;
					_lineStart = lineStart;
					break;
				}
				var start = _pos;
				while (!AtLineEnd)
					_pos++;
				lines.Add(_text.Substring(start, _pos - start));
				hasFinalBreak = Ch == '\n';
			}

			var trailing = 0;
			while (lines.Count > trailing && lines[lines.Count - 1 - trailing].Length == 0)
				trailing++;
			var count = lines.Count - trailing;
			_sb.Clear();
			for (int i = 0; i < count; i++)
			{
				var line = lines[i];
				_sb.Append(line);
				if (i == count - 1)
					break;
				if (literal || line.Length == 0)
				{
					_sb.Append('\n');
					continue;
				}
				// folded lines join with a space, unless blank or indented further
				var next = i + 1;
				while (next < count && lines[next].Length == 0)
					next++;
				if (IsSpace(line[0]) || IsSpace(lines[next][0]))
					_sb.Append('\n');
				else if (next == i + 1)
					_sb.Append(' ');
			}
			if (chomp != '-' && count > 0 && (hasFinalBreak || trailing > 0))
				_sb.Append('\n');
			if (chomp == '+')
				_sb.Append('\n', count > 0 ? trailing : Math.Max(trailing - 1, 0));
			node.Text = _sb.ToString();
			return node;
		}

		void SkipFlowSpace()
		{
			while (true)
			{
				SkipToLineEnd();
				if (Ch != '\n')
					return;
				Next();
			}
		}

		XamlDataNode ReadFlowNode()
		{
			SkipFlowSpace();
			var line = _line;
			var column = Column;
			ReadProperties(true, out var anchor, out var tag);
			XamlDataNode node;
			switch (Ch)
			{
				case '[':
					node = ReadFlowSequence();
					break;
				case '{':
					node = ReadFlowMapping();
					break;
				case '"':
				case '\'':
					node = NewNode(XamlDataKind.String, _line, Column);
					node.Text = ReadQuoted();
					break;
				case '*':
					_pos++;
					var name = ReadAnchorName(true);
					if (_anchors == null || !_anchors.TryGetValue(name, out node))
						throw CreateException($"Unknown anchor '{name}'", line, column);
					// copied so a tag on the alias doesn't change the anchored node
					node = node.Clone();
					break;
				case ',':
				case ']':
				case '}':
				case -1:
					node = NewNode(XamlDataKind.Null, line, column);
					break;
				default:
					node = NewNode(XamlDataKind.String, _line, Column);
					node.Text = ReadPlain(-1, true, false);
					node.Kind = Resolve(ref node.Text);
					break;
			}
			return ApplyProperties(node, anchor, tag);
		}

		XamlDataNode ReadFlowSequence()
		{
			var node = NewNode(XamlDataKind.Array, _line, Column);
			_pos++;
			while (true)
			{
				SkipFlowSpace();
				if (Ch == ']')
					break;
				var item = ReadFlowNode();
				SkipFlowSpace();
				// a single key and value in a sequence is a mapping
				if (Ch == ':')
				{
					_pos++;
					var pair = NewNode(XamlDataKind.Object, item.Line, item.Column);
					AddProperty(pair, new XamlDataProperty { Name = item.Text ?? string.Empty, Value = ReadFlowValue(), Line = item.Line, Column = item.Column });
					item = pair;
				}
				node.Items.Add(item);
				if (!ReadFlowSeparator(']'))
					break;
			}
			_pos++;
			return node;
		}

		XamlDataNode ReadFlowMapping()
		{
			var node = NewNode(XamlDataKind.Object, _line, Column);
			XamlDataProperty? merge = null;
			_pos++;
			while (true)
			{
				SkipFlowSpace();
				if (Ch == '}')
					break;
				var property = new XamlDataProperty { Line = _line, Column = Column };
				var quoted = Ch == '"' || Ch == '\'';
				property.Name = quoted ? ReadQuoted() : ReadPlain(-1, true, true);
				SkipFlowSpace();
				if (Ch == ':')
				{
					_pos++;
					property.Value = ReadFlowValue();
				}
				else
					property.Value = NewNode(XamlDataKind.Null, property.Line, property.Column);
				AddProperty(node, property, quoted, ref merge);
				if (!ReadFlowSeparator('}'))
					break;
			}
			_pos++;
			ApplyMerges(node, merge);
			return node;
		}

		XamlDataNode ReadFlowValue()
		{
			SkipFlowSpace();
			return ReadFlowNode();
		}

		/// <summary>Reads a comma, if any, and returns true when there may be more items before the end.</summary>
		bool ReadFlowSeparator(char end)
		{
			SkipFlowSpace();
			if (Ch == ',')
			{
				_pos++;
				return true;
			}
			if (Ch == end)
				return false;
			throw CreateException(Ch == -1 ? $"Expected ',' or '{end}'" : $"Expected ',' or '{end}' but got '{(char)Ch}'");
		}

		/// <summary>Reads a single or double quoted scalar, which can span lines.</summary>
		string ReadQuoted()
		{
			var quote = Ch;
			_pos++;
			_sb.Clear();
			while (true)
			{
				var ch = Ch;
				if (ch == -1)
					throw CreateException("Expected the end of the string");
				if (ch == quote)
				{
					_pos++;
					if (quote == '\'' && Ch == '\'')
					{
						_sb.Append('\'');
						_pos++;
						continue;
					}
					return _sb.ToString();
				}
				if (ch == '\n')
				{
					// line breaks fold to a space, or to line breaks when there are blank lines
					TrimEnd(_sb);
					var breaks = 0;
					while (Ch == '\n')
					{
						Next();
						breaks++;
						SkipSpaces();
					}
					if (breaks == 1)
						_sb.Append(' ');
					else
						_sb.Append('\n', breaks - 1);
					continue;
				}
				if (ch == '\\' && quote == '"')
				{
					_pos++;
					ReadEscape();
					continue;
				}
				_sb.Append((char)ch);
				_pos++;
			}
		}

		void ReadEscape()
		{
			var ch = Ch;
			_pos++;
			switch (ch)
			{
				case '0': _sb.Append('\0'); break;
				case 'a': _sb.Append('\a'); break;
				case 'b': _sb.Append('\b'); break;
				case 't':
				case '\t': _sb.Append('\t'); break;
				case 'n': _sb.Append('\n'); break;
				case 'v': _sb.Append('\v'); break;
				case 'f': _sb.Append('\f'); break;
				case 'r': _sb.Append('\r'); break;
				case 'e': _sb.Append('\x1B'); break;
				case 'N': _sb.Append('\x85'); break;
				case '_': _sb.Append('\xA0'); break;
				case 'L': _sb.Append((char)0x2028); break;
				case 'P': _sb.Append((char)0x2029); break;
				case 'x': ReadHex(2); break;
				case 'u': ReadHex(4); break;
				case 'U': ReadHex(8); break;
				case '\n':
					// an escaped line break joins the lines without a space
					_pos--;
					Next();
					SkipSpaces();
					break;
				case -1:
					throw CreateException("Expected the end of the string");
				default:
					// covers \\, \", \/ and \ followed by a space
					_sb.Append((char)ch);
					break;
			}
		}

		void ReadHex(int length)
		{
			if (_pos + length > _text.Length || !int.TryParse(_text.Substring(_pos, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
				throw CreateException($"Expected {length} hex digits");
			_pos += length;
			if (code > 0xFFFF)
				_sb.Append(char.ConvertFromUtf32(code));
			else
				_sb.Append((char)code);
		}

		#endregion

		Exception CreateException(string message) => CreateException(message, _line, Column);

		Exception CreateException(string message, int line, int column) => new XamlParseException(message, null, line, column);
	}
}
