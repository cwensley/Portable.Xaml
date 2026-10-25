using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Portable.Xaml.CodeGen.Schema;
using Portable.Xaml.Schema;

namespace Portable.Xaml.CodeGen
{
	/// <summary>A C# expression and its type (null when only known as object).</summary>
	public struct XamlExpression
	{
		public XamlExpression(string code, ITypeSymbol type)
		{
			Code = code;
			Type = type;
		}

		public string Code { get; }

		public ITypeSymbol Type { get; }

		public override string ToString() => Code;
	}

	/// <summary>The object and member a value is being assigned to, given to markup extensions.</summary>
	public class XamlValueTarget
	{
		public XamlValueTarget(XamlExpression obj, SymbolXamlMember member)
		{
			Object = obj;
			Member = member;
		}

		public XamlExpression Object { get; }

		public SymbolXamlMember Member { get; }
	}

	/// <summary>
	/// Writes the C# for one XAML document, in place of what XamlObjectWriter does at runtime.
	/// </summary>
	/// <remarks>
	/// Subclass and override <see cref="TryEmitMarkupExtension"/> to turn a markup extension into code
	/// that needs more than <see cref="Portable.Xaml.Markup.XamlCodeGenAttribute"/> can describe.
	/// </remarks>
	public class XamlCodeEmitter
	{
		const string ServiceProviderClass = "__XamlServiceProvider";
		const string ConvertMethod = "__XamlConvert";

		readonly List<(int Indent, string Text)> _lines = new List<(int, string)>();
		readonly Dictionary<string, XamlExpression> _names = new Dictionary<string, XamlExpression>();
		readonly List<(string Modifier, ITypeSymbol Type, string Name)> _fields = new List<(string, ITypeSymbol, string)>();
		readonly HashSet<string> _variables = new HashSet<string>();
		readonly Stack<XamlObjectNode> _scopes = new Stack<XamlObjectNode>();
		readonly Dictionary<XamlObjectNode, Dictionary<string, XamlExpression>> _resources = new Dictionary<XamlObjectNode, Dictionary<string, XamlExpression>>();
		int _indent;
		int _lastLine = -1;
		bool _needsServiceProvider;
		bool _needsConvert;
		// EndInit for objects not yet stored in their parent, as the runtime writer stores them first
		readonly List<string> _pendingEndInit = new List<string>();
		bool _deferEndInit;

		public XamlCodeEmitter(SymbolXamlSchemaContext context, XamlCodeGenOptions options, INamedTypeSymbol rootClass)
		{
			Context = context;
			Options = options;
			RootClass = rootClass;
		}

		public SymbolXamlSchemaContext Context { get; }

		public Compilation Compilation => Context.Compilation;

		public XamlCodeGenOptions Options { get; }

		public INamedTypeSymbol RootClass { get; }

		public List<XamlCodeGenDiagnostic> Diagnostics { get; } = new List<XamlCodeGenDiagnostic>();

		/// <summary>Objects created so far, by x:Name.</summary>
		public IReadOnlyDictionary<string, XamlExpression> Names => _names;

		/// <summary>
		/// Finds a keyed dictionary item added so far to the current object or one of its parents, innermost first.
		/// </summary>
		/// <remarks>Use to resolve resource lookups such as StaticResource at compile time.</remarks>
		public bool TryFindResource(string key, out XamlExpression value)
		{
			foreach (var scope in _scopes)
			{
				if (_resources.TryGetValue(scope, out var resources) && resources.TryGetValue(key, out value))
					return true;
			}
			value = default;
			return false;
		}

		// output helpers

		public void WriteLine(string code) => _lines.Add((_indent, code));

		/// <summary>Maps the following statements to a line in the XAML file.</summary>
		public void MarkLine(XamlLineInfo lineInfo)
		{
			if (!lineInfo.HasLineInfo || string.IsNullOrEmpty(Options.FilePath) || lineInfo.Line == _lastLine)
				return;
			_lastLine = lineInfo.Line;
			var directive = (-1, $"#line {lineInfo.Line} \"{Options.FilePath}\"");
			// nothing was written for the previous line, so replace it
			if (_lines.Count > 0 && _lines[_lines.Count - 1].Indent < 0)
				_lines[_lines.Count - 1] = directive;
			else
				_lines.Add(directive);
		}

		public string NewVariable(string hint)
		{
			var name = "_" + char.ToLowerInvariant(hint[0]) + hint.Substring(1);
			if (!SyntaxFacts.IsValidIdentifier(name))
				name = "_obj";
			var candidate = name;
			for (int i = 1; !_variables.Add(candidate); i++)
				candidate = name + i;
			return candidate;
		}

		public XamlCodeGenException Error(string message, XamlLineInfo lineInfo) => new XamlCodeGenException(message, lineInfo);

		public void Warning(string message, XamlLineInfo lineInfo) => Diagnostics.Add(new XamlCodeGenDiagnostic(DiagnosticSeverity.Warning, message, lineInfo));

		public static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

		public static string TypeName(ITypeSymbol type) => type.ToTypeName();

