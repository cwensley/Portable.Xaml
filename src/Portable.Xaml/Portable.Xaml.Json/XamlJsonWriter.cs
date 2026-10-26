using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Portable.Xaml.Markup;
using System.Reflection;

namespace Portable.Xaml.Json
{
	class XamlJsonWriterInternal : XamlWriterInternalBase
	{
		TextWriter _writer;
		XamlJsonWriter _source;
		XamlJsonWriterSettings _settings;
		Stack<TextWriter> _writers = new Stack<TextWriter>();

		public XamlJsonWriterInternal(XamlJsonWriter source, XamlJsonWriterSettings settings, TextWriter writer, XamlSchemaContext schemaContext, XamlWriterStateManager manager)
			: base(schemaContext, manager)
		{
			_source = source;
			_writer = writer;
			_settings = settings;
		}

		protected override void OnWriteEndMember()
		{
		}

		protected override void OnWriteEndObject()
		{
			var state = CurrentState;
			var type = state.Type;
			if (!directTypes.Contains(type))
			{
				var member = LastState?.CurrentMember;
				if (member?.Type?.IsCollection == true && member?.Type?.IsDictionary != true && state.IsGetObject)
					_writer.Write("]");
				else
				{
					_writer.Write("}");
					if (type.IsMarkupExtension && !ReferenceEquals(type, XamlLanguage.Reference))
						_writer.Write("\"");
				}
			}

			// dictionary items are written aside until their key is known, including strings and nulls
			var last = LastState;
			var parentType = last?.Type;
			if (parentType?.IsDictionary == true && _writers.Count > 0)
			{
				var oldWriter = _writer;
				_writer = _writers.Pop();
				if (parentType.KeyType.ValueSerializer.ConverterInstance.CanConvertToString(state.KeyValue, null))
				{
					var str = parentType.KeyType.ValueSerializer.ConverterInstance.ConvertToString(state.KeyValue, null);
					WritePropertyName(str, last);
				}
				_writer.Write(oldWriter);
			}
		}

		protected override void OnWriteGetObject()
		{
			var lastState = LastState;
			if (lastState != null)
			{
				var member = lastState.CurrentMemberState.Member;
				if (!member.Type.IsCollection && !member.Type.IsDictionary)
					throw new InvalidOperationException(String.Format("WriteGetObject method can be invoked only when current member '{0}' is of collection or dictionary type", CurrentMember));
				if (member.Type.IsCollection || lastState.Type.IsDictionary)
				{
					_writer.Write("[");
					return;
				}
			}
			_writer.Write("{");
		}

		List<NamespaceDeclaration> namespaces = new List<NamespaceDeclaration>();

		protected override void OnWriteNamespace(NamespaceDeclaration nd)
		{
			if (_settings.UseNamespaces)
				namespaces.Add(nd);
		}

		void WritePropertyName(string name, ObjectState state = null)
		{
			WriteSeparator(state);
			WriteEscapedString(name);
			_writer.Write(':');
		}

		string GetPrefixedName(XamlType xt, XamlMember xm)
		{
			return GetPrefixedName(xm.PreferredXamlNamespace, xm.IsAttachable ? String.Concat(xt.InternalXmlName, ".", xm.Name) : xm.Name);
		}

		string GetPrefixedName(string ns, string name)
		{
			string prefix = GetPrefix(ns);
			if (!string.IsNullOrEmpty(prefix))
				return $"{prefix}:{name}";
			else
				return name;

		}

		void WritePropertyName(XamlType xt, XamlMember xm)
		{
			WritePropertyName(GetPrefixedName(xt, xm));
		}

		char[] buffer = new char[1000];

