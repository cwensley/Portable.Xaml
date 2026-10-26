using System;
using System.Collections.Generic;
using System.Text;
using Portable.Xaml.Schema;

namespace Portable.Xaml
{
	/// <summary>Turns XAML nodes into a JSON or YAML document, using the same conventions as <see cref="XamlDataNodeReader"/>.</summary>
	/// <remarks>The whole object graph is gathered before it is written, as how an object is written depends on all of its members.</remarks>
	abstract class XamlDataNodeWriter : XamlWriterInternalBase
	{
		class CapturedObject
		{
			public XamlType Type;
			public bool IsGetObject;
			public readonly List<CapturedMember> Members = new List<CapturedMember>();

			public CapturedMember Find(XamlMember member)
			{
				foreach (var m in Members)
				{
					if (ReferenceEquals(m.Member, member))
						return m;
				}
				return null;
			}
		}

		class CapturedMember
		{
			public XamlMember Member;
			/// <summary>Strings and <see cref="CapturedObject"/>s.</summary>
			public readonly List<object> Values = new List<object>();
		}

		readonly Stack<CapturedObject> _objects = new Stack<CapturedObject>();

		protected XamlDataNodeWriter(XamlSchemaContext schemaContext, XamlWriterStateManager manager)
			: base(schemaContext, manager)
		{
		}

		/// <summary>Writes the document once the root object has ended.</summary>
		protected abstract void WriteDocument(XamlDataNode root);

		protected abstract XamlException CreateException(string message);

		protected override void OnWriteNamespace(NamespaceDeclaration nd)
		{
			// all namespaces are declared on the root, along with those found while writing
		}

		protected override void OnWriteStartObject() => Push(new CapturedObject { Type = CurrentState.Type });

		protected override void OnWriteGetObject() => Push(new CapturedObject { Type = CurrentState.Type, IsGetObject = true });

		void Push(CapturedObject obj)
		{
			if (_objects.Count > 0)
				CurrentCapturedMember.Values.Add(obj);
			_objects.Push(obj);
		}

		CapturedMember CurrentCapturedMember
		{
			get
			{
				var members = _objects.Peek().Members;
				return members[members.Count - 1];
			}
		}

		protected override void OnWriteStartMember(XamlMember xm) => _objects.Peek().Members.Add(new CapturedMember { Member = xm });

		protected override void OnWriteValue(object value)
		{
			var member = CurrentCapturedMember;
			member.Values.Add(value == null ? null : GetValueString(member.Member, value));
		}

		protected override void OnWriteEndMember()
		{
		}

		protected override void OnWriteEndObject()
		{
			var obj = _objects.Pop();
			if (_objects.Count == 0)
				WriteDocument(ConvertRoot(obj));
		}

		XamlDataNode ConvertRoot(CapturedObject obj)
		{
			var node = Convert(obj, null, null, true);
			if (node.Kind != XamlDataKind.Object)
				return node;

			// type names were written while converting, so all namespaces they need are known now
			var properties = new List<XamlDataProperty>();
			var prefixes = new HashSet<string>();
			foreach (var nd in prefix_lookup.Namespaces)
			{
				if (prefixes.Add(nd.Prefix))
					properties.Add(new XamlDataProperty { Name = string.IsNullOrEmpty(nd.Prefix) ? "$ns" : "$ns:" + nd.Prefix, Value = Text(XamlDataKind.String, nd.Namespace) });
			}
			node.Properties.InsertRange(0, properties);
			return node;
		}