		static string Identifier(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

		// document

		/// <summary>Writes the generated partial class for <paramref name="root"/>.</summary>
		public string Generate(XamlObjectNode root)
		{
			if (root.IsGetObject)
				throw Error("The root element must be an object", root.LineInfo);
			var rootType = Context.GetSymbol(root.Type) ?? throw UnknownType(root);
			if (!IsAssignable(RootClass, rootType))
				throw Error($"'{RootClass.FullName()}' must derive from '{rootType.FullName()}'", root.LineInfo);

			_indent = 3;
			EmitObjectBody(root, new XamlExpression("this", RootClass), true);
			return BuildSource();
		}

		protected virtual void EmitObjectBody(XamlObjectNode node, XamlExpression target, bool isRoot)
		{
			var nameMember = node.GetMember(XamlLanguage.Name);
			var alias = node.Type.GetAliasedProperty(XamlLanguage.Name) as SymbolXamlMember;
			var name = node.GetText(XamlLanguage.Name) ?? (alias != null ? node.GetText(alias) : null);
			if (name != null)
			{
				MarkLine((nameMember ?? node.GetMember(alias))?.LineInfo ?? node.LineInfo);
				if (nameMember != null && alias != null && node.GetMember(alias) == null)
					WriteLine($"{target.Code}.{Identifier(alias.Name)} = {Literal(name)};");
				if (!isRoot)
					RegisterName(name, target, node);
			}

			var supportInitialize = target.Type != null && target.Type.Implements("System.ComponentModel.ISupportInitialize");
			if (supportInitialize)
				WriteLine($"((global::System.ComponentModel.ISupportInitialize){target.Code}).BeginInit();");

			_scopes.Push(node);
			try
			{
				foreach (var member in node.Members)
					EmitMember(node, target, member);
			}
			finally
			{
				_scopes.Pop();
			}

			if (isRoot)
				WritePendingEndInit(0);
			if (!supportInitialize)
				return;
			var endInit = $"((global::System.ComponentModel.ISupportInitialize){target.Code}).EndInit();";
			if (isRoot)
				WriteLine(endInit);
			else
				_pendingEndInit.Add(endInit);
		}

		/// <summary>Writes the EndInit calls held back since <paramref name="start"/>, once their objects are stored.</summary>
		protected void WritePendingEndInit(int start = 0)
		{
			for (int i = start; i < _pendingEndInit.Count; i++)
				WriteLine(_pendingEndInit[i]);
			_pendingEndInit.RemoveRange(start, _pendingEndInit.Count - start);
		}

		/// <summary>Emits a value that the caller stores in its parent, then calls <see cref="WritePendingEndInit"/>.</summary>
		protected XamlExpression EmitStoredValue(XamlValueNode value, ITypeSymbol type, SymbolXamlMember member, XamlValueTarget target, XamlObjectNode scope, bool allowAsIs = false)
		{
			_deferEndInit = true;
			return EmitValue(value, type, member, target, scope, allowAsIs);
		}

		static readonly XamlMember[] s_handledDirectives =
		{
			XamlLanguage.Name, XamlLanguage.Key, XamlLanguage.Class, XamlLanguage.ClassModifier, XamlLanguage.Subclass,
			XamlLanguage.FieldModifier, XamlLanguage.TypeArguments, XamlLanguage.Arguments, XamlLanguage.FactoryMethod,
			XamlLanguage.Initialization, XamlLanguage.PositionalParameters, XamlLanguage.Uid, XamlLanguage.Shared,
			XamlLanguage.ConnectionId, XamlLanguage.Lang, XamlLanguage.Space, XamlLanguage.Base
		};

		protected virtual bool IsIgnored(XamlMember member)
		{
			if (Options.IgnoredNamespaces.Count == 0)
				return false;
			return Options.IgnoredNamespaces.Contains(member.PreferredXamlNamespace)
				|| (member.DeclaringType != null && Options.IgnoredNamespaces.Contains(member.DeclaringType.PreferredXamlNamespace) && member.IsAttachable);
		}

		protected virtual void EmitMember(XamlObjectNode node, XamlExpression target, XamlMemberNode memberNode)
		{
			var member = memberNode.Member;
			if (IsIgnored(member))
				return;

			if (member.IsDirective)
			{
				if (member == XamlLanguage.Items)
				{
					foreach (var item in memberNode.Values)
						EmitCollectionAdd(target, node.Type, item, node);
				}
				else if (member == XamlLanguage.UnknownContent)
					throw Error($"'{node.Type}' does not have a content property", memberNode.LineInfo);
				else if (!s_handledDirectives.Contains(member))
					Warning($"x:{member.Name} is not supported and was ignored", memberNode.LineInfo);
				return;
			}

			if (!(member is SymbolXamlMember symbolMember))
				throw Error($"'{node.Type}' does not have a member named '{member.Name}'", memberNode.LineInfo);

			MarkLine(memberNode.LineInfo);
			if (symbolMember.IsEvent)
			{
				EmitEvent(target, symbolMember, memberNode);
				return;
			}

			if (memberNode.Values.Count != 1)
				throw Error($"'{member.Name}' can only be set once", memberNode.LineInfo);

			var value = memberNode.Values[0];
			if (value.Object != null && value.Object.IsGetObject)
			{
				EmitGetObject(target, symbolMember, value.Object);
				return;
			}

			if (symbolMember.IsReadOnly)
				throw Error($"'{member.Name}' is read-only", memberNode.LineInfo);

			var expr = EmitStoredValue(value, symbolMember.ValueType, symbolMember, new XamlValueTarget(target, symbolMember), node);
			MarkLine(memberNode.LineInfo);
			if (symbolMember.IsAttachable)
				WriteLine($"{TypeName(symbolMember.Setter.ContainingType)}.{symbolMember.Setter.Name}({target.Code}, {expr.Code});");
			else
				WriteLine($"{target.Code}.{Identifier(member.Name)} = {expr.Code};");
			WritePendingEndInit();
		}

		protected virtual void EmitEvent(XamlExpression target, SymbolXamlMember member, XamlMemberNode memberNode)
		{
			var handler = memberNode.Values.Count == 1 ? memberNode.Values[0].Text : null;
			if (string.IsNullOrEmpty(handler))
				throw Error($"Event '{member.Name}' must be set to the name of a method", memberNode.LineInfo);

			var method = RootClass.GetMembersIncludingBase(handler)
				.OfType<IMethodSymbol>()
				.FirstOrDefault(m => m.MethodKind == MethodKind.Ordinary && Compilation.IsSymbolAccessibleWithin(m, RootClass));
			if (method == null)
				throw Error($"Event handler '{handler}' was not found on '{RootClass.FullName()}'", memberNode.LineInfo);

			var methodRef = method.IsStatic ? $"{TypeName(method.ContainingType)}.{Identifier(handler)}" : $"this.{Identifier(handler)}";
			if (member.IsAttachable)
				WriteLine($"{TypeName(member.Adder.ContainingType)}.{member.Adder.Name}({target.Code}, new {TypeName(member.ValueType)}({methodRef}));");
			else
				WriteLine($"{target.Code}.{Identifier(member.Name)} += {methodRef};");
		}

		protected virtual void EmitGetObject(XamlExpression target, SymbolXamlMember member, XamlObjectNode node)
		{
			var code = member.IsAttachable
				? $"{TypeName(member.Getter?.ContainingType ?? throw Error($"'{member.Name}' has no getter", node.LineInfo))}.{member.Getter.Name}({target.Code})"
				: $"{target.Code}.{Identifier(member.Name)}";

			var expr = new XamlExpression(code, member.ValueType);
			var count = node.Members.Sum(r => r.Member == XamlLanguage.Items ? r.Values.Count : 1);
			if (count > 1)
			{
				var variable = NewVariable(member.Name);
				WriteLine($"var {variable} = {code};");
				expr = new XamlExpression(variable, member.ValueType);
			}
			foreach (var memberNode in node.Members)
				EmitMember(node, expr, memberNode);
		}

		protected virtual void EmitCollectionAdd(XamlExpression collection, XamlType collectionType, XamlValueNode value, XamlObjectNode scope)
		{
			var collectionSymbol = Context.GetSymbol(collectionType) ?? collection.Type;
			if (collectionType.IsDictionary)
			{
				var keyNode = value.Object?.GetMember(XamlLanguage.Key);
				var keyAlias = value.Object?.Type.GetAliasedProperty(XamlLanguage.Key);
				if (keyNode == null && keyAlias != null)
					keyNode = value.Object.GetMember(keyAlias);
				if (keyNode == null || keyNode.Values.Count != 1)
					throw Error("Dictionary items must have an x:Key", value.LineInfo);
				var keyType = Context.GetSymbol(collectionType.KeyType);
				var key = EmitValue(keyNode.Values[0], keyType, null, null, value.Object);
				var item = EmitStoredValue(value, Context.GetSymbol(collectionType.ItemType), null, null, scope);
				var keyText = keyNode.Values[0].Text;
				if (keyText != null && item.Code != "null" && !SyntaxFacts.IsValidIdentifier(item.Code))
				{
					// resource lookups must get this instance, not evaluate the expression again
					var variable = NewVariable("resource");
					WriteLine($"var {variable} = {item.Code};");
					item = new XamlExpression(variable, item.Type);
				}
				MarkLine(value.LineInfo);
				WriteLine($"{collection.Code}.Add({key.Code}, {item.Code});");
				WritePendingEndInit();
				if (keyText != null && _scopes.Count > 0)
				{
					if (!_resources.TryGetValue(_scopes.Peek(), out var resources))
						_resources[_scopes.Peek()] = resources = new Dictionary<string, XamlExpression>();
					resources[keyText] = item;
				}
				return;
			}

			if (!collectionType.IsCollection)
				throw Error($"'{collectionType}' is not a collection", value.LineInfo);

			var itemType = Context.GetSymbol(collectionType.ItemType);
			var expr = EmitStoredValue(value, itemType, null, null, scope, allowAsIs: true);
			MarkLine(value.LineInfo);
			WriteLine($"{AddTarget(collection, collectionSymbol)}.Add({expr.Code});");
			WritePendingEndInit();
		}

		string AddTarget(XamlExpression collection, ITypeSymbol collectionSymbol)
		{
			if (collectionSymbol == null)
				return collection.Code;
			var hasAdd = collectionSymbol.GetMembersIncludingBase("Add")
				.OfType<IMethodSymbol>()
				.Any(m => !m.IsStatic && m.Parameters.Length == 1 && m.DeclaredAccessibility == Accessibility.Public);
			if (hasAdd)
				return collection.Code;
			var generic = collectionSymbol.FindInterface("System.Collections.Generic.ICollection`1");
			if (generic != null)
				return $"(({TypeName(generic)}){collection.Code})";
			return $"((global::System.Collections.IList){collection.Code})";
		}

		// values

		/// <summary>Writes the code for a value converted to <paramref name="type"/>.</summary>
		public virtual XamlExpression EmitValue(XamlValueNode value, ITypeSymbol type, SymbolXamlMember member, XamlValueTarget target, XamlObjectNode scope, bool allowAsIs = false)
		{
			var defer = _deferEndInit;
			_deferEndInit = false;
			if (value.Text != null)
				return ConvertText(value.Text, type, member, scope, value.LineInfo, allowAsIs);
			var start = _pendingEndInit.Count;
			var expr = EmitObject(value.Object, type, target);
			expr = ConvertValue(expr, type, member, value.LineInfo, allowAsIs);
			// values that aren't stored, e.g. arguments, are complete before they're used
			if (!defer)
				WritePendingEndInit(start);
			return expr;
		}

		/// <summary>Writes the code that creates and fills an object, returning the expression for it.</summary>
		public virtual XamlExpression EmitObject(XamlObjectNode node, ITypeSymbol expectedType, XamlValueTarget target)
		{
			MarkLine(node.LineInfo);
			if (node.Type.IsMarkupExtension)
				return EmitMarkupExtension(node, expectedType, target);

			var type = Context.GetSymbol(node.Type) ?? throw UnknownType(node);

			var initialization = node.GetMember(XamlLanguage.Initialization);
			if (initialization != null)
			{
				var text = initialization.Values.FirstOrDefault()?.Text ?? throw Error($"'{node.Type}' content must be text", initialization.LineInfo);
				return ConvertText(text, type, null, node, initialization.LineInfo);
			}

			var creation = EmitCreation(node, type);
			var variable = NewVariable(type.Name);
			MarkLine(node.LineInfo);
			WriteLine($"var {variable} = {creation};");
			var expr = new XamlExpression(variable, type);
			EmitObjectBody(node, expr, false);
			return expr;
		}

		string EmitCreation(XamlObjectNode node, ITypeSymbol type)
		{
			var arguments = node.GetMember(XamlLanguage.Arguments)?.Values ?? new List<XamlValueNode>();
			var factory = node.GetText(XamlLanguage.FactoryMethod);
			if (factory != null)
			{
				var method = type.GetMembers(factory).OfType<IMethodSymbol>()
					.FirstOrDefault(m => m.IsStatic && m.Parameters.Length == arguments.Count && Compilation.IsSymbolAccessibleWithin(m, RootClass))
					?? throw Error($"'{type.FullName()}' has no static method '{factory}' with {arguments.Count} parameters", node.LineInfo);
				return $"{TypeName(type)}.{Identifier(factory)}({EmitArguments(method, arguments, node)})";
			}

			if (arguments.Count > 0)
			{
				var ctor = (type as INamedTypeSymbol)?.InstanceConstructors
					.FirstOrDefault(c => c.Parameters.Length == arguments.Count && Compilation.IsSymbolAccessibleWithin(c, RootClass))
					?? throw Error($"'{type.FullName()}' has no constructor with {arguments.Count} parameters", node.LineInfo);
				return $"new {TypeName(type)}({EmitArguments(ctor, arguments, node)})";
			}

			if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
				throw Error($"'{type.FullName()}' is abstract and can't be created", node.LineInfo);
			if (!type.IsValueType && !(type as INamedTypeSymbol).InstanceConstructors.Any(c => c.Parameters.Length == 0 && Compilation.IsSymbolAccessibleWithin(c, RootClass)))
				throw Error($"'{type.FullName()}' has no parameterless constructor", node.LineInfo);
			return $"new {TypeName(type)}()";
		}

		string EmitArguments(IMethodSymbol method, List<XamlValueNode> arguments, XamlObjectNode scope)
		{
			var args = new List<string>();
			for (int i = 0; i < arguments.Count; i++)
				args.Add(EmitValue(arguments[i], method.Parameters[i].Type, null, null, scope).Code);
			return string.Join(", ", args);
		}

		void RegisterName(string name, XamlExpression expr, XamlObjectNode node)
		{
			if (_names.ContainsKey(name))
				throw Error($"The name '{name}' is already used", node.LineInfo);
			_names[name] = expr;

			var existing = RootClass.GetMembersIncludingBase(name)
				.FirstOrDefault(m => !m.IsStatic && (m is IFieldSymbol || m is IPropertySymbol) && Compilation.IsSymbolAccessibleWithin(m, RootClass));
			if (existing != null)
			{
				var memberType = (existing as IFieldSymbol)?.Type ?? ((IPropertySymbol)existing).Type;
				var writable = existing is IFieldSymbol field ? !field.IsReadOnly && !field.IsConst : ((IPropertySymbol)existing).SetMethod != null;
				if (!writable)
					throw Error($"'{RootClass.FullName()}.{name}' can't be set", node.LineInfo);
				var value = ConvertValue(expr, memberType, null, node.LineInfo);
				WriteLine($"this.{Identifier(name)} = {value.Code};");
				return;
			}

			if (!Options.GenerateFields)
				return;
			if (!SyntaxFacts.IsValidIdentifier(name))
			{
				Warning($"No field was generated for '{name}' as it is not a valid identifier", node.LineInfo);
				return;
			}
			var modifier = node.GetText(XamlLanguage.FieldModifier);
			if (modifier == null)
				modifier = Options.DefaultFieldModifier;
			else if (string.Equals(modifier, "NotPublic", StringComparison.OrdinalIgnoreCase))
				modifier = "internal";
			else
				modifier = modifier.ToLowerInvariant();
			_fields.Add((modifier, expr.Type ?? Compilation.ObjectType, name));
			WriteLine($"this.{Identifier(name)} = {expr.Code};");
		}

		// conversions

		/// <summary>Converts text from the XAML to an expression of <paramref name="type"/>.</summary>
		public virtual XamlExpression ConvertText(string text, ITypeSymbol type, SymbolXamlMember member, XamlObjectNode scope, XamlLineInfo lineInfo, bool allowAsIs = false)
		{
			var stringType = Compilation.GetSpecialType(SpecialType.System_String);
			if (type == null || type.SpecialType == SpecialType.System_Object || type.SpecialType == SpecialType.System_String)
				return new XamlExpression(Literal(text), stringType);

			if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
			{
				var inner = ((INamedTypeSymbol)type).TypeArguments[0];
				var innerExpr = ConvertText(text, inner, member, scope, lineInfo, allowAsIs);
				return new XamlExpression(innerExpr.Code, innerExpr.Type);
			}

			var memberConverter = member?.Property?.FindAttribute(IsTypeConverterAttribute);
			if (memberConverter != null)
				return UseConverter(memberConverter, new XamlExpression(Literal(text), stringType), type, member, lineInfo);

			if (TryCallCodeGenMethod(type, text, lineInfo, out var custom))
				return custom;

			// a type's own converter wins over the built-in enum and primitive parsing, like the runtime
			var typeConverter = type.FindAttribute(IsTypeConverterAttribute);
			if (typeConverter != null)
				return UseConverter(typeConverter, new XamlExpression(Literal(text), stringType), type, null, lineInfo);

			if (type.TypeKind == TypeKind.Enum)
				return ConvertEnum(text, (INamedTypeSymbol)type, lineInfo);

			if (TryConvertPrimitive(text, type, out var primitive))
				return primitive;
			if (IsPrimitive(type))
				throw Error($"'{text}' is not a valid {type.FullName()}", lineInfo);

			if (type.IsMetadataName("System.Type"))
			{
				var resolved = ResolveType(text, scope, lineInfo);
				return new XamlExpression($"typeof({TypeName(resolved)})", type);
			}

			if (s_intrinsicConverterTypes.Any(type.IsMetadataName))
				return UseConverter(null, new XamlExpression(Literal(text), stringType), type, null, lineInfo);

			// e.g. XamlOptional<T>: convert to what it accepts, and let C# apply the operator
			var implicitFrom = type.GetMembers("op_Implicit").OfType<IMethodSymbol>()
				.FirstOrDefault(m => m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.ReturnType, type) && m.Parameters[0].Type.SpecialType != SpecialType.System_String);
			if (implicitFrom != null)
			{
				var inner = ConvertText(text, implicitFrom.Parameters[0].Type, member, scope, lineInfo);
				return new XamlExpression($"({TypeName(type)})({inner.Code})", type);
			}

			if (allowAsIs)
				return new XamlExpression(Literal(text), stringType);
			throw Error($"Can't convert '{text}' to '{type.FullName()}'", lineInfo);
		}

