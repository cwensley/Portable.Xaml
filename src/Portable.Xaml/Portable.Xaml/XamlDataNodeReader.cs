using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Portable.Xaml.Schema;

namespace Portable.Xaml
{
	/// <summary>Turns a parsed JSON or YAML document into XAML nodes.</summary>
	class XamlDataNodeReader
	{
		readonly XamlSchemaContext _context;
		readonly IXamlDataReaderSource _source;
		readonly Assembly _localAssembly;
		readonly Stack<Dictionary<string, string>> _namespaces = new Stack<Dictionary<string, string>>();
		List<XamlDataNodeInfo> _nodes;

		class NamespaceResolver : IXamlNamespaceResolver
		{
			readonly XamlDataNodeReader _reader;

			public NamespaceResolver(XamlDataNodeReader reader) => _reader = reader;

			public string GetNamespace(string prefix) => _reader.ResolveNamespace(prefix ?? string.Empty);

			public IEnumerable<NamespaceDeclaration> GetNamespacePrefixes() => _reader._namespaces.Peek().Select(r => new NamespaceDeclaration(_reader.ResolveNamespace(r.Key), r.Key));
		}

		public XamlDataNodeReader(XamlSchemaContext context, IXamlDataReaderSource source, Assembly localAssembly, IEnumerable<KeyValuePair<string, string>> defaultNamespaces)
		{
			_context = context;
			_source = source;
			_localAssembly = localAssembly;
			_namespaces.Push(defaultNamespaces.ToDictionary(r => r.Key, r => r.Value));
		}

		/// <param name="capacity">Estimate of the number of nodes, so the list doesn't grow while adding.</param>
		public List<XamlDataNodeInfo> GetNodes(XamlDataNode root, XamlType rootType, int capacity)
		{
			_nodes = new List<XamlDataNodeInfo>(capacity);
			WriteValue(root, rootType, null);
			return _nodes;
		}

		void Add(XamlNodeType nodeType, object value, int line, int column)
			=> _nodes.Add(new XamlDataNodeInfo { NodeType = nodeType, Value = value, Line = line, Column = column });

		void Add(XamlNodeType nodeType, object value, XamlDataNode node) => Add(nodeType, value, node.Line, node.Column);

		void WriteValue(XamlDataNode node, XamlType expectedType, XamlMember member, string key = null)
		{
			switch (node.Kind)
			{
				case XamlDataKind.Object:
					WriteObject(node, expectedType, member, key);
					break;
				case XamlDataKind.Array:
					WriteArray(node, expectedType, member, key);
					break;
				case XamlDataKind.Null:
					WriteNull(node, key);
					break;
				default:
					WriteText(node, expectedType, member, key);
					break;
			}
		}

		void WriteNull(XamlDataNode node, string key)
		{
			Add(XamlNodeType.StartObject, XamlLanguage.Null, node);
			WriteKey(node, key);
			Add(XamlNodeType.EndObject, null, node);
		}

		void WriteKey(XamlDataNode node, string key)
		{
			if (key == null)
				return;
			Add(XamlNodeType.StartMember, XamlLanguage.Key, node);
			var markup = key.Length > 0 && key[0] == '{' ? ParseMarkup(key) : null;
			if (markup != null)
				WriteMarkup(markup, node, null);
			else
				Add(XamlNodeType.Value, Unescape(key), node);
			Add(XamlNodeType.EndMember, null, node);
		}

