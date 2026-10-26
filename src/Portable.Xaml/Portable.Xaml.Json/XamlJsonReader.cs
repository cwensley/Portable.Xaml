using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
		// read in blocks, as a virtual TextReader.Read() per character is most of the parsing time
		readonly char[] _buffer = new char[1024];
		int _bufferPosition;
		int _bufferLength;
		List<XamlDataNodeInfo> _nodes;
		int _index = -1;
		int _nodeEstimate;
		XamlDataNodeInfo _current;
		bool _isEof;
		int _ch;
		int _line = 1;
		int _column;
		bool _newLine;

		class Source : IXamlDataReaderSource
		{
			readonly XamlJsonReader _reader;

			public Source(XamlJsonReader reader) => _reader = reader;

			public XamlType GetObjectType(XamlType expectedType, IList<string> propertyNames) => _reader.GetObjectType(expectedType, propertyNames);

			public XamlMember GetArrayMember(XamlType expectedType, out XamlType objectType) => _reader.GetArrayMember(expectedType, out objectType);

			public XamlMember GetTextMember(XamlType expectedType, out XamlType objectType) => _reader.GetTextMember(expectedType, out objectType);
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

		// the whole document is parsed before the first node, so generate all nodes at once rather than through nested iterators
		List<XamlDataNodeInfo> GetNodes()
		{
			ReadChar();
			SkipWhitespace();
			var root = ReadValue();
			SkipWhitespace();
			if (_ch != -1)
				throw CreateException("Expected the end of the document");
			if (root.Kind != XamlDataKind.Object && root.Kind != XamlDataKind.Array)
				throw CreateException("Expected an object {} or array []", root);
			var reader = new XamlDataNodeReader(_context, new Source(this), _settings.LocalAssembly, _settings.DefaultNamespaces);
			return reader.GetNodes(root, _settings.RootType, _nodeEstimate);
		}

		#region Parsing

		void ReadChar()
		{
			if (_newLine)
			{
				_line++;
				_column = 0;
			}
			if (_bufferPosition >= _bufferLength)
			{
				_bufferLength = _reader.Read(_buffer, 0, _buffer.Length);
				_bufferPosition = 0;
			}
			_ch = _bufferPosition < _bufferLength ? _buffer[_bufferPosition++] : -1;
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

		XamlDataNode ReadValue()
		{
			var node = new XamlDataNode { Line = _line, Column = _column };
			// most values become a start and end node, and properties add a start and end member
			_nodeEstimate += 2;
			if (_ch == '{')
			{
				node.Kind = XamlDataKind.Object;
				node.Properties = new List<XamlDataProperty>();
				ReadChar();
				while (true)
				{
					SkipWhitespace();
					if (_ch == '}')
						break;
					var property = new XamlDataProperty { Line = _line, Column = _column };
					property.Name = _ch == '"' || _ch == '\'' ? ReadQuotedString() : ReadName();
					SkipWhitespace();
					if (_ch != ':')
						throw CreateException("Expected a colon");
					ReadChar();
					SkipWhitespace();
					property.Value = ReadValue();
					node.Properties.Add(property);
					_nodeEstimate += 2;
					if (!ReadSeparator('}'))
						break;
				}
				ReadChar();
			}
			else if (_ch == '[')
			{
				node.Kind = XamlDataKind.Array;
				node.Items = new List<XamlDataNode>();
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
				node.Kind = XamlDataKind.String;
				node.Text = ReadQuotedString();
			}
			else if (_ch == '-' || _ch == '+' || _ch == '.' || (_ch >= '0' && _ch <= '9'))
			{
				node.Kind = XamlDataKind.Number;
				node.Text = ReadNumber();
			}
			else
			{
				var word = ReadName();
				if (word == "true" || word == "false")
				{
					node.Kind = XamlDataKind.Boolean;
					node.Text = word;
				}
				else if (word == "null")
					node.Kind = XamlDataKind.Null;
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
			// most strings are in the buffer without escapes or line breaks, so take them directly
			var start = _bufferPosition;
			for (int i = start; i < _bufferLength; i++)
			{
				var c = _buffer[i];
				if (c == quote)
				{
					var value = new string(_buffer, start, i - start);
					_column += i - start;
					_bufferPosition = i;
					ReadChar();
					ReadChar();
					return value;
				}
				if (c == '\\' || c == '\n')
					break;
			}

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

		Exception CreateException(string message, XamlDataNode node) => CreateException(message, node.Line, node.Column);

		Exception CreateException(string message) => CreateException(message, _line, _column);

		Exception CreateException(string message, int line, int column) => new XamlParseException(message, null, line, column);
	}
}