		/// <summary>Converts an expression to <paramref name="type"/>, the way the runtime writer would.</summary>
		public virtual XamlExpression ConvertValue(XamlExpression value, ITypeSymbol type, SymbolXamlMember member, XamlLineInfo lineInfo, bool allowAsIs = false)
		{
			if (type == null || type.SpecialType == SpecialType.System_Object)
				return value;
			if (value.Code == "null")
				return new XamlExpression(type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T ? $"default({TypeName(type)})" : "null", type);
			if (value.Type == null)
				return new XamlExpression($"({TypeName(type)})({value.Code})", type);

			var conversion = Compilation.ClassifyConversion(value.Type, type);
			if (conversion.IsIdentity || (conversion.IsImplicit && !conversion.IsUserDefined))
				return value;
			if (conversion.IsImplicit)
				return new XamlExpression($"({TypeName(type)})({value.Code})", type);

			var memberConverter = member?.Property?.FindAttribute(IsTypeConverterAttribute);
			var converter = memberConverter ?? type.FindAttribute(IsTypeConverterAttribute);
			var onlyKnownAsBase = value.Type.SpecialType == SpecialType.System_Object || value.Type.TypeKind == TypeKind.Interface;
			if (converter != null && onlyKnownAsBase)
			{
				// the runtime writer uses the value as-is when it already fits, so check at runtime
				// and only look up the converter when it's needed, as that can probe for assemblies
				_needsConvert = true;
				var instance = ConverterInstance(converter, type, memberConverter != null ? member : null);
				return new XamlExpression($"{ConvertMethod}<{TypeName(type)}>({value.Code}, () => {instance})", type);
			}
			if (converter != null)
				return UseConverter(converter, value, type, memberConverter != null ? member : null, lineInfo);

			// the value is only known as a base type, e.g. object from ProvideValue
			if (conversion.Exists && (value.Type.SpecialType == SpecialType.System_Object || value.Type.TypeKind == TypeKind.Interface || conversion.IsReference))
				return new XamlExpression($"({TypeName(type)})({value.Code})", type);

			if (allowAsIs)
				return value;
			throw Error($"Can't convert '{value.Type.FullName()}' to '{type.FullName()}'", lineInfo);
		}