		void WriteObject(XamlDataNode node, XamlType expectedType, XamlMember member, string key)
		{
			var hasNamespaces = false;
			foreach (var property in node.Properties)
			{
				if (property.Name != "$ns" && !property.Name.StartsWith("$ns:", StringComparison.Ordinal))
					continue;
				if (property.Value.Kind != XamlDataKind.String)
					throw CreateException("Expected a namespace string", property.Value);
				if (!hasNamespaces)
				{
					_namespaces.Push(new Dictionary<string, string>(_namespaces.Peek()));
					hasNamespaces = true;
				}
				var prefix = property.Name.Length > 4 ? property.Name.Substring(4) : string.Empty;
				_namespaces.Peek()[prefix] = property.Value.Text;
				Add(XamlNodeType.NamespaceDeclaration, new NamespaceDeclaration(property.Value.Text, prefix), property.Line, property.Column);
			}

			if (node.TryFind("$ref", out var reference))
			{
				Add(XamlNodeType.StartObject, XamlLanguage.Reference, node);
				Add(XamlNodeType.StartMember, XamlLanguage.PositionalParameters, reference.Line, reference.Column);
				Add(XamlNodeType.Value, GetText(reference.Value), reference.Value);
				Add(XamlNodeType.EndMember, null, reference.Value);
			}
			else
			{
				XamlType type;
				var isGetObject = false;
				if (node.TryFind("$type", out var typeProperty) || node.TypeName != null)
				{
					var typeNode = typeProperty.Value ?? node;
					type = LookupType(typeProperty.Value != null ? GetText(typeProperty.Value) : node.TypeName, typeNode);
					// XData fills a read only IXmlSerializable, like in XAML
					if (IsReadOnly(member) && !ReferenceEquals(type, XamlLanguage.XData))
						throw CreateException($"Cannot specify a '$type' for read only member '{member}'", typeNode);
				}
				else
				{
					var names = new List<string>(node.Properties.Count);
					foreach (var property in node.Properties)
					{
						if (!property.Name.StartsWith("$", StringComparison.Ordinal))
							names.Add(property.Name);
					}
					type = _source.GetObjectType(expectedType, names);
					if (ReferenceEquals(type, null))
						throw CreateException("Add a '$type' to say what type of object to create", node);
					isGetObject = IsReadOnly(member);
				}

				if (isGetObject)
					Add(XamlNodeType.GetObject, null, node);
				else
					Add(XamlNodeType.StartObject, type, node);

				// the value creates the object, so it comes before any members
				if (node.TryFind("$value", out var value))
				{
					Add(XamlNodeType.StartMember, XamlLanguage.Initialization, value.Line, value.Column);
					Add(XamlNodeType.Value, Unescape(GetText(value.Value)), value.Value);
					Add(XamlNodeType.EndMember, null, value.Value);
				}

				var hasItems = node.TryFind("$items", out var items);
				var itemsStarted = false;
				foreach (var property in node.Properties)
				{
					if (property.Name == "$name")
					{
						Add(XamlNodeType.StartMember, XamlLanguage.Name, property.Line, property.Column);
						Add(XamlNodeType.Value, GetText(property.Value), property.Value);
						Add(XamlNodeType.EndMember, null, property.Value);
						continue;
					}
					if (property.Name.StartsWith("$", StringComparison.Ordinal))
						continue;

					// without $items, every property of a dictionary is an item
					if (type.IsDictionary && !hasItems)
					{
						if (!itemsStarted)
						{
							Add(XamlNodeType.StartMember, XamlLanguage.Items, property.Line, property.Column);
							itemsStarted = true;
						}
						WriteValue(property.Value, type.ItemType, XamlLanguage.Items, property.Name);
						continue;
					}

					var propertyMember = LookupMember(type, property);
					Add(XamlNodeType.StartMember, propertyMember, property.Line, property.Column);
					if (IsListDirective(propertyMember) && property.Value.Kind == XamlDataKind.Array)
					{
						foreach (var item in property.Value.Items)
							WriteValue(item, null, propertyMember);
					}
					else
						WriteValue(property.Value, propertyMember.Type, propertyMember);
					Add(XamlNodeType.EndMember, null, property.Value);
				}
				if (itemsStarted)
					Add(XamlNodeType.EndMember, null, node);
				if (hasItems)
					WriteItems(items, type);
			}

			WriteKey(node, key);
			Add(XamlNodeType.EndObject, null, node);

			if (hasNamespaces)
				_namespaces.Pop();
		}

		/// <summary>Directives that hold their values directly rather than a collection object.</summary>
		static bool IsListDirective(XamlMember member) => ReferenceEquals(member, XamlLanguage.Arguments) || ReferenceEquals(member, XamlLanguage.PositionalParameters);

		void WriteItems(XamlDataProperty items, XamlType type)
		{
			var node = items.Value;
			if (node.Kind == XamlDataKind.Array)
			{
				if (!type.IsCollection && !type.IsDictionary)
					throw CreateException($"'{type}' is not a collection", node);
				if (node.Items.Count == 0)
					return;
				Add(XamlNodeType.StartMember, XamlLanguage.Items, items.Line, items.Column);
				foreach (var item in node.Items)
					WriteValue(item, type.ItemType, XamlLanguage.Items);
				Add(XamlNodeType.EndMember, null, node);
			}
			else if (node.Kind == XamlDataKind.Object && type.IsDictionary)
			{
				if (node.Properties.Count == 0)
					return;
				Add(XamlNodeType.StartMember, XamlLanguage.Items, items.Line, items.Column);
				foreach (var property in node.Properties)
					WriteValue(property.Value, type.ItemType, XamlLanguage.Items, property.Name);
				Add(XamlNodeType.EndMember, null, node);
			}
			else
				throw CreateException(type.IsDictionary ? "Expected an array or object of items" : "Expected an array of items", node);
		}

		static bool IsReadOnly(XamlMember member) => member != null && !member.IsDirective && !member.IsWritePublic;

