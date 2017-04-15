using System;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using System.Reflection;

namespace Portable.Xaml.Roslyn
{
	public class RoslynWriter : Portable.Xaml.XamlWriter
	{
		SyntaxGenerator _syntax;
		XamlSchemaContext _context;
		SyntaxNode _top;
		Workspace _workspace;

		SyntaxGenerator Syntax => _syntax;

		public RoslynWriter(XamlSchemaContext schemaContext, string language = LanguageNames.CSharp)
		{
			_workspace = new AdhocWorkspace();

			_syntax = SyntaxGenerator.GetGenerator(_workspace, language);
			_context = schemaContext;
		}

		public string ToCode()
		{
			var method = Syntax.MethodDeclaration("InitializeComponent", statements: statements);
			_top = Syntax.AddMembers(_top, method);
			return Formatter.Format(_top, _workspace).ToFullString();
		}

		public override XamlSchemaContext SchemaContext => _context;

		public override void WriteEndMember()
		{
		}

		public override void WriteEndObject()
		{
			var node = CurrentNode;
			types.Pop();

			if (types.Count > 0)
			{
				var initializer = Syntax.ObjectCreationExpression(node.TypeName(this));
				if (!string.IsNullOrEmpty(node.ID))
				{
					initializer = Syntax.AssignmentStatement(Syntax.IdentifierName(node.ID), initializer);
				}
				statements.Add(Syntax.LocalDeclarationStatement(node.GetName(this), initializer));
			}
			if (node.HasStatements)
				statements.AddRange(node.Statements);

			var member = CurrentNode?.Member;
			if (member != null)
			{
				if (member.Type.IsCollection)
				{
					// get correct add method, or just call blah.Add()
				}
				else
				{
					statements.Add(Syntax.AssignmentStatement(Syntax.MemberAccessExpression(CurrentObject, Syntax.IdentifierName(member.Name)), Syntax.IdentifierName(node.GetName(this))));
				}
			}
		}

		public override void WriteGetObject()
		{
		}

		public override void WriteNamespace(NamespaceDeclaration namespaceDeclaration)
		{
		}

		class Node
		{
			public XamlType Type;
			public SyntaxNode Create;
			public XamlMember Member;
			public string ID;
			SyntaxNode _typeName;
			public SyntaxNode TypeName(RoslynWriter w) => _typeName ?? (_typeName = w.GetType(Type));
			public Action<object> SetValue;
			List<SyntaxNode> _statements;
			string _name;
			public bool HasStatements => _statements?.Count > 0;
			public List<SyntaxNode> Statements => _statements ?? (_statements = new List<SyntaxNode>());

			public string SetName(string name) => _name = name;

			public string GetName(RoslynWriter w)
			{
				if (_name != null)
					return _name;

				int count;
				if (!w.nameCount.TryGetValue(Type.Name, out count))
				{
					count = 0;
				}
				count++;
				w.nameCount[Type.Name] = count;
				return _name = "_" + Type.Name + count;
			}
		}

		Dictionary<string, int> nameCount = new Dictionary<string, int>();
		Stack<Node> types = new Stack<Node>();
		List<SyntaxNode> statements = new List<SyntaxNode>();
		SyntaxNode createObject;

		Node CurrentNode => types.Count > 0 ? types.Peek() : null;

		SyntaxNode GetType(XamlType type) => Syntax.DottedName(type.UnderlyingType.FullName);

		SyntaxNode CurrentObject => types.Count == 1 ? Syntax.ThisExpression() : Syntax.IdentifierName(CurrentNode.GetName(this));

		public override void WriteStartMember(XamlMember xamlMember)
		{
			var node = CurrentNode;
			node.Member = xamlMember;
			if (xamlMember == XamlLanguage.Class)
			{
				node.SetValue = val => _top = Syntax.ClassDeclaration(val.ToString(), modifiers: DeclarationModifiers.Partial, baseType: node.TypeName(this));
				return;
			}
			if (xamlMember == XamlLanguage.Name)
			{
				// write as protected field
				node.SetValue = val =>
				{
					node.ID = val.ToString();
					_top = Syntax.AddMembers(_top, Syntax.FieldDeclaration(val.ToString(), node.TypeName(this)));
					var aliasedName = node.Type.GetAliasedProperty(XamlLanguage.Name);
					if (aliasedName != null)
					{
						node.Statements.Add(Syntax.AssignmentStatement(Syntax.MemberAccessExpression(CurrentObject, Syntax.IdentifierName(aliasedName.Name)), Syntax.LiteralExpression(val)));
					}
				};
				return;
			}
			if (!xamlMember.IsDirective)
			{
				node.SetValue = val =>
				{
					node.Statements.Add(Syntax.AssignmentStatement(Syntax.MemberAccessExpression(CurrentObject, Syntax.IdentifierName(xamlMember.Name)), Syntax.LiteralExpression(val)));
				};
			}
		}

		public override void WriteStartObject(XamlType type)
		{
			var node = new Node { Type = type };
			types.Push(node);
		}

		public override void WriteValue(object value)
		{
			// todo: translate value to the correct type here, or in SetValue?
			CurrentNode?.SetValue?.Invoke(value);
		}
	}

}
