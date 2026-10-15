using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Portable.Xaml.Schema;

namespace Portable.Xaml.CodeGen.Schema
{
	/// <summary>
	/// A XAML type backed by a compiler symbol instead of a runtime <see cref="Type"/>.
	/// </summary>
	public class SymbolXamlType : XamlType
	{
		public SymbolXamlType(ITypeSymbol symbol, string xamlNamespace, IList<XamlType> typeArguments, SymbolXamlSchemaContext schemaContext)
			: base(xamlNamespace, GetXamlName(symbol), typeArguments, schemaContext)
		{
			Symbol = symbol;
			Context = schemaContext;
		}

		static string GetXamlName(ITypeSymbol symbol)
		{
			if (symbol is IArrayTypeSymbol array)
				return GetXamlName(array.ElementType) + "[]";
			return symbol.Name;
		}

		public ITypeSymbol Symbol { get; }

		public SymbolXamlSchemaContext Context { get; }

		Compilation Compilation => Context.Compilation;

		INamedTypeSymbol NamedSymbol => Symbol as INamedTypeSymbol;

		protected AttributeData FindAttribute(string name, bool inherit = true)
			=> Symbol.FindAttribute(c => Context.IsXamlAttribute(c, name), inherit);

		XamlMember GetMemberFromAttribute(string attributeName)
		{
			var name = FindAttribute(attributeName).GetStringArgument();
			return name != null ? GetMember(name) : null;
		}

		public override bool CanAssignTo(XamlType xamlType)
		{
			if (ReferenceEquals(xamlType, null))
				return false;
			if (ReferenceEquals(xamlType, XamlLanguage.Object))
				return true;
			var target = Context.GetSymbol(xamlType);
			if (target == null)
				return false;
			// only the conversions the runtime writer allows (no user-defined operators)
			var conversion = Compilation.ClassifyConversion(Symbol, target);
			return conversion.IsIdentity || (conversion.IsImplicit && (conversion.IsReference || conversion.IsBoxing || conversion.IsNullable));
		}

		public override IList<string> GetXamlNamespaces()
		{
			var clr = $"clr-namespace:{Symbol.ContainingNamespace.MetadataFullName()};assembly={Symbol.ContainingAssembly?.Identity.Name}";
			return PreferredXamlNamespace == clr ? new[] { clr } : new[] { PreferredXamlNamespace, clr };
		}

		public override string ToString() => Symbol.FullName();

		protected override bool LookupIsUnknown() => false;

		protected override Type LookupUnderlyingType() => null;

		protected override XamlType LookupBaseType()
		{
			var baseType = Symbol.BaseType;
			if (baseType == null || baseType.SpecialType == SpecialType.System_Object)
				return null;
			return Context.GetXamlType(baseType);
		}

		protected override XamlCollectionKind LookupCollectionKind()
		{
			if (Symbol is IArrayTypeSymbol)
				return XamlCollectionKind.Array;
			if (Symbol.Implements("System.Collections.IDictionary") || Symbol.Implements("System.Collections.Generic.IDictionary`2"))
				return XamlCollectionKind.Dictionary;
			if (Symbol.Implements("System.Collections.IList") || Symbol.Implements("System.Collections.Generic.ICollection`1"))
				return XamlCollectionKind.Collection;
			return XamlCollectionKind.None;
		}

		protected override XamlType LookupItemType()
		{
			if (Symbol is IArrayTypeSymbol array)
				return Context.GetXamlType(array.ElementType);
			if (IsDictionary)
			{
				var dictionary = Symbol.FindInterface("System.Collections.Generic.IDictionary`2");
				return Context.GetXamlType(dictionary?.TypeArguments[1] ?? Compilation.ObjectType);
			}
			if (IsCollection)
			{
				var collection = Symbol.FindInterface("System.Collections.Generic.ICollection`1");
				return Context.GetXamlType(collection?.TypeArguments[0] ?? Compilation.ObjectType);
			}
			return null;
		}

		protected override XamlType LookupKeyType()
		{
			if (!IsDictionary)
				return null;
			var dictionary = Symbol.FindInterface("System.Collections.Generic.IDictionary`2");
			return Context.GetXamlType(dictionary?.TypeArguments[0] ?? Compilation.ObjectType);
		}

		protected override XamlMember LookupContentProperty() => GetMemberFromAttribute("ContentPropertyAttribute");

		protected override XamlMember LookupAliasedProperty(XamlDirective directive)
		{
			if (ReferenceEquals(directive, XamlLanguage.Name))
				return GetMemberFromAttribute("RuntimeNamePropertyAttribute");
			if (ReferenceEquals(directive, XamlLanguage.Key))
				return GetMemberFromAttribute("DictionaryKeyPropertyAttribute");
			if (ReferenceEquals(directive, XamlLanguage.Uid))
				return GetMemberFromAttribute("UidPropertyAttribute");
			if (ReferenceEquals(directive, XamlLanguage.Lang))
				return GetMemberFromAttribute("XmlLangPropertyAttribute");
			return null;
		}

		protected override bool LookupIsAmbient() => FindAttribute("AmbientAttribute") != null;

		protected override bool LookupIsMarkupExtension() => Symbol.IsOrDerivesFrom("Portable.Xaml.Markup.MarkupExtension");

		protected override XamlType LookupMarkupExtensionReturnType()
		{
			var attr = FindAttribute("MarkupExtensionReturnTypeAttribute");
			if (attr != null && attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol returnType)
				return Context.GetXamlType(returnType);
			return null;
		}