		void WriteArray(XamlDataNode node, XamlType expectedType, XamlMember member, string key)
		{
			if (node.TypeName != null)
			{
				var type = LookupType(node.TypeName, node);
				Add(XamlNodeType.StartObject, type, node);
				WriteItems(new XamlDataProperty { Value = node, Line = node.Line, Column = node.Column }, type);
				WriteKey(node, key);
				Add(XamlNodeType.EndObject, null, node);
				return;
			}

			if (ReferenceEquals(expectedType, null))
				throw CreateException("Add a '$type' to say what type of collection to create", node);

			if (!expectedType.IsCollection)
			{
				var collectionMember = _source.GetArrayMember(expectedType, out var objectType);
				if (ReferenceEquals(collectionMember, null))
					throw CreateException($"An array can't be used for '{expectedType}'", node);

				Add(XamlNodeType.StartObject, objectType ?? collectionMember.DeclaringType, node);
				Add(XamlNodeType.StartMember, collectionMember, node);
				WriteArray(node, collectionMember.Type, collectionMember, null);
				Add(XamlNodeType.EndMember, null, node);
				WriteKey(node, key);
				Add(XamlNodeType.EndObject, null, node);
				return;
			}

			// like XAML, fill the collection a property already has rather than replacing it
			if (member != null && !member.IsDirective)
				Add(XamlNodeType.GetObject, null, node);
			else
				Add(XamlNodeType.StartObject, expectedType, node);
			if (node.Items.Count > 0)
			{
				Add(XamlNodeType.StartMember, XamlLanguage.Items, node);
				foreach (var item in node.Items)
					WriteValue(item, expectedType.ItemType, XamlLanguage.Items);
				Add(XamlNodeType.EndMember, null, node);
			}
			WriteKey(node, key);
			Add(XamlNodeType.EndObject, null, node);
		}

		void WriteText(XamlDataNode node, XamlType expectedType, XamlMember member, string key)
		{
			var text = node.Text;
			if (node.Kind == XamlDataKind.String && text.Length > 0 && text[0] == '{')
			{
				if (text.Length > 1 && text[1] == '}')
					text = text.Substring(2);
				else
				{
					var markup = ParseMarkup(text);
					if (markup != null)
					{
						WriteMarkup(markup, node, key);
						return;
					}
				}
			}

			var textType = node.TypeName != null ? LookupType(node.TypeName, node) : null;
			XamlType textObjectType = null;
			var textMember = ReferenceEquals(textType, null) && node.Kind == XamlDataKind.String && !ReferenceEquals(expectedType, null) ? _source.GetTextMember(expectedType, out textObjectType) : null;
			if (!ReferenceEquals(textMember, null))
			{
				Add(XamlNodeType.StartObject, textObjectType ?? textMember.DeclaringType, node);
				Add(XamlNodeType.StartMember, textMember, node);
				Add(XamlNodeType.Value, text, node);
				Add(XamlNodeType.EndMember, null, node);
			}
			// dictionary items and the root are objects, as they can't be a bare value
			else if (!ReferenceEquals(textType, null) || key != null || member == null)
			{
				textType = textType ?? expectedType;
				if (ReferenceEquals(textType, null))
					throw CreateException(key != null ? "Unknown type for dictionary item" : "Add a '$type' to say what type of value to create", node);
				Add(XamlNodeType.StartObject, textType, node);
				Add(XamlNodeType.StartMember, XamlLanguage.Initialization, node);
				Add(XamlNodeType.Value, text, node);
				Add(XamlNodeType.EndMember, null, node);
			}
			else
			{
				Add(XamlNodeType.Value, text, node);
				return;
			}

			WriteKey(node, key);
			Add(XamlNodeType.EndObject, null, node);
		}

		/// <summary>Removes the <c>{}</c> that escapes text starting with a curly brace.</summary>
		static string Unescape(string text) => text.StartsWith("{}", StringComparison.Ordinal) ? text.Substring(2) : text;

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

		void WriteMarkup(ParsedMarkupExtensionInfo info, XamlDataNode node, string key)
		{
			Add(XamlNodeType.StartObject, info.Type, node);
			foreach (var argument in info.Arguments)
			{
				Add(XamlNodeType.StartMember, argument.Key, node);
				var values = argument.Value as List<object> ?? new List<object> { argument.Value };
				foreach (var value in values)
				{
					if (value is ParsedMarkupExtensionInfo nested)
						WriteMarkup(nested, node, null);
					else
						Add(XamlNodeType.Value, value, node);
				}
				Add(XamlNodeType.EndMember, null, node);
			}
			WriteKey(node, key);
			Add(XamlNodeType.EndObject, null, node);
		}

		string GetText(XamlDataNode node)
		{
			if (node.Kind == XamlDataKind.Object || node.Kind == XamlDataKind.Array || node.Kind == XamlDataKind.Null)
				throw CreateException("Expected a string", node);
			return node.Text;
		}