		static readonly string[] s_intrinsicConverterTypes =
		{
			"System.TimeSpan", "System.DateTime", "System.DateTimeOffset", "System.Guid", "System.Uri", "System.Version", "System.Globalization.CultureInfo"
		};

		bool IsTypeConverterAttribute(INamedTypeSymbol attributeClass) => attributeClass.IsMetadataName("System.ComponentModel.TypeConverterAttribute");

		XamlExpression UseConverter(AttributeData attribute, XamlExpression value, ITypeSymbol type, SymbolXamlMember member, XamlLineInfo lineInfo)
		{
			var instance = ConverterInstance(attribute, type, member);
			return new XamlExpression($"({TypeName(type)}){instance}.ConvertFrom(null, global::System.Globalization.CultureInfo.InvariantCulture, {value.Code})", type);
		}

		string ConverterInstance(AttributeData attribute, ITypeSymbol type, SymbolXamlMember member)
		{
			INamedTypeSymbol converterType = null;
			if (attribute != null && attribute.ConstructorArguments.Length == 1)
			{
				var arg = attribute.ConstructorArguments[0].Value;
				converterType = arg as INamedTypeSymbol;
				if (converterType == null && arg is string typeName)
					converterType = Compilation.GetTypeByMetadataName(typeName.Split(',')[0].Trim());
			}

			string instance;
			if (converterType != null && Compilation.IsSymbolAccessibleWithin(converterType, RootClass) && converterType.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public))
				instance = $"new {TypeName(converterType)}()";
			else if (member?.Property != null)
				instance = $"global::System.ComponentModel.TypeDescriptor.GetProperties(typeof({TypeName(member.Property.ContainingType)}))[{Literal(member.Name)}].Converter";
			else
				instance = $"global::System.ComponentModel.TypeDescriptor.GetConverter(typeof({TypeName(type)}))";
			return instance;
		}

