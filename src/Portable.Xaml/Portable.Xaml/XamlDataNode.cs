using System.Collections.Generic;

namespace Portable.Xaml
{
	enum XamlDataKind
	{
		Object,
		Array,
		String,
		Number,
		Boolean,
		Null
	}

	/// <summary>A value of a data format such as JSON or YAML, as read or to be written.</summary>
	class XamlDataNode
	{
		public XamlDataKind Kind;
		public string Text;
		/// <summary>Type given outside of the properties, e.g. a YAML tag.</summary>
		public string TypeName;
		public List<XamlDataProperty> Properties;
		public List<XamlDataNode> Items;
		public int Line;
		public int Column;

		public bool TryFind(string name, out XamlDataProperty property)
		{
			foreach (var p in Properties)
			{
				if (p.Name == name)
				{
					property = p;
					return true;
				}
			}
			property = default(XamlDataProperty);
			return false;
		}

		public static XamlDataNode CreateObject() => new XamlDataNode { Kind = XamlDataKind.Object, Properties = new List<XamlDataProperty>() };

		public static XamlDataNode CreateArray() => new XamlDataNode { Kind = XamlDataKind.Array, Items = new List<XamlDataNode>() };

		public static XamlDataNode CreateText(XamlDataKind kind, string text) => new XamlDataNode { Kind = kind, Text = text };

		public void Add(string name, XamlDataNode value) => Properties.Add(new XamlDataProperty { Name = name, Value = value });
	}

	struct XamlDataProperty
	{
		public string Name;
		public XamlDataNode Value;
		public int Line;
		public int Column;
	}

	struct XamlDataNodeInfo
	{
		public XamlNodeType NodeType;
		public object Value;
		public int Line;
		public int Column;
	}

	/// <summary>Lets a public reader fill in what the data leaves out.</summary>
	interface IXamlDataReaderSource
	{
		XamlType GetObjectType(XamlType expectedType, IList<string> propertyNames);

		XamlMember GetArrayMember(XamlType expectedType, out XamlType objectType);

		XamlMember GetTextMember(XamlType expectedType, out XamlType objectType);
	}
}
