using System;
using System.IO;
using System.Text;

namespace Portable.Xaml.Yaml
{
	class XamlYamlWriterInternal : XamlDataNodeWriter
	{
		readonly TextWriter _writer;
		readonly XamlYamlWriter _source;
		readonly XamlYamlWriterSettings _settings;

		public XamlYamlWriterInternal(XamlYamlWriter source, XamlYamlWriterSettings settings, TextWriter writer, XamlSchemaContext schemaContext, XamlWriterStateManager manager)
			: base(schemaContext, manager)
		{
			_source = source;
			_writer = writer;
			_settings = settings;
		}

		protected override void WriteDocument(XamlDataNode root)
		{
			if (root.Kind == XamlDataKind.Object && root.Properties.Count > 0)
				WriteMapping(root, 0, false);
			else if (root.Kind == XamlDataKind.Array && root.Items.Count > 0)
				WriteSequence(root, 0, false);
			else
			{
				WriteInline(root);
				_writer.WriteLine();
			}
		}

		void WriteMapping(XamlDataNode node, int indent, bool firstInline)
		{
			for (int i = 0; i < node.Properties.Count; i++)
			{
				var property = node.Properties[i];
				if (i > 0 || !firstInline)
					WriteIndent(indent);
				WriteScalar(property.Name, XamlDataKind.String);
				_writer.Write(':');
				WriteValue(property.Value, indent, false);
			}
		}

		void WriteSequence(XamlDataNode node, int indent, bool firstInline)
		{
			for (int i = 0; i < node.Items.Count; i++)
			{
				if (i > 0 || !firstInline)
					WriteIndent(indent);
				_writer.Write('-');
				WriteValue(node.Items[i], indent, true);
			}
		}

		/// <summary>Writes a value after a key's colon or a sequence's dash, to the end of its last line.</summary>
		void WriteValue(XamlDataNode node, int indent, bool inSequence)
		{
			if (node.Kind == XamlDataKind.Object && node.Properties.Count > 0)
			{
				// a mapping in a sequence starts on the same line as its dash
				if (inSequence)
					_writer.Write(' ');
				else
					_writer.WriteLine();
				WriteMapping(node, indent + 2, inSequence);
			}
			else if (node.Kind == XamlDataKind.Array && node.Items.Count > 0)
			{
				if (inSequence)
					_writer.Write(' ');
				else
					_writer.WriteLine();
				WriteSequence(node, indent + 2, inSequence);
			}
			else if (node.Kind != XamlDataKind.String || !TryWriteLiteral(node.Text, indent))
			{
				_writer.Write(' ');
				WriteInline(node);
				_writer.WriteLine();
			}
		}

		void WriteInline(XamlDataNode node)
		{
			switch (node.Kind)
			{
				case XamlDataKind.Object:
					_writer.Write("{}");
					break;
				case XamlDataKind.Array:
					_writer.Write("[]");
					break;
				case XamlDataKind.Null:
					_writer.Write("null");
					break;
				default:
					WriteScalar(node.Text, node.Kind);
					break;
			}
		}

		void WriteIndent(int indent) => _writer.Write(new string(' ', indent));

		/// <summary>Writes multi-line text as a literal block, so it reads as it is.</summary>
		bool TryWriteLiteral(string text, int indent)
		{
			if (text.IndexOf('\n') < 0)
				return false;
			foreach (var ch in text)
			{
				if (ch != '\n' && ch != '\t' && !IsPrintable(ch))
					return false;
			}
			var core = text.TrimEnd('\n');
			if (core.Trim('\n', ' ').Length == 0)
				return false;

			_writer.Write(" |");
			// an indicator is needed when the first line's own indent would be taken as the block's
			var firstLine = core.TrimStart('\n');
			if (firstLine[0] == ' ')
				_writer.Write('2');
			var trailing = text.Length - core.Length;
			if (trailing == 0)
				_writer.Write('-');
			else if (trailing > 1)
				_writer.Write('+');
			_writer.WriteLine();
			foreach (var line in core.Split('\n'))
			{
				if (line.Length > 0)
				{
					WriteIndent(indent + 2);
					_writer.Write(line);
				}
				_writer.WriteLine();
			}
			for (int i = 1; i < trailing; i++)
				_writer.WriteLine();
			return true;
		}

		void WriteScalar(string text, XamlDataKind kind)
		{
			if (!NeedsQuotes(text, kind))
				_writer.Write(text);
			else if (CanSingleQuote(text))
			{
				_writer.Write('\'');
				_writer.Write(text.Replace("'", "''"));
				_writer.Write('\'');
			}
			else
				WriteDoubleQuoted(text);
		}

		static bool NeedsQuotes(string text, XamlDataKind kind)
		{
			if (text.Length == 0)
				return true;
			var resolved = text;
			var resolvedKind = XamlYamlReader.Resolve(ref resolved);
			if (resolvedKind == XamlDataKind.Null)
				return true;
			if (kind == XamlDataKind.String && (resolvedKind != XamlDataKind.String || IsOldBoolean(text)))
				return true;

			var first = text[0];
			if (IsSpace(first) || IsSpace(text[text.Length - 1]))
				return true;
			// "-5" or "-foo" are fine, but "- foo" is a sequence
			if (first == '-' || first == '?' || first == ':')
			{
				if (text.Length == 1 || IsSpace(text[1]))
					return true;
			}
			else if ("[]{},#&*!|>'\"%@`".IndexOf(first) >= 0)
				return true;
			if (text.StartsWith("---", StringComparison.Ordinal) || text.StartsWith("...", StringComparison.Ordinal))
				return true;

			for (int i = 0; i < text.Length; i++)
			{
				var ch = text[i];
				if (!IsPrintable(ch))
					return true;
				if (ch == ':' && (i + 1 == text.Length || IsSpace(text[i + 1])))
					return true;
				if (ch == '#' && IsSpace(text[i - 1]))
					return true;
			}
			return false;
		}