		bool TryCallCodeGenMethod(ITypeSymbol type, string text, XamlLineInfo lineInfo, out XamlExpression result)
		{
			result = default;
			if (!Context.TryGetCodeGenMethod(type, out var declaringType, out var methodName))
				return false;
			var method = declaringType.GetMembers(methodName).OfType<IMethodSymbol>()
				.FirstOrDefault(m => m.IsStatic && m.Parameters.Length == 1 && m.Parameters[0].Type.SpecialType == SpecialType.System_String && Compilation.IsSymbolAccessibleWithin(m, RootClass))
				?? throw Error($"[XamlCodeGen] on '{type.FullName()}' must name a static method that takes a string", lineInfo);
			result = new XamlExpression($"{TypeName(declaringType)}.{Identifier(methodName)}({Literal(text)})", method.ReturnType);
			return true;
		}

		XamlExpression ConvertEnum(string text, INamedTypeSymbol type, XamlLineInfo lineInfo)
		{
			var parts = new List<string>();
			foreach (var raw in text.Split(','))
			{
				var part = raw.Trim();
				if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
				{
					parts.Add($"({TypeName(type)})({number})");
					continue;
				}
				var field = type.GetMembers().OfType<IFieldSymbol>()
					.FirstOrDefault(f => f.HasConstantValue && string.Equals(f.Name, part, StringComparison.OrdinalIgnoreCase))
					?? throw Error($"'{part}' is not a member of '{type.FullName()}'", lineInfo);
				parts.Add($"{TypeName(type)}.{Identifier(field.Name)}");
			}
			var code = parts.Count == 1 ? parts[0] : "(" + string.Join(" | ", parts) + ")";
			return new XamlExpression(code, type);
		}

		static bool IsPrimitive(ITypeSymbol type)
		{
			switch (type.SpecialType)
			{
				case SpecialType.System_Boolean:
				case SpecialType.System_Char:
				case SpecialType.System_SByte:
				case SpecialType.System_Byte:
				case SpecialType.System_Int16:
				case SpecialType.System_UInt16:
				case SpecialType.System_Int32:
				case SpecialType.System_UInt32:
				case SpecialType.System_Int64:
				case SpecialType.System_UInt64:
				case SpecialType.System_Single:
				case SpecialType.System_Double:
				case SpecialType.System_Decimal:
					return true;
				default:
					return false;
			}
		}

