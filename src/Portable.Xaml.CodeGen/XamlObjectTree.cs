using System;
using System.Collections.Generic;
using System.Linq;

namespace Portable.Xaml.CodeGen
{
	/// <summary>An object (StartObject) or existing value (GetObject) read from XAML.</summary>
	public sealed class XamlObjectNode
	{
		public XamlType Type { get; internal set; }

		/// <summary>True when this is the current value of the parent member, e.g. a read-only collection.</summary>
		public bool IsGetObject { get; internal set; }

		public List<XamlMemberNode> Members { get; } = new List<XamlMemberNode>();

		/// <summary>Prefix to XAML namespace mappings in scope for this object.</summary>
		public IReadOnlyDictionary<string, string> Namespaces { get; internal set; }

		public XamlLineInfo LineInfo { get; internal set; }

		public XamlMemberNode GetMember(XamlMember member) => Members.FirstOrDefault(r => r.Member == member);

		/// <summary>Gets the text of a member that has a single text value, e.g. x:Name.</summary>
		public string GetText(XamlMember member) => GetMember(member)?.Values.FirstOrDefault()?.Text;

		public override string ToString() => (IsGetObject ? "GetObject " : "") + Type;
	}

	public sealed class XamlMemberNode
	{
		public XamlMember Member { get; internal set; }

		public List<XamlValueNode> Values { get; } = new List<XamlValueNode>();

		public XamlLineInfo LineInfo { get; internal set; }

		public override string ToString() => Member?.ToString();
	}

	/// <summary>Either text or an object.</summary>
	public sealed class XamlValueNode
	{
		public string Text { get; internal set; }

		public XamlObjectNode Object { get; internal set; }

		public XamlLineInfo LineInfo { get; internal set; }

		public override string ToString() => Text ?? Object?.ToString();
	}

	public struct XamlLineInfo
	{
		public XamlLineInfo(int line, int column)
		{
			Line = line;
			Column = column;
		}

		public int Line { get; }

		public int Column { get; }

		public bool HasLineInfo => Line > 0;
	}

	/// <summary>Reads a XAML node stream into a tree of objects and members.</summary>
	public static class XamlObjectTree
	{
		public static XamlObjectNode Read(XamlReader reader, IEnumerable<KeyValuePair<string, string>> rootNamespaces = null)
		{
			var lineInfo = reader as IXamlLineInfo;
			XamlLineInfo GetLineInfo() => lineInfo != null && lineInfo.HasLineInfo ? new XamlLineInfo(lineInfo.LineNumber, lineInfo.LinePosition) : default(XamlLineInfo);

			var objects = new Stack<XamlObjectNode>();
			var members = new Stack<XamlMemberNode>();
			var pendingNamespaces = new Dictionary<string, string>();
			var rootScope = new Dictionary<string, string>();
			if (rootNamespaces != null)
			{
				foreach (var ns in rootNamespaces)
					rootScope[ns.Key] = ns.Value;
			}
			XamlObjectNode root = null;

			while (reader.Read())
			{
				switch (reader.NodeType)
				{
					case XamlNodeType.NamespaceDeclaration:
						pendingNamespaces[reader.Namespace.Prefix] = reader.Namespace.Namespace;
						break;
					case XamlNodeType.StartObject:
					case XamlNodeType.GetObject:
						var parentScope = objects.Count > 0 ? objects.Peek().Namespaces : rootScope;
						IReadOnlyDictionary<string, string> scope = parentScope;
						if (pendingNamespaces.Count > 0)
						{
							var merged = parentScope.ToDictionary(r => r.Key, r => r.Value);
							foreach (var ns in pendingNamespaces)
								merged[ns.Key] = ns.Value;
							scope = merged;
							pendingNamespaces.Clear();
						}
						var node = new XamlObjectNode
						{
							Type = reader.NodeType == XamlNodeType.GetObject ? members.Peek().Member.Type : reader.Type,
							IsGetObject = reader.NodeType == XamlNodeType.GetObject,
							Namespaces = scope,
							LineInfo = GetLineInfo()
						};
						if (members.Count > 0)
							members.Peek().Values.Add(new XamlValueNode { Object = node, LineInfo = node.LineInfo });
						else
							root = node;
						objects.Push(node);
						break;
					case XamlNodeType.EndObject:
						objects.Pop();
						break;
					case XamlNodeType.StartMember:
						var member = new XamlMemberNode { Member = reader.Member, LineInfo = GetLineInfo() };
						objects.Peek().Members.Add(member);
						members.Push(member);
						break;
					case XamlNodeType.EndMember:
						members.Pop();
						break;
					case XamlNodeType.Value:
						if (!(reader.Value is string text))
							throw new XamlCodeGenException($"Values of type {reader.Value?.GetType().Name ?? "null"} are not supported", GetLineInfo());
						members.Peek().Values.Add(new XamlValueNode { Text = text, LineInfo = GetLineInfo() });
						break;
				}
			}
			return root;
		}
	}

	/// <summary>Thrown when XAML can't be turned into code.</summary>
	public class XamlCodeGenException : Exception
	{
		public XamlCodeGenException(string message, XamlLineInfo lineInfo, Exception innerException = null)
			: base(message, innerException)
		{
			LineInfo = lineInfo;
		}

		public XamlLineInfo LineInfo { get; }
	}
}