		string ResolveNamespace(string prefix)
		{
			if (_namespaces.Peek().TryGetValue(prefix, out var ns))
			{
				// like XamlXmlReader, a clr-namespace without an assembly is in the local assembly
				if (_localAssembly != null && ns.StartsWith("clr-namespace:", StringComparison.Ordinal) && ns.IndexOf(';') == -1)
					ns += ";assembly=" + _localAssembly.GetName().Name;
				return ns;
			}
			return null;
		}

		/// <summary>Looks up "Type", "prefix:Type", "prefix:Type(prefix:Arg)", "Namespace.Type" or "Namespace.Type, Assembly".</summary>
		XamlType LookupType(string typeName, XamlDataNode node)
		{
			try
			{
				return FindType(typeName, node);
			}
			catch (Exception ex) when (!(ex is XamlException xamlException && xamlException.LineNumber > 0))
			{
				// e.g. a missing or empty assembly name, which would otherwise not say where it is
				throw new XamlParseException($"Type '{typeName}' is unknown", ex, node.Line, node.Column);
			}
		}

		XamlType FindType(string typeName, XamlDataNode node)
		{
			string ns;
			string name;
			var assemblyIndex = typeName.IndexOf(',');
			var prefixIndex = typeName.IndexOf(':');
			var argumentIndex = typeName.IndexOfAny(TypeArgumentChars);
			if (argumentIndex > 0 && (assemblyIndex < 0 || argumentIndex < assemblyIndex))
			{
				// generic and array types, e.g. "List(x:Int32)" or "x:Int32[]"
				XamlTypeName.TryParse(typeName, new NamespaceResolver(this), out var typeNameInfo);
				return Verify(typeNameInfo != null ? _context.GetXamlType(typeNameInfo) : null, typeName, node);
			}
			if (assemblyIndex > 0 || (prefixIndex < 0 && typeName.IndexOf('.') > 0))
			{
				var fullName = assemblyIndex > 0 ? typeName.Substring(0, assemblyIndex).Trim() : typeName;
				var dotIndex = fullName.LastIndexOf('.');
				name = fullName.Substring(dotIndex + 1);
				ns = "clr-namespace:" + (dotIndex > 0 ? fullName.Substring(0, dotIndex) : string.Empty);
				if (assemblyIndex > 0)
					ns += ";assembly=" + typeName.Substring(assemblyIndex + 1).Trim();
				else if (_localAssembly != null)
					ns += ";assembly=" + _localAssembly.GetName().Name;
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
			// markup extensions can be named without their suffix, like in XAML
			if (ReferenceEquals(type, null) || type.IsUnknown)
				type = _context.GetXamlType(new XamlTypeName(ns, name + "Extension")) ?? type;
			return Verify(type, typeName, node);
		}

		static readonly char[] TypeArgumentChars = { '(', '[' };

		static XamlType Verify(XamlType type, string typeName, XamlDataNode node)
		{
			if (ReferenceEquals(type, null) || type.IsUnknown)
				throw CreateException($"Type '{typeName}' is unknown", node);
			return type;
		}

		/// <summary>Looks up "Member", "Type.Member", "prefix:Type.AttachedMember" or a directive like "x:Key".</summary>
		XamlMember LookupMember(XamlType type, XamlDataProperty property)
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
				var memberName = name.Substring(dotIndex + 1);
				var owner = ns != null ? _context.GetXamlType(new XamlTypeName(ns, ownerName)) : LookupType(ownerName, property.Value);
				member = owner?.GetAttachableMember(memberName);
				// "Type.Member" can also name a property of the object itself, like a property element in XAML
				if (ReferenceEquals(member, null) && !ReferenceEquals(owner, null) && type.CanAssignTo(owner))
					member = owner.GetMember(memberName);
			}
			else if (ns != null)
			{
				member = ns == XamlLanguage.Xaml2006Namespace
					? FindDirective(name)
					: new XamlDirective(ns, name);
			}
			else
				member = type.GetMember(name) ?? type.GetAttachableMember(name);

			if (ReferenceEquals(member, null))
				throw CreateException($"Could not find member '{property.Name}' in type '{type}'", property);
			return member;
		}

		// not a lambda, so the closure isn't allocated on every LookupMember call
		static XamlDirective FindDirective(string name)
		{
			foreach (var directive in XamlLanguage.AllDirectives)
			{
				if (directive.Name == name)
					return directive;
			}
			return null;
		}

		static Exception CreateException(string message, XamlDataNode node) => new XamlParseException(message, null, node.Line, node.Column);

		static Exception CreateException(string message, XamlDataProperty property) => new XamlParseException(message, null, property.Line, property.Column);
	}
}