		void WriteEscapedString(string name)
		{
			if (!string.IsNullOrEmpty(name))
			{
				_writer.Write('\"');
				if (buffer.Length < name.Length)
					buffer = new char[name.Length];
				int start = 0;
				for (int i = 0; i < name.Length; i++)
				{
					var ch = name[i];
					string text;
					switch (ch)
					{
						case '\"':
							text = "\\\"";
							break;
						case '\n':
							text = "\\n";
							break;
						case '\r':
							text = "\\r";
							break;
						case '\t':
							text = "\\t";
							break;
						case '\b':
							text = "\\b";
							break;
						case '\f':
							text = "\\f";
							break;
						case '\\':
							text = "\\\\";
							break;
						default:
							if (ch >= 0x20)
								continue;
							text = "\\u" + ((int)ch).ToString("x4");
							break;
					}
					var len = i - start;
					name.CopyTo(start, buffer, 0, len);
					_writer.Write(buffer, 0, len);
					// got text we can't write directly
					_writer.Write(text);
					start = i + 1;
				}
				if (start == 0)
				{
					_writer.Write(name);
				}
				else if (start < name.Length)
				{
					var len = name.Length - start;
					name.CopyTo(start, buffer, 0, len);
					_writer.Write(buffer, 0, len);
				}
				_writer.Write('\"');
			}
			else
			{
				_writer.Write("\"\"");
			}
		}

		protected override void OnWriteStartMember(XamlMember xm)
		{
			if (ReferenceEquals(xm, XamlLanguage.Initialization))
				return;
			if (ReferenceEquals(xm, XamlLanguage.Items))
				return;

			if (ReferenceEquals(xm, XamlLanguage.PositionalParameters))
				return;
			if (ReferenceEquals(xm, XamlLanguage.Key))
				return;
			if (ReferenceEquals(xm, XamlLanguage.Name))
			{
				WritePropertyName("$id");
				return;
			}

			var xt = CurrentState.Type;
			WritePropertyName(xt, xm);
		}

		bool WriteSeparator(ObjectState state = null)
		{
			state = state ?? CurrentState;
			if (state.HasSeparator)
			{
				_writer.Write(',');
				return true;
			}
			state.HasSeparator = true;
			return false;
		}

		void WriteArraySeparator()
		{
			var lastState = LastState;
			if (ReferenceEquals(lastState?.CurrentMember, XamlLanguage.Items))
			{
				if (lastState.HasSeparator)
				{
					_writer.Write(',');
					return;
				}
				lastState.HasSeparator = lastState.Type.IsCollection;
			}
		}

		protected override void OnWriteStartObject()
		{
			var state = CurrentState;
			var last = LastState;
			var parentType = last?.Type;
			if (parentType?.IsDictionary == true)
			{
				_writers.Push(_writer);
				_writer = new StringWriter();
			}
			else
			{
				WriteArraySeparator();
			}

			var type = state.Type;
			if (type == XamlLanguage.Null)
			{
				_writer.Write("null");
				return;
			}
			if (directTypes.Contains(type))
				return;

			if (ReferenceEquals(type, XamlLanguage.Reference))
			{
				_writer.Write("{\"$ref\":");
				return;
			}

			if (type.IsMarkupExtension && namespaces.Count == 0)
			{
				_writer.Write($"\"{{{GetPrefixedName(type.PreferredXamlNamespace, type.InternalXmlName)}");
				return;
			}

			_writer.Write("{");
			for (int i = 0; i < namespaces.Count; i++)
			{
				var nd = namespaces[i];
				WritePropertyName(string.IsNullOrEmpty(nd.Prefix) ? "$ns" : $"$ns:{nd.Prefix}");
				WriteEscapedString(nd.Namespace);
			}

			namespaces.Clear();

			XamlType memberType;
			if (parentType?.IsDictionary == true || parentType?.IsCollection == true)
				memberType = parentType.ItemType;
			else
				memberType = last?.CurrentMember?.Type;

			if (type != memberType)
			{
				WritePropertyName("$type");
				if (_settings.UseNamespaces)
					WriteEscapedString(GetPrefixedName(type.PreferredXamlNamespace, type.InternalXmlName));
				else
					WriteEscapedString(type.UnderlyingType.AssemblyQualifiedName);
			}
		}

		static HashSet<XamlType> directTypes = new HashSet<XamlType> {
			XamlLanguage.Boolean,
			XamlLanguage.Byte,
			XamlLanguage.Decimal,
			XamlLanguage.Double,
			XamlLanguage.Int16,
			XamlLanguage.Int32,
			XamlLanguage.Int64,
			XamlLanguage.Single,
			XamlLanguage.String,
			XamlLanguage.Null
		};