		XamlDataNode Convert(CapturedObject obj, XamlType expectedType, XamlMember parentMember, bool isRoot = false)
		{
			var type = obj.Type;
			var members = new List<CapturedMember>(obj.Members.Count);
			foreach (var member in obj.Members)
			{
				// the parent writes the key
				if (!ReferenceEquals(member.Member, XamlLanguage.Key))
					members.Add(member);
			}

			if (!isRoot)
			{
				if (ReferenceEquals(type, XamlLanguage.Null) && members.Count == 0)
					return new XamlDataNode { Kind = XamlDataKind.Null };

				if (ReferenceEquals(type, XamlLanguage.Reference) && TryGetSingleText(members, XamlLanguage.PositionalParameters, out var name))
				{
					var reference = XamlDataNode.CreateObject();
					reference.Add("$ref", Text(XamlDataKind.String, name));
					return reference;
				}

				if (type.IsMarkupExtension && TryFormatMarkup(obj, members, out var markup))
					return Text(XamlDataKind.String, markup);

				// a value, unless its type is needed to know what to create. Arguments are converted to their parameter's type, and text set to an object stays text.
				if (TryGetSingleText(members, XamlLanguage.Initialization, out var text)
					&& (type == expectedType || ReferenceEquals(parentMember, XamlLanguage.Arguments) || (type == XamlLanguage.String && expectedType == XamlLanguage.Object)))
					return Value(type, text);

				if (type.IsCollection && !type.IsDictionary && IsOnly(members, XamlLanguage.Items) && IsArrayAllowed(obj, expectedType, parentMember))
					return ConvertItems(members.Count > 0 ? members[0] : null, type);
			}

			var node = XamlDataNode.CreateObject();
			if (!obj.IsGetObject && (isRoot || type != expectedType))
				node.Add("$type", Text(XamlDataKind.String, GetTypeName(type)));

			// a dictionary's own properties are its items, unless it has other members or a key can't be a property
			XamlDataNode keyedItems = null;
			if (type.IsDictionary && IsOnly(members, XamlLanguage.Items) && TryConvertKeyedItems(members.Count > 0 ? members[0] : null, type, out keyedItems))
			{
				foreach (var property in keyedItems.Properties)
				{
					if (property.Name.StartsWith("$", StringComparison.Ordinal))
					{
						keyedItems = null;
						break;
					}
				}
				if (keyedItems != null)
				{
					node.Properties.AddRange(keyedItems.Properties);
					return node;
				}
			}

			foreach (var member in members)
			{
				var xm = member.Member;
				if (member.Values.Count == 0)
					continue;
				if (ReferenceEquals(xm, XamlLanguage.Name))
					node.Add("$name", ConvertValue(member.Values[0], XamlLanguage.String, xm));
				else if (ReferenceEquals(xm, XamlLanguage.Initialization))
					node.Add("$value", member.Values[0] is string text ? Value(type, text) : ConvertValue(member.Values[0], null, xm));
				else if (ReferenceEquals(xm, XamlLanguage.Items))
					node.Add("$items", type.IsDictionary && TryConvertKeyedItems(member, type, out var items) ? items : ConvertItems(member, type));
				else if (ReferenceEquals(xm, XamlLanguage.Arguments) || ReferenceEquals(xm, XamlLanguage.PositionalParameters))
				{
					var array = XamlDataNode.CreateArray();
					foreach (var value in member.Values)
						array.Items.Add(ConvertValue(value, null, xm));
					node.Add(GetMemberName(xm), array);
				}
				else if (member.Values.Count == 1)
					node.Add(GetMemberName(xm), ConvertValue(member.Values[0], xm.Type, xm));
				else
				{
					var array = XamlDataNode.CreateArray();
					foreach (var value in member.Values)
						array.Items.Add(ConvertValue(value, xm.Type?.ItemType, xm));
					node.Add(GetMemberName(xm), array);
				}
			}
			return node;
		}

		/// <summary>Whether a collection can be written as a plain array, as the reader then fills the existing collection of a property, or creates one of the expected type.</summary>
		static bool IsArrayAllowed(CapturedObject obj, XamlType expectedType, XamlMember parentMember)
		{
			if (ReferenceEquals(parentMember, null))
				return false;
			if (obj.IsGetObject)
				return !parentMember.IsDirective;
			return parentMember.IsDirective && obj.Type == expectedType;
		}

		static bool IsOnly(List<CapturedMember> members, XamlMember member) => members.Count == 0 || (members.Count == 1 && ReferenceEquals(members[0].Member, member));

		static bool TryGetSingleText(List<CapturedMember> members, XamlMember member, out string text)
		{
			if (members.Count == 1 && ReferenceEquals(members[0].Member, member) && members[0].Values.Count == 1 && members[0].Values[0] is string value)
			{
				text = value;
				return true;
			}
			text = null;
			return false;
		}

		XamlDataNode ConvertItems(CapturedMember items, XamlType type)
		{
			var array = XamlDataNode.CreateArray();
			if (items == null)
				return array;
			foreach (var item in items.Values)
			{
				var node = ConvertValue(item, type.ItemType, XamlLanguage.Items);
				// in a dictionary that has items without keys, keyed items say their key with x:Key
				if (type.IsDictionary && item is CapturedObject obj && obj.Find(XamlLanguage.Key) is CapturedMember key && key.Values.Count == 1)
				{
					if (node.Kind != XamlDataKind.Object)
						throw CreateException($"Cannot write the key of an item in '{type}'");
					node.Add(GetMemberName(XamlLanguage.Key), ConvertValue(key.Values[0], null, XamlLanguage.Key));
				}
				array.Items.Add(node);
			}
			return array;
		}

		/// <summary>Converts the items of a dictionary to properties named by their key, when all keys can be written as text.</summary>
		bool TryConvertKeyedItems(CapturedMember items, XamlType type, out XamlDataNode node)
		{
			node = XamlDataNode.CreateObject();
			if (items == null)
				return true;
			foreach (var item in items.Values)
			{
				if (!(item is CapturedObject obj) || !TryGetKey(obj, out var key))
				{
					node = null;
					return false;
				}
				node.Add(key, ConvertValue(obj, type.ItemType, XamlLanguage.Items));
			}
			return true;
		}

		bool TryGetKey(CapturedObject obj, out string key)
		{
			key = null;
			var member = obj.Find(XamlLanguage.Key);
			if (member == null || member.Values.Count != 1)
				return false;
			var value = member.Values[0];
			if (value is CapturedObject keyObject)
			{
				var keyMembers = keyObject.Members;
				if (TryGetSingleText(keyMembers, XamlLanguage.Initialization, out var text))
					value = text;
				else
					return keyObject.Type.IsMarkupExtension && TryFormatMarkup(keyObject, keyMembers, out key);
			}
			key = Escape((string)value);
			return true;
		}