		protected override bool LookupIsConstructible()
		{
			if (IsMarkupExtension)
				return true;
			if (Symbol.IsAbstract || Symbol.IsStatic || Symbol.TypeKind == TypeKind.Interface)
				return false;
			return IsNameValid;
		}

		protected override bool LookupConstructionRequiresArguments()
		{
			if (Symbol.IsValueType)
				return false;
			if (Symbol.IsAbstract)
				return true;
			if (Symbol.SpecialType == SpecialType.System_String)
				return true;
			return NamedSymbol == null || !NamedSymbol.HasPublicParameterlessConstructor();
		}

		protected override bool LookupIsNameScope() => Symbol.Implements("Portable.Xaml.Markup.INameScope");

		protected override bool LookupIsNullable() => !Symbol.IsValueType || Symbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

		protected override bool LookupIsPublic() => Symbol.IsPublicType();

		protected override bool LookupIsWhitespaceSignificantCollection() => FindAttribute("WhitespaceSignificantCollectionAttribute") != null;

		protected override bool LookupTrimSurroundingWhitespace() => FindAttribute("TrimSurroundingWhitespaceAttribute") != null;

		protected override bool LookupUsableDuringInitialization() => FindAttribute("UsableDuringInitializationAttribute") != null;

		protected override bool LookupIsXData() => false;

		protected override XamlValueConverter<XamlDeferringLoader> LookupDeferringLoader() => null;

		protected override XamlValueConverter<System.ComponentModel.TypeConverter> LookupTypeConverter() => null;

		protected override XamlValueConverter<Markup.ValueSerializer> LookupValueSerializer() => null;

		protected override IList<XamlType> LookupContentWrappers() => null;

		protected override IList<XamlType> LookupAllowedContentTypes() => null;

		protected override EventHandler<Markup.XamlSetMarkupExtensionEventArgs> LookupSetMarkupExtensionHandler() => null;

		protected override EventHandler<Markup.XamlSetTypeConverterEventArgs> LookupSetTypeConverterHandler() => null;

		protected override XamlMember LookupMember(string name, bool skipReadOnlyCheck)
		{
			foreach (var symbol in Symbol.GetMembersIncludingBase(name))
			{
				if (IsXamlMember(symbol))
					return Context.GetXamlMember(symbol, Context.GetXamlType(symbol.ContainingType));
			}
			return null;
		}

		protected override IEnumerable<XamlMember> LookupAllMembers()
		{
			var names = new HashSet<string>();
			foreach (var type in Symbol.BaseTypesAndSelf())
			{
				foreach (var symbol in type.GetMembers())
				{
					if (IsXamlMember(symbol) && names.Add(symbol.Name))
						yield return Context.GetXamlMember(symbol, Context.GetXamlType(symbol.ContainingType));
				}
			}
		}

		static bool IsXamlMember(ISymbol symbol)
		{
			if (symbol.IsStatic || symbol.DeclaredAccessibility != Accessibility.Public)
				return false;
			return symbol is IPropertySymbol property && !property.IsIndexer
				|| symbol is IEventSymbol;
		}

		protected override XamlMember LookupAttachableMember(string name)
		{
			IMethodSymbol getter = null, setter = null, adder = null;
			foreach (var type in Symbol.BaseTypesAndSelf())
			{
				foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
				{
					if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public)
						continue;
					if (setter == null && method.Name == "Set" + name && method.Parameters.Length == 2)
						setter = method;
					else if (getter == null && method.Name == "Get" + name && method.Parameters.Length == 1)
						getter = method;
					else if (adder == null && method.Name == "Add" + name + "Handler" && method.Parameters.Length == 2)
						adder = method;
				}
				if (setter != null || adder != null)
					break;
			}
			if (setter == null && adder == null && getter == null)
				return null;
			return new SymbolXamlMember(name, this, getter, setter, adder);
		}

		protected override IEnumerable<XamlMember> LookupAllAttachableMembers()
		{
			var names = new HashSet<string>();
			foreach (var type in Symbol.BaseTypesAndSelf())
			{
				foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
				{
					if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.Parameters.Length != 2)
						continue;
					string name = null;
					if (method.Name.StartsWith("Set", StringComparison.Ordinal))
						name = method.Name.Substring(3);
					else if (method.Name.StartsWith("Add", StringComparison.Ordinal) && method.Name.EndsWith("Handler", StringComparison.Ordinal))
						name = method.Name.Substring(3, method.Name.Length - 3 - "Handler".Length);
					if (!string.IsNullOrEmpty(name) && names.Add(name))
					{
						var member = LookupAttachableMember(name);
						if (member != null)
							yield return member;
					}
				}
			}
		}

		protected override IList<XamlType> LookupPositionalParameters(int parameterCount)
		{
			if (parameterCount == 1)
			{
				foreach (var member in GetAllMembers().OfType<SymbolXamlMember>())
				{
					if (member.ConstructorArgumentName != null)
						return new XamlType[] { member.Type };
				}
			}
			var ctors = PublicConstructors.Where(r => r.Parameters.Length == parameterCount).ToList();
			if (ctors.Count == 1)
				return ctors[0].Parameters.Select(p => (XamlType)Context.GetXamlType(p.Type)).ToList();
			return null;
		}

		public IEnumerable<IMethodSymbol> PublicConstructors
			=> NamedSymbol?.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public) ?? Enumerable.Empty<IMethodSymbol>();
	}
}