		static HashSet<Type> numericTypes = new HashSet<Type> {
			typeof(byte),
			typeof(decimal),
			typeof(double),
			typeof(short),
			typeof(int),
			typeof(long),
			typeof(float)
		};

		protected override void OnWriteValue(object value)
		{
			var state = CurrentState;
			var type = state.Type;
			var member = state.CurrentMember;
			if (ReferenceEquals(member, XamlLanguage.Key))
			{
				state.KeyValue = value;
				return;
			}
			// types from other schema contexts aren't the same instances, so compare the CLR type
			var valueType = (ReferenceEquals(member, XamlLanguage.Initialization) ? type : member.Type)?.UnderlyingType;
			if (valueType != null)
				valueType = Nullable.GetUnderlyingType(valueType) ?? valueType;
			if (valueType == typeof(bool))
			{
				_writer.Write(string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false");
				return;
			}

			if (valueType != null && numericTypes.Contains(valueType))
			{
				var number = Convert.ToString(value, CultureInfo.InvariantCulture);
				// NaN and Infinity aren't valid JSON numbers
				if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && !double.IsNaN(d) && !double.IsInfinity(d))
					_writer.Write(number);
				else
					WriteEscapedString(number);
				return;
			}

			if (!ReferenceEquals(type, XamlLanguage.Reference) && ReferenceEquals(member, XamlLanguage.PositionalParameters))
			{
				if (!WriteSeparator())
					_writer.Write(' ');
				_writer.Write(GetValueString(member, value));
				return;
			}

			var str = GetValueString(member, value);
			WriteEscapedString(str);
		}

		public void Flush()
		{
			_writer.Flush();
		}

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

	[EnhancedXaml]
	public class XamlJsonWriter : XamlWriter, IXamlLineInfoConsumer
	{
		XamlSchemaContext _schemaContext;
		XamlJsonWriterSettings _settings;
		XamlJsonWriterInternal _intl;
		TextWriter _writer;

		public override XamlSchemaContext SchemaContext => _schemaContext;

		public XamlJsonWriterSettings Settings => _settings;

		public XamlJsonWriter(Stream stream, XamlSchemaContext schemaContext)
			: this(stream, schemaContext, null)
		{
		}

		public XamlJsonWriter(Stream stream, XamlSchemaContext schemaContext, XamlJsonWriterSettings settings)
			: this(new StreamWriter(stream), schemaContext, settings)
		{
		}
		public XamlJsonWriter(TextWriter writer, XamlSchemaContext schemaContext)
			: this(writer, schemaContext, null)
		{
		}

		public XamlJsonWriter(TextWriter writer, XamlSchemaContext schemaContext, XamlJsonWriterSettings settings)
		{
			_writer = writer ?? throw new ArgumentNullException(nameof(writer));
			_schemaContext = schemaContext ?? throw new ArgumentNullException(nameof(schemaContext));
			_settings = settings ?? new XamlJsonWriterSettings();

			var manager = new XamlWriterStateManager<XamlJsonWriterException, InvalidOperationException>(true);
			_intl = new XamlJsonWriterInternal(this, _settings, _writer, _schemaContext, manager);
		}

		internal int Line { get; private set; }
		internal int Column { get; private set; }

		public bool ShouldProvideLineInfo => true;

		public void SetLineInfo(int lineNumber, int linePosition)
		{
			Line = lineNumber;
			Column = linePosition;
		}

		public override void WriteEndMember()
		{
			_intl.WriteEndMember();
		}

		public override void WriteEndObject()
		{
			_intl.WriteEndObject();
		}

		public override void WriteGetObject()
		{
			_intl.WriteGetObject();
		}

		public override void WriteNamespace(NamespaceDeclaration namespaceDeclaration)
		{
			_intl.WriteNamespace(namespaceDeclaration);
		}

		public override void WriteStartMember(XamlMember xamlMember)
		{
			_intl.WriteStartMember(xamlMember);
		}

		public override void WriteStartObject(XamlType type)
		{
			_intl.WriteStartObject(type);
		}

		public override void WriteValue(object value)
		{
			_intl.WriteValue(value);
		}

		public void Flush()
		{
			_intl.Flush();
		}

		protected override void Dispose(bool disposing)
		{
			if (!disposing)
				return;

			_intl.CloseAll();
		}

	}
}