		XamlDataNode ConvertValue(object value, XamlType expectedType, XamlMember member)
		{
			if (value == null)
				return new XamlDataNode { Kind = XamlDataKind.Null };
			if (value is CapturedObject obj)
				return Convert(obj, expectedType, member);
			return Value(member.Type, (string)value);
		}

		/// <summary>Gets text as a number or boolean when that is its type, so it is written as one.</summary>
		static XamlDataNode Value(XamlType type, string text)
		{
			var underlyingType = type?.UnderlyingType;
			if (underlyingType != null)
			{
				underlyingType = Nullable.GetUnderlyingType(underlyingType) ?? underlyingType;
				switch (underlyingType.IsEnum ? TypeCode.Object : Type.GetTypeCode(underlyingType))
				{
					case TypeCode.Boolean:
						return Text(XamlDataKind.Boolean, text);
					case TypeCode.SByte:
					case TypeCode.Byte:
					case TypeCode.Int16:
					case TypeCode.UInt16:
					case TypeCode.Int32:
					case TypeCode.UInt32:
					case TypeCode.Int64:
					case TypeCode.UInt64:
					case TypeCode.Single:
					case TypeCode.Double:
					case TypeCode.Decimal:
						return Text(XamlDataKind.Number, text);
				}
			}
			return Text(XamlDataKind.String, Escape(text));
		}

		static XamlDataNode Text(XamlDataKind kind, string text) => XamlDataNode.CreateText(kind, text);

		/// <summary>Escapes text that would otherwise be read as a markup extension.</summary>
		static string Escape(string text) => text.Length > 0 && text[0] == '{' ? "{}" + text : text;

		/// <summary>Formats a markup extension as text like <c>{x:Type x:Int32}</c>, when all of its values can be text.</summary>
		bool TryFormatMarkup(CapturedObject obj, List<CapturedMember> members, out string text)
		{
			text = null;
			var type = obj.Type;
			if (obj.IsGetObject || type.TypeArguments != null)
				return false;

			var sb = new StringBuilder();
			sb.Append('{');
			var prefix = prefix_lookup.LookupPrefix(type.PreferredXamlNamespace);
			if (!string.IsNullOrEmpty(prefix))
				sb.Append(prefix).Append(':');
			// like XAML, the reader looks for "NameExtension" first
			sb.Append(TrimExtension(type.Name));

			var first = true;
			foreach (var member in members)
			{
				var xm = member.Member;
				var isPositional = ReferenceEquals(xm, XamlLanguage.PositionalParameters);
				if (!isPositional && (xm.IsDirective || xm.IsAttachable || member.Values.Count != 1))
					return false;
				foreach (var value in member.Values)
				{
					string valueText;
					if (value is string s)
						valueText = FormatMarkupValue(s);
					else if (!(value is CapturedObject nested) || !nested.Type.IsMarkupExtension || !TryFormatMarkup(nested, nested.Members, out valueText))
						return false;
					sb.Append(first ? " " : ", ");
					first = false;
					if (!isPositional)
						sb.Append(xm.Name).Append('=');
					sb.Append(valueText);
				}
			}
			sb.Append('}');
			text = sb.ToString();
			return true;
		}

		static string FormatMarkupValue(string value)
		{
			var needsQuotes = value.Length == 0;
			foreach (var ch in value)
			{
				if (ch == ',' || ch == '=' || ch == '{' || ch == '}' || ch == '\'' || ch == '"' || ch == '\\' || char.IsWhiteSpace(ch))
				{
					needsQuotes = true;
					break;
				}
			}
			if (!needsQuotes)
				return value;
			return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
		}

		static string TrimExtension(string name) => name.EndsWith("Extension", StringComparison.Ordinal) && name.Length > 9 ? name.Substring(0, name.Length - 9) : name;

		string GetTypeName(XamlType type)
		{
			var typeName = new XamlTypeName(type);
			// "x:Array" rather than "x:ArrayExtension", when that doesn't name another type
			if (type.IsMarkupExtension && typeName.TypeArguments.Count == 0)
			{
				var shortName = new XamlTypeName(typeName.Namespace, TrimExtension(typeName.Name));
				var shortType = sctx.GetXamlType(shortName);
				if (shortName.Name != typeName.Name && (ReferenceEquals(shortType, null) || shortType.IsUnknown || shortType == type))
					typeName = shortName;
			}
			return typeName.ToString(prefix_lookup);
		}

		string GetMemberName(XamlMember member)
		{
			if (member.IsDirective)
				return GetPrefixedName(member.PreferredXamlNamespace, member.Name);
			if (member.IsAttachable)
				return GetTypeName(member.DeclaringType) + "." + member.Name;
			return member.Name;
		}

		string GetPrefixedName(string ns, string name)
		{
			var prefix = prefix_lookup.LookupPrefix(ns);
			return string.IsNullOrEmpty(prefix) ? name : prefix + ":" + name;
		}
	}
}
