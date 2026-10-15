using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Portable.Xaml.CodeGen.Schema
{
	static class SymbolExtensions
	{
		public static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
			.WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

		public static string ToTypeName(this ITypeSymbol type) => type.ToDisplayString(TypeFormat);

		public static string FullName(this ISymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

		public static string MetadataFullName(this INamespaceSymbol ns) => ns == null || ns.IsGlobalNamespace ? string.Empty : ns.ToDisplayString();

		public static IEnumerable<ITypeSymbol> BaseTypesAndSelf(this ITypeSymbol type)
		{
			for (var t = type; t != null; t = t.BaseType)
				yield return t;
		}

		public static bool IsOrDerivesFrom(this ITypeSymbol type, string metadataName)
		{
			return type.BaseTypesAndSelf().Any(t => t.IsMetadataName(metadataName));
		}

		public static bool Implements(this ITypeSymbol type, string metadataName)
		{
			if (type.IsMetadataName(metadataName))
				return true;
			return type.AllInterfaces.Any(i => i.OriginalDefinition.IsMetadataName(metadataName));
		}

		public static INamedTypeSymbol FindInterface(this ITypeSymbol type, string metadataName)
		{
			if (type is INamedTypeSymbol named && named.OriginalDefinition.IsMetadataName(metadataName))
				return named;
			return type.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.IsMetadataName(metadataName));
		}

		public static bool IsMetadataName(this ITypeSymbol type, string metadataName)
		{
			var name = type.ContainingNamespace.MetadataFullName();
			name = name.Length > 0 ? name + "." + type.MetadataName : type.MetadataName;
			return name == metadataName;
		}

		public static string GetMetadataFullName(this ITypeSymbol type)
		{
			if (type.ContainingType != null)
				return type.ContainingType.GetMetadataFullName() + "+" + type.MetadataName;
			var ns = type.ContainingNamespace.MetadataFullName();
			return ns.Length > 0 ? ns + "." + type.MetadataName : type.MetadataName;
		}

		public static bool IsPublicType(this ITypeSymbol type)
		{
			for (var t = type; t != null; t = t.ContainingType)
			{
				if (t.DeclaredAccessibility != Accessibility.Public)
					return false;
			}
			return true;
		}

		/// <summary>Finds an attribute on a symbol (and base types when <paramref name="inherit"/> is set).</summary>
		public static AttributeData FindAttribute(this ISymbol symbol, Func<INamedTypeSymbol, bool> match, bool inherit = true)
		{
			if (symbol is ITypeSymbol type && inherit)
			{
				foreach (var t in type.BaseTypesAndSelf())
				{
					var attr = t.GetAttributes().FirstOrDefault(a => a.AttributeClass != null && match(a.AttributeClass));
					if (attr != null)
						return attr;
				}
				return null;
			}
			if (symbol is IPropertySymbol property && inherit)
			{
				for (var p = property; p != null; p = p.OverriddenProperty)
				{
					var attr = p.GetAttributes().FirstOrDefault(a => a.AttributeClass != null && match(a.AttributeClass));
					if (attr != null)
						return attr;
				}
				return null;
			}
			return symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass != null && match(a.AttributeClass));
		}

		public static string GetStringArgument(this AttributeData attr, int index = 0)
		{
			if (attr == null || attr.ConstructorArguments.Length <= index)
				return null;
			return attr.ConstructorArguments[index].Value as string;
		}

		public static IEnumerable<ISymbol> GetMembersIncludingBase(this ITypeSymbol type, string name)
		{
			foreach (var t in type.BaseTypesAndSelf())
			{
				foreach (var m in t.GetMembers(name))
					yield return m;
			}
			if (type.TypeKind == TypeKind.Interface)
			{
				foreach (var i in type.AllInterfaces)
				{
					foreach (var m in i.GetMembers(name))
						yield return m;
				}
			}
		}

		public static bool HasPublicParameterlessConstructor(this INamedTypeSymbol type)
		{
			if (type.IsValueType)
				return true;
			return type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);
		}
	}
}