		/// <summary>YAML 1.1 readers take these as booleans.</summary>
		static bool IsOldBoolean(string text)
		{
			switch (text.ToLowerInvariant())
			{
				case "yes":
				case "no":
				case "on":
				case "off":
				case "y":
				case "n":
					return true;
			}
			return false;
		}

		static bool IsSpace(char ch) => ch == ' ' || ch == '\t';

		static bool IsPrintable(char ch) => ch >= ' ' && ch != '\x7F' && ch != '\x85' && ch != (char)0x2028 && ch != (char)0x2029 && ch != (char)0xFEFF;

		static bool CanSingleQuote(string text)
		{
			foreach (var ch in text)
			{
				if (!IsPrintable(ch))
					return false;
			}
			return true;
		}

		void WriteDoubleQuoted(string text)
		{
			var sb = new StringBuilder(text.Length + 2);
			sb.Append('"');
			foreach (var ch in text)
			{
				switch (ch)
				{
					case '"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\0': sb.Append("\\0"); break;
					case '\a': sb.Append("\\a"); break;
					case '\b': sb.Append("\\b"); break;
					case '\t': sb.Append("\\t"); break;
					case '\n': sb.Append("\\n"); break;
					case '\v': sb.Append("\\v"); break;
					case '\f': sb.Append("\\f"); break;
					case '\r': sb.Append("\\r"); break;
					case '\x1B': sb.Append("\\e"); break;
					case '\x85': sb.Append("\\N"); break;
					case (char)0x2028: sb.Append("\\L"); break;
					case (char)0x2029: sb.Append("\\P"); break;
					default:
						if (IsPrintable(ch))
							sb.Append(ch);
						else
							sb.Append("\\u").Append(((int)ch).ToString("X4"));
						break;
				}
			}
			sb.Append('"');
			_writer.Write(sb.ToString());
		}

		protected override XamlException CreateException(string message) => WithLineInfo(new XamlYamlWriterException(message));

		public void Flush() => _writer.Flush();

		public override void CloseAll()
		{
			base.CloseAll();

			_writer.Flush();
			if (_settings.CloseOutput)
				_writer.Dispose();
		}

		protected override XamlException WithLineInfo(XamlException ex)
		{
			ex.SetLineInfo(_source.Line, _source.Column);
			return ex;
		}
	}

	/// <summary>
	/// Writes XAML nodes as YAML that <see cref="XamlYamlReader"/> can read.
	/// </summary>
	/// <remarks>
	/// Objects are written when they end, as how an object is written depends on all of its members.
	/// </remarks>
	[EnhancedXaml]
	public class XamlYamlWriter : XamlWriter, IXamlLineInfoConsumer
	{
		readonly XamlSchemaContext _schemaContext;
		readonly XamlYamlWriterSettings _settings;
		readonly XamlYamlWriterInternal _intl;

		public override XamlSchemaContext SchemaContext => _schemaContext;

		public XamlYamlWriterSettings Settings => _settings;

		public XamlYamlWriter(Stream stream, XamlSchemaContext schemaContext)
			: this(stream, schemaContext, null)
		{
		}

		public XamlYamlWriter(Stream stream, XamlSchemaContext schemaContext, XamlYamlWriterSettings settings)
			: this(new StreamWriter(stream), schemaContext, settings)
		{
		}

		public XamlYamlWriter(TextWriter writer, XamlSchemaContext schemaContext)
			: this(writer, schemaContext, null)
		{
		}

		public XamlYamlWriter(TextWriter writer, XamlSchemaContext schemaContext, XamlYamlWriterSettings settings)
		{
			if (writer == null)
				throw new ArgumentNullException(nameof(writer));
			_schemaContext = schemaContext ?? throw new ArgumentNullException(nameof(schemaContext));
			_settings = settings ?? new XamlYamlWriterSettings();

			var manager = new XamlWriterStateManager<XamlYamlWriterException, InvalidOperationException>(true);
			_intl = new XamlYamlWriterInternal(this, _settings, writer, _schemaContext, manager);
		}

		internal int Line { get; private set; }
		internal int Column { get; private set; }

		public bool ShouldProvideLineInfo => true;

		public void SetLineInfo(int lineNumber, int linePosition)
		{
			Line = lineNumber;
			Column = linePosition;
		}

		public override void WriteEndMember() => _intl.WriteEndMember();

		public override void WriteEndObject() => _intl.WriteEndObject();

		public override void WriteGetObject() => _intl.WriteGetObject();

		public override void WriteNamespace(NamespaceDeclaration namespaceDeclaration) => _intl.WriteNamespace(namespaceDeclaration);

		public override void WriteStartMember(XamlMember xamlMember) => _intl.WriteStartMember(xamlMember);

		public override void WriteStartObject(XamlType type) => _intl.WriteStartObject(type);

		public override void WriteValue(object value) => _intl.WriteValue(value);

		public void Flush() => _intl.Flush();

		protected override void Dispose(bool disposing)
		{
			if (!disposing)
				return;

			_intl.CloseAll();
		}
	}
}
