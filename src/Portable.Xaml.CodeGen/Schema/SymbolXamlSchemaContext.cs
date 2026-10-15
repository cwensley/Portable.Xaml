using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Portable.Xaml.CodeGen.Schema
{
	/// <summary>
	/// A XAML schema built from a compilation's symbols, so XAML can be read without loading any of its types.
	/// </summary>
	/// <remarks>
	/// Subclass to recognize attributes from other namespaces (see <see cref="IsXamlAttribute"/>) or to change type lookup.
	/// </remarks>
	public class SymbolXamlSchemaContext : XamlSchemaContext
	{
		static readonly string[] s_markupNamespaces = { "Portable.Xaml.Markup", "System.Windows.Markup", "System.Xaml" };

		readonly Dictionary<ITypeSymbol, SymbolXamlType> _types = new Dictionary<ITypeSymbol, SymbolXamlType>(SymbolEqualityComparer.Default);
		readonly Dictionary<ISymbol, SymbolXamlMember> _members = new Dictionary<ISymbol, SymbolXamlMember>(SymbolEqualityComparer.Default);
		readonly Dictionary<(string, string, int), SymbolXamlType> _typeLookup = new Dictionary<(string, string, int), SymbolXamlType>();
		Dictionary<string, List<(string ClrNamespace, IAssemblySymbol Assembly)>> _xmlnsDefinitions;
		Dictionary<string, string> _prefixes;
		Dictionary<string, string> _compatibleNamespaces;
		List<IAssemblySymbol> _assemblies;
		Dictionary<ITypeSymbol, (ITypeSymbol DeclaringType, string MethodName)> _assemblyCodeGenMethods;

		public SymbolXamlSchemaContext(Compilation compilation)
			// only Portable.Xaml itself, so the base never scans loaded assemblies inside the compiler
			: base(new[] { typeof(XamlLanguage).GetTypeInfo().Assembly })
		{
			Compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
		}

		public Compilation Compilation { get; }

		/// <summary>The compilation's own assembly followed by every referenced assembly.</summary>
		public IReadOnlyList<IAssemblySymbol> Assemblies
		{
			get
			{
				if (_assemblies == null)
				{
					_assemblies = new List<IAssemblySymbol> { Compilation.Assembly };
					_assemblies.AddRange(Compilation.SourceModule.ReferencedAssemblySymbols);
				}
				return _assemblies;
			}
		}

		/// <summary>
		/// Whether an attribute class is the XAML attribute named <paramref name="name"/>, e.g. "ContentPropertyAttribute".
		/// </summary>
		public virtual bool IsXamlAttribute(INamedTypeSymbol attributeClass, string name)
		{
			return attributeClass.Name == name && s_markupNamespaces.Contains(attributeClass.ContainingNamespace.MetadataFullName());
		}

		/// <summary>
		/// Gets the static method compiled XAML calls for a type, from [XamlCodeGen] on the type or on any assembly.
		/// </summary>
		public virtual bool TryGetCodeGenMethod(ITypeSymbol type, out ITypeSymbol declaringType, out string methodName)
		{
			var attr = type.FindAttribute(c => IsXamlAttribute(c, "XamlCodeGenAttribute"), inherit: false);
			if (attr != null)
			{
				var args = attr.ConstructorArguments;
				declaringType = args.Length >= 2 ? (args[args.Length - 2].Value as ITypeSymbol) ?? type : type;
				methodName = args[args.Length - 1].Value as string;
				return methodName != null;
			}

			if (_assemblyCodeGenMethods == null)
			{
				// the compilation's own assembly comes first, so a project can override a library
				_assemblyCodeGenMethods = new Dictionary<ITypeSymbol, (ITypeSymbol, string)>(SymbolEqualityComparer.Default);
				foreach (var assembly in Assemblies)
				{
					foreach (var assemblyAttr in assembly.GetAttributes())
					{
						if (assemblyAttr.AttributeClass == null || !IsXamlAttribute(assemblyAttr.AttributeClass, "XamlCodeGenAttribute") || assemblyAttr.ConstructorArguments.Length != 3)
							continue;
						if (assemblyAttr.ConstructorArguments[0].Value is ITypeSymbol target
							&& assemblyAttr.ConstructorArguments[1].Value is ITypeSymbol declaring
							&& assemblyAttr.ConstructorArguments[2].Value is string name
							&& !_assemblyCodeGenMethods.ContainsKey(target))
							_assemblyCodeGenMethods[target] = (declaring, name);
					}
				}
			}
			if (_assemblyCodeGenMethods.TryGetValue(type, out var method))
			{
				declaringType = method.DeclaringType;
				methodName = method.MethodName;
				return true;
			}
			declaringType = null;
			methodName = null;
			return false;
		}

		public SymbolXamlType GetXamlType(ITypeSymbol symbol) => GetXamlType(symbol, null);

		protected virtual SymbolXamlType CreateXamlType(ITypeSymbol symbol, string xamlNamespace, IList<XamlType> typeArguments)
			=> new SymbolXamlType(symbol, xamlNamespace, typeArguments, this);

		protected virtual SymbolXamlMember CreateXamlMember(ISymbol symbol, SymbolXamlType declaringType)
			=> new SymbolXamlMember(symbol, declaringType);

		SymbolXamlType GetXamlType(ITypeSymbol symbol, string xamlNamespace)
		{
			if (symbol == null)
				return null;
			if (_types.TryGetValue(symbol, out var xt))
				return xt;

			IList<XamlType> typeArguments = null;
			if (symbol is INamedTypeSymbol named && named.IsGenericType)
				typeArguments = named.TypeArguments.Select(r => (XamlType)GetXamlType(r)).ToList();

			xt = CreateXamlType(symbol, xamlNamespace ?? GetPreferredXamlNamespace(symbol), typeArguments);
			_types[symbol] = xt;
			return xt;
		}

		internal SymbolXamlMember GetXamlMember(ISymbol symbol, SymbolXamlType declaringType)
		{
			if (_members.TryGetValue(symbol, out var member))
				return member;
			member = CreateXamlMember(symbol, declaringType);
			_members[symbol] = member;
			return member;
		}

		/// <summary>Gets the symbol for a XAML type, including Portable.Xaml's built-in types.</summary>
		public ITypeSymbol GetSymbol(XamlType xamlType)
		{
			if (xamlType is SymbolXamlType st)
				return st.Symbol;
			var type = xamlType?.UnderlyingType;
			if (type == null)
				return null;
			if (type.IsArray)
			{
				var element = GetSymbol(GetXamlType(type.GetElementType()));
				return element != null ? Compilation.CreateArrayTypeSymbol(element) : null;
			}
			return Compilation.GetTypeByMetadataName(type.FullName);
		}

		protected override XamlType GetXamlType(string xamlNamespace, string name, params XamlType[] typeArguments)
		{
			if (xamlNamespace == XamlLanguage.Xaml2006Namespace)
			{
				var builtIn = base.GetXamlType(xamlNamespace, name, typeArguments);
				// keep x:Type, x:Static, etc. as-is but use symbols for x:String, x:Int32, ...
				if (builtIn != null && !builtIn.IsMarkupExtension && builtIn.UnderlyingType != null && (typeArguments == null || typeArguments.Length == 0))
				{
					var symbol = Compilation.GetTypeByMetadataName(builtIn.UnderlyingType.FullName);
					if (symbol != null)
						return GetXamlType(symbol);
				}
				return builtIn;
			}

			var arity = typeArguments?.Length ?? 0;
			var key = (xamlNamespace, name, arity);
			if (arity == 0 && _typeLookup.TryGetValue(key, out var cached))
				return cached;

			var definition = FindTypeSymbol(xamlNamespace, name, arity);
			SymbolXamlType xt = null;
			if (definition != null)
			{
				ITypeSymbol symbol = definition;
				if (arity > 0)
				{
					var args = typeArguments.Select(GetSymbol).ToArray();
					if (args.Any(r => r == null))
						return null;
					symbol = definition.Construct(args);
				}
				xt = GetXamlType(symbol, xamlNamespace);
			}

			if (arity == 0)
				_typeLookup[key] = xt;
			return xt;
		}

		INamedTypeSymbol FindTypeSymbol(string xamlNamespace, string name, int arity)
		{
			var metadataName = arity > 0 ? $"{name}`{arity}" : name;
			foreach (var (clrNamespace, assemblies) in GetClrNamespaces(xamlNamespace))
			{
				var fullName = string.IsNullOrEmpty(clrNamespace) ? metadataName : clrNamespace + "." + metadataName;
				foreach (var assembly in assemblies)
				{
					var type = assembly.GetTypeByMetadataName(fullName);
					if (type != null)
						return type;
				}
				if (arity == 0)
				{
					// markup extensions can be used without the Extension suffix
					foreach (var assembly in assemblies)
					{
						var type = assembly.GetTypeByMetadataName(fullName + "Extension");
						if (type != null && type.IsOrDerivesFrom("Portable.Xaml.Markup.MarkupExtension"))
							return type;
					}
				}
			}
			return null;
		}

		IEnumerable<(string ClrNamespace, IEnumerable<IAssemblySymbol> Assemblies)> GetClrNamespaces(string xamlNamespace)
		{
			if (xamlNamespace == null)
				yield break;
			if (xamlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
			{
				var value = xamlNamespace.Substring("clr-namespace:".Length);
				var idx = value.IndexOf(';');
				var clrNamespace = idx >= 0 ? value.Substring(0, idx) : value;
				string assemblyName = null;
				if (idx >= 0)
				{
					var rest = value.Substring(idx + 1).Trim();
					if (rest.StartsWith("assembly=", StringComparison.Ordinal))
						assemblyName = rest.Substring("assembly=".Length).Split(',')[0].Trim();
				}
				// no assembly means the project being compiled first, then anything referenced
				var assembly = assemblyName != null ? FindAssembly(assemblyName) : null;
				yield return (clrNamespace, assembly != null ? new[] { assembly } : (IEnumerable<IAssemblySymbol>)Assemblies);
				yield break;
			}

			if (XmlnsDefinitions.TryGetValue(xamlNamespace, out var list))
			{
				foreach (var def in list)
					yield return (def.ClrNamespace, new[] { def.Assembly });
			}
		}

		public IAssemblySymbol FindAssembly(string name) => Assemblies.FirstOrDefault(r => r.Identity.Name == name);

		Dictionary<string, List<(string ClrNamespace, IAssemblySymbol Assembly)>> XmlnsDefinitions
		{
			get
			{
				if (_xmlnsDefinitions != null)
					return _xmlnsDefinitions;
				_xmlnsDefinitions = new Dictionary<string, List<(string, IAssemblySymbol)>>();
				_prefixes = new Dictionary<string, string>();
				_compatibleNamespaces = new Dictionary<string, string>();
				foreach (var assembly in Assemblies)
				{
					foreach (var attr in assembly.GetAttributes())
					{
						var cls = attr.AttributeClass;
						if (cls == null)
							continue;
						if (IsXamlAttribute(cls, "XmlnsDefinitionAttribute"))
						{
							var xmlns = attr.GetStringArgument(0);
							var clrNamespace = attr.GetStringArgument(1);
							if (xmlns == null || clrNamespace == null)
								continue;
							var target = assembly;
							var assemblyName = attr.NamedArguments.FirstOrDefault(r => r.Key == "AssemblyName").Value.Value as string;
							if (!string.IsNullOrEmpty(assemblyName))
								target = FindAssembly(new AssemblyName(assemblyName).Name) ?? assembly;
							if (!_xmlnsDefinitions.TryGetValue(xmlns, out var defs))
								_xmlnsDefinitions[xmlns] = defs = new List<(string, IAssemblySymbol)>();
							defs.Add((clrNamespace, target));
						}
						else if (IsXamlAttribute(cls, "XmlnsPrefixAttribute"))
						{
							var xmlns = attr.GetStringArgument(0);
							var prefix = attr.GetStringArgument(1);
							if (xmlns != null && prefix != null && !_prefixes.ContainsKey(xmlns))
								_prefixes[xmlns] = prefix;
						}
						else if (IsXamlAttribute(cls, "XmlnsCompatibleWithAttribute"))
						{
							var oldNs = attr.GetStringArgument(0);
							var newNs = attr.GetStringArgument(1);
							if (oldNs != null && newNs != null)
								_compatibleNamespaces[oldNs] = newNs;
						}
					}
				}
				return _xmlnsDefinitions;
			}
		}

		string GetPreferredXamlNamespace(ITypeSymbol symbol)
		{
			if (symbol is IArrayTypeSymbol array)
				return GetPreferredXamlNamespace(array.ElementType);
			var clrNamespace = symbol.ContainingNamespace.MetadataFullName();
			var assembly = symbol.ContainingAssembly;
			foreach (var def in XmlnsDefinitions)
			{
				if (def.Value.Any(r => r.ClrNamespace == clrNamespace && SymbolEqualityComparer.Default.Equals(r.Assembly, assembly)))
					return def.Key;
			}
			return $"clr-namespace:{clrNamespace};assembly={assembly?.Identity.Name}";
		}

		public override IEnumerable<string> GetAllXamlNamespaces()
		{
			var list = new List<string>(XmlnsDefinitions.Keys);
			list.Add(XamlLanguage.Xaml2006Namespace);
			return list;
		}

		public override string GetPreferredPrefix(string xmlns)
		{
			var _ = XmlnsDefinitions;
			if (_prefixes.TryGetValue(xmlns, out var prefix))
				return prefix;
			return base.GetPreferredPrefix(xmlns);
		}

		public override bool TryGetCompatibleXamlNamespace(string xamlNamespace, out string compatibleNamespace)
		{
			if (xamlNamespace == null)
				throw new ArgumentNullException(nameof(xamlNamespace));
			var defs = XmlnsDefinitions;
			if (_compatibleNamespaces.TryGetValue(xamlNamespace, out compatibleNamespace))
				return true;
			if (defs.ContainsKey(xamlNamespace) || xamlNamespace == XamlLanguage.Xaml2006Namespace || xamlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
			{
				compatibleNamespace = xamlNamespace;
				return true;
			}
			compatibleNamespace = null;
			return false;
		}
	}
}