		bool TryConvertPrimitive(string text, ITypeSymbol type, out XamlExpression result)
		{
			result = default;
			var value = text.Trim();
			string code = null;
			var inv = CultureInfo.InvariantCulture;
			switch (type.SpecialType)
			{
				case SpecialType.System_Boolean:
					if (bool.TryParse(value, out var b))
						code = b ? "true" : "false";
					break;
				case SpecialType.System_Char:
					if (text.Length == 1)
						code = SymbolDisplay.FormatLiteral(text[0], true);
					break;
				case SpecialType.System_SByte:
				case SpecialType.System_Byte:
				case SpecialType.System_Int16:
				case SpecialType.System_UInt16:
				case SpecialType.System_Int32:
				case SpecialType.System_UInt32:
				case SpecialType.System_Int64:
				case SpecialType.System_UInt64:
					code = ParseInteger(value, type);
					break;
				case SpecialType.System_Single:
					if (TryParseSpecialFloat(value, "float", out code))
						break;
					if (float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, inv, out var f))
						code = SyntaxFactory.Literal(f).Text;
					break;
				case SpecialType.System_Double:
					if (TryParseSpecialFloat(value, "double", out code))
						break;
					if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, inv, out var d))
						code = SyntaxFactory.Literal(d).Text + (value.IndexOfAny(new[] { '.', 'e', 'E' }) < 0 ? "D" : "");
					break;
				case SpecialType.System_Decimal:
					if (decimal.TryParse(value, NumberStyles.Number, inv, out var m))
						code = SyntaxFactory.Literal(m).Text;
					break;
			}
			if (code == null)
				return false;
			if (code.StartsWith("-", StringComparison.Ordinal))
				code = "(" + code + ")";
			result = new XamlExpression(code, type);
			return true;
		}

		static bool TryParseSpecialFloat(string value, string keyword, out string code)
		{
			switch (value)
			{
				case "NaN": code = keyword + ".NaN"; return true;
				case "Infinity": code = keyword + ".PositiveInfinity"; return true;
				case "-Infinity": code = keyword + ".NegativeInfinity"; return true;
				default: code = null; return false;
			}
		}

		static string ParseInteger(string value, ITypeSymbol type)
		{
			var inv = CultureInfo.InvariantCulture;
			var negative = value.StartsWith("-", StringComparison.Ordinal);
			decimal number;
			var hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value.Substring(2)
				: value.StartsWith("#", StringComparison.Ordinal) ? value.Substring(1)
				: null;
			if (hex != null)
			{
				if (!ulong.TryParse(hex, NumberStyles.HexNumber, inv, out var h))
					return null;
				number = h;
			}
			else if (!decimal.TryParse(value, NumberStyles.Integer, inv, out number))
				return null;

			var typeName = type.ToTypeName();
			try
			{
				switch (type.SpecialType)
				{
					case SpecialType.System_Int32: return SyntaxFactory.Literal((int)number).Text;
					case SpecialType.System_UInt32: return SyntaxFactory.Literal((uint)number).Text;
					case SpecialType.System_Int64: return SyntaxFactory.Literal((long)number).Text;
					case SpecialType.System_UInt64: return SyntaxFactory.Literal((ulong)number).Text;
					case SpecialType.System_SByte: return $"({typeName})({(sbyte)number})";
					case SpecialType.System_Byte: return $"({typeName})({(byte)number})";
					case SpecialType.System_Int16: return $"({typeName})({(short)number})";
					case SpecialType.System_UInt16: return $"({typeName})({(ushort)number})";
				}
			}
			catch (OverflowException)
			{
			}
			return null;
		}

		/// <summary>Resolves a type name such as "local:MyType" using the namespaces in scope.</summary>
		public ITypeSymbol ResolveType(string typeName, XamlObjectNode scope, XamlLineInfo lineInfo)
		{
			if (!XamlTypeName.TryParse(typeName.Trim(), new NamespaceResolver(scope), out var xtn))
				throw Error($"'{typeName}' is not a valid type name", lineInfo);
			var xt = Context.GetXamlType(xtn);
			return Context.GetSymbol(xt) ?? throw Error($"Type '{typeName}' was not found", lineInfo);
		}

		class NamespaceResolver : IXamlNamespaceResolver
		{
			readonly IReadOnlyDictionary<string, string> _namespaces;

			public NamespaceResolver(XamlObjectNode scope)
			{
				_namespaces = scope?.Namespaces ?? new Dictionary<string, string>();
			}

			public string GetNamespace(string prefix) => _namespaces.TryGetValue(prefix ?? string.Empty, out var ns) ? ns : null;

			public IEnumerable<NamespaceDeclaration> GetNamespacePrefixes() => _namespaces.Select(r => new NamespaceDeclaration(r.Value, r.Key));
		}

		// markup extensions

		/// <summary>
		/// Override to write custom code for a markup extension. Return false to use the default behaviour.
		/// </summary>
		protected virtual bool TryEmitMarkupExtension(XamlObjectNode node, ITypeSymbol expectedType, XamlValueTarget target, out XamlExpression result)
		{
			result = default;
			return false;
		}

		protected virtual XamlExpression EmitMarkupExtension(XamlObjectNode node, ITypeSymbol expectedType, XamlValueTarget target)
		{
			if (TryEmitMarkupExtension(node, expectedType, target, out var custom))
				return custom;

			var xt = node.Type;
			var positional = node.GetMember(XamlLanguage.PositionalParameters)?.Values ?? new List<XamlValueNode>();
			string GetText(string memberName)
			{
				var value = positional.FirstOrDefault()?.Text;
				if (value != null)
					return value;
				return node.Members.FirstOrDefault(r => r.Member.Name == memberName)?.Values.FirstOrDefault()?.Text;
			}

			if (xt == XamlLanguage.Null)
				return new XamlExpression("null", null);

			if (xt == XamlLanguage.Type)
			{
				var name = GetText("TypeName") ?? throw Error("x:Type needs a type name", node.LineInfo);
				var type = ResolveType(name, node, node.LineInfo);
				return new XamlExpression($"typeof({TypeName(type)})", Compilation.GetTypeByMetadataName("System.Type"));
			}

			if (xt == XamlLanguage.Static)
				return EmitStatic(GetText("Member") ?? throw Error("x:Static needs a member", node.LineInfo), node);

			if (xt == XamlLanguage.Reference)
			{
				var name = GetText("Name") ?? throw Error("x:Reference needs a name", node.LineInfo);
				if (_names.TryGetValue(name, out var named))
					return named;
				throw Error($"x:Reference to '{name}' must come after the object it refers to", node.LineInfo);
			}

			if (xt == XamlLanguage.Array)
				return EmitArray(node);

			var symbol = Context.GetSymbol(xt) as INamedTypeSymbol ?? throw UnknownType(node);
			var values = GetMarkupExtensionValues(node, symbol, positional, out var ctor);

			if (Context.TryGetCodeGenMethod(symbol, out var codeGenType, out var codeGenMethod))
				return EmitCodeGenCall(node, codeGenType, codeGenMethod, values, expectedType, target);

			// no codegen method, so create the extension and call ProvideValue at runtime
			var args = ctor != null
				? string.Join(", ", positional.Select((p, i) => EmitValue(p, ctor.Parameters[i].Type, null, null, node).Code))
				: string.Empty;
			var initializers = new List<string>();
			foreach (var memberNode in node.Members.Where(r => !r.Member.IsDirective))
			{
				if (!(memberNode.Member is SymbolXamlMember member) || member.IsReadOnly)
					throw Error($"'{xt}' does not have a settable member named '{memberNode.Member.Name}'", memberNode.LineInfo);
				var value = EmitValue(memberNode.Values[0], member.ValueType, member, null, node);
				initializers.Add($"{Identifier(member.Name)} = {value.Code}");
			}
			var init = initializers.Count > 0 ? " { " + string.Join(", ", initializers) + " }" : string.Empty;
			var returnType = Context.GetSymbol(xt.MarkupExtensionReturnType);
			return new XamlExpression($"(new {TypeName(symbol)}({args}){init}).ProvideValue({ServiceProvider(target)})", returnType);
		}

		Dictionary<string, (XamlValueNode Value, ITypeSymbol Type)> GetMarkupExtensionValues(XamlObjectNode node, INamedTypeSymbol symbol, List<XamlValueNode> positional, out IMethodSymbol ctor)
		{
			var values = new Dictionary<string, (XamlValueNode, ITypeSymbol)>(StringComparer.OrdinalIgnoreCase);
			foreach (var memberNode in node.Members.Where(r => !r.Member.IsDirective))
			{
				if (memberNode.Values.Count != 1)
					throw Error($"'{memberNode.Member.Name}' must have one value", memberNode.LineInfo);
				var type = (memberNode.Member as SymbolXamlMember)?.ValueType;
				values[memberNode.Member.Name] = (memberNode.Values[0], type);
			}

			ctor = null;
			if (positional.Count == 0)
				return values;

			ctor = symbol.InstanceConstructors.FirstOrDefault(c => c.Parameters.Length == positional.Count && c.DeclaredAccessibility == Accessibility.Public)
				?? throw Error($"'{symbol.FullName()}' has no constructor with {positional.Count} parameters", node.LineInfo);
			var properties = symbol.BaseTypesAndSelf().SelectMany(t => t.GetMembers()).OfType<IPropertySymbol>().ToList();
			for (int i = 0; i < positional.Count; i++)
			{
				var parameter = ctor.Parameters[i];
				var property = properties.FirstOrDefault(p => p.FindAttribute(c => Context.IsXamlAttribute(c, "ConstructorArgumentAttribute")).GetStringArgument() == parameter.Name)
					?? properties.FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
				values[property?.Name ?? parameter.Name] = (positional[i], property?.Type ?? parameter.Type);
			}
			return values;
		}

		XamlExpression EmitCodeGenCall(XamlObjectNode node, ITypeSymbol declaringType, string methodName, Dictionary<string, (XamlValueNode Value, ITypeSymbol Type)> values, ITypeSymbol expectedType, XamlValueTarget target)
		{
			var serviceProviderType = Compilation.GetTypeByMetadataName("System.IServiceProvider");
			var candidates = declaringType.GetMembers(methodName).OfType<IMethodSymbol>()
				.Where(m => m.IsStatic && Compilation.IsSymbolAccessibleWithin(m, RootClass))
				.OrderByDescending(m => m.Parameters.Count(p => values.ContainsKey(p.Name)));

			foreach (var candidate in candidates)
			{
				var method = candidate;
				if (method.IsGenericMethod)
				{
					if (method.TypeParameters.Length != 1)
						continue;
					method = method.Construct(expectedType ?? Compilation.ObjectType);
				}
				// bind on names first so nothing is written for an overload that doesn't fit
				var bound = method.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, serviceProviderType) || values.ContainsKey(p.Name) || p.IsOptional || p.IsParams);
				var used = method.Parameters.Count(p => values.ContainsKey(p.Name));
				if (!bound || used != values.Count)
					continue;

				var args = new List<string>();
				foreach (var parameter in method.Parameters)
				{
					if (SymbolEqualityComparer.Default.Equals(parameter.Type, serviceProviderType))
						args.Add($"{Identifier(parameter.Name)}: {ServiceProvider(target)}");
					else if (values.TryGetValue(parameter.Name, out var value))
						args.Add($"{Identifier(parameter.Name)}: {EmitValue(value.Value, parameter.Type, null, null, node).Code}");
				}
				var typeArgs = method.IsGenericMethod ? $"<{string.Join(", ", method.TypeArguments.Select(TypeName))}>" : string.Empty;
				return new XamlExpression($"{TypeName(declaringType)}.{Identifier(method.Name)}{typeArgs}({string.Join(", ", args)})", method.ReturnType);
			}

			var names = values.Count > 0 ? string.Join(", ", values.Keys) : "no values";
			throw Error($"No '{declaringType.FullName()}.{methodName}' method matches {names}", node.LineInfo);
		}

		XamlExpression EmitStatic(string value, XamlObjectNode scope)
		{
			var idx = value.LastIndexOf('.');
			if (idx <= 0)
				throw Error($"'{value}' must be in the form Type.Member", scope.LineInfo);
			var type = ResolveType(value.Substring(0, idx), scope, scope.LineInfo);
			var name = value.Substring(idx + 1);
			var member = type.GetMembersIncludingBase(name).FirstOrDefault(m => m.IsStatic && (m is IFieldSymbol || m is IPropertySymbol))
				?? throw Error($"'{type.FullName()}' has no static field or property '{name}'", scope.LineInfo);
			var memberType = (member as IFieldSymbol)?.Type ?? ((IPropertySymbol)member).Type;
			return new XamlExpression($"{TypeName(type)}.{Identifier(name)}", memberType);
		}

		XamlExpression EmitArray(XamlObjectNode node)
		{
			var typeNode = node.Members.FirstOrDefault(r => r.Member.Name == "Type")?.Values.FirstOrDefault()
				?? throw Error("x:Array needs a Type", node.LineInfo);
			var typeName = typeNode.Text;
			if (typeName == null && typeNode.Object?.Type == XamlLanguage.Type)
				typeName = typeNode.Object.GetMember(XamlLanguage.PositionalParameters)?.Values.FirstOrDefault()?.Text
					?? typeNode.Object.Members.FirstOrDefault(r => r.Member.Name == "TypeName")?.Values.FirstOrDefault()?.Text;
			if (typeName == null)
				throw Error("x:Array Type must be a type name or x:Type", typeNode.LineInfo);
			var elementType = ResolveType(typeName, typeNode.Object ?? node, typeNode.LineInfo);
			var items = node.Members.FirstOrDefault(r => r.Member.Name == "Items" || r.Member == XamlLanguage.Items);
			var values = items?.Values.SelectMany(v => v.Object != null && v.Object.IsGetObject ? v.Object.Members.SelectMany(m => m.Values) : new[] { v })
				.Select(v => EmitValue(v, elementType, null, null, node).Code)
				.ToList() ?? new List<string>();
			var arrayType = Compilation.CreateArrayTypeSymbol(elementType);
			return new XamlExpression($"new {TypeName(elementType)}[] {{ {string.Join(", ", values)} }}", arrayType);
		}

		/// <summary>An IServiceProvider with the target object and property, for ProvideValue and codegen methods.</summary>
		public string ServiceProvider(XamlValueTarget target)
		{
			_needsServiceProvider = true;
			var property = target?.Member?.Property;
			var propertyInfo = property != null
				? $"typeof({TypeName(property.ContainingType)}).GetProperty({Literal(property.Name)})"
				: "null";
			return $"new {ServiceProviderClass}(this, {target?.Object.Code ?? "null"}, {propertyInfo})";
		}

		XamlCodeGenException UnknownType(XamlObjectNode node)
			=> Error($"Type '{node.Type.Name}' was not found in '{node.Type.PreferredXamlNamespace}'", node.LineInfo);

		protected bool IsAssignable(ITypeSymbol from, ITypeSymbol to)
		{
			var conversion = Compilation.ClassifyConversion(from, to);
			return conversion.IsIdentity || (conversion.IsImplicit && conversion.IsReference);
		}

		// source

		string BuildSource()
		{
			var sb = new StringBuilder();
			sb.AppendLine("// <auto-generated/>");
			sb.AppendLine("#pragma warning disable");
			if (((CSharpCompilation)Compilation).LanguageVersion >= LanguageVersion.CSharp8)
				sb.AppendLine("#nullable disable");
			sb.AppendLine();

			var ns = RootClass.ContainingNamespace.MetadataFullName();
			var indent = 0;
			void Line(string text) => sb.Append('\t', indent).AppendLine(text);

			if (ns.Length > 0)
			{
				Line($"namespace {ns}");
				Line("{");
				indent++;
			}

			var containers = new List<INamedTypeSymbol>();
			for (var t = RootClass; t != null; t = t.ContainingType)
				containers.Insert(0, t);
			foreach (var container in containers)
			{
				Line($"partial {TypeKeyword(container)} {container.Name}{TypeParameters(container)}");
				Line("{");
				indent++;
			}

			foreach (var field in _fields)
				Line($"{field.Modifier} {TypeName(field.Type)} {Identifier(field.Name)};");
			if (_fields.Count > 0)
				sb.AppendLine();

			Line($"[global::System.CodeDom.Compiler.GeneratedCode(\"Portable.Xaml.CodeGen\", \"{typeof(XamlCodeEmitter).Assembly.GetName().Version}\")]");
			Line($"{MethodSignature()}");
			Line("{");
			var bodyIndent = indent + 1;
			foreach (var line in _lines)
			{
				if (line.Indent < 0)
					sb.AppendLine(line.Text);
				else
					sb.Append('\t', bodyIndent).AppendLine(line.Text);
			}
			if (_lastLine >= 0)
				sb.AppendLine("#line default");
			Line("}");

			if (_needsServiceProvider)
				WriteServiceProvider(Line);

			if (_needsConvert)
			{
				Line("");
				Line($"static T {ConvertMethod}<T>(object value, global::System.Func<global::System.ComponentModel.TypeConverter> converter)");
				Line("	=> value is T ? (T)value : value == null ? default(T) : (T)converter().ConvertFrom(null, global::System.Globalization.CultureInfo.InvariantCulture, value);");
			}

			WriteMembers(Line);

			for (int i = 0; i < containers.Count; i++)
			{
				indent--;
				Line("}");
			}
			if (ns.Length > 0)
			{
				indent--;
				Line("}");
			}
			return sb.ToString();
		}

		string MethodSignature()
		{
			var existing = RootClass.GetMembers(Options.MethodName).OfType<IMethodSymbol>().FirstOrDefault(m => m.Parameters.Length == 0);
			if (existing == null)
				return $"{Options.MethodModifiers} void {Options.MethodName}()";
			if (existing.IsPartialDefinition && existing.PartialImplementationPart == null)
			{
				// match the declared modifiers so the implementation pairs with it
				var syntax = existing.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
				var modifiers = syntax != null ? string.Join(" ", syntax.Modifiers.Select(m => m.Text)) : "partial";
				return $"{modifiers} void {Options.MethodName}()";
			}
			throw Error($"'{RootClass.FullName()}' already has a method named '{Options.MethodName}'. Declare it as 'partial void {Options.MethodName}();' or remove it.", default);
		}

		/// <summary>Override to add members to the generated class, after the generated method.</summary>
		protected virtual void WriteMembers(Action<string> line)
		{
		}

		static string TypeKeyword(INamedTypeSymbol type)
		{
			if (type.IsRecord)
				return type.IsValueType ? "record struct" : "record";
			if (type.TypeKind == TypeKind.Interface)
				return "interface";
			return type.IsValueType ? "struct" : "class";
		}

		static string TypeParameters(INamedTypeSymbol type)
			=> type.TypeParameters.Length > 0 ? "<" + string.Join(", ", type.TypeParameters.Select(r => r.Name)) + ">" : string.Empty;

		static void WriteServiceProvider(Action<string> line)
		{
			line("");
			line($"sealed class {ServiceProviderClass} : global::System.IServiceProvider, global::Portable.Xaml.Markup.IProvideValueTarget, global::Portable.Xaml.IRootObjectProvider");
			line("{");
			line("\tpublic " + ServiceProviderClass + "(object rootObject, object targetObject, object targetProperty)");
			line("\t{");
			line("\t\tRootObject = rootObject;");
			line("\t\tTargetObject = targetObject;");
			line("\t\tTargetProperty = targetProperty;");
			line("\t}");
			line("\tpublic object RootObject { get; }");
			line("\tpublic object TargetObject { get; }");
			line("\tpublic object TargetProperty { get; }");
			line("\tpublic object GetService(global::System.Type serviceType) => serviceType.IsInstanceOfType(this) ? this : null;");
			line("}");
		}
	}
}
