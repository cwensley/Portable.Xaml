//
// Copyright (C) 2010 Novell Inc. http://novell.com
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Collections.Generic;
using Portable.Xaml.ComponentModel;
using System.Linq;
using System.Reflection;
using Portable.Xaml.Markup;
using Portable.Xaml.Schema;

using Pair = System.Collections.Generic.KeyValuePair<string,string>;
using System.Diagnostics;

namespace Portable.Xaml
{
	// This type caches assembly attribute search results. To do this,
	// it registers AssemblyLoaded event on CurrentDomain when it should
	// reflect dynamic in-scope asemblies.
	// It should be released at finalizer.
	public class XamlSchemaContext
	{
		public XamlSchemaContext()
			: this(null, null)
		{
		}

		public XamlSchemaContext(IEnumerable<Assembly> referenceAssemblies)
			: this(referenceAssemblies, null)
		{
		}

		public XamlSchemaContext(XamlSchemaContextSettings settings)
			: this(null, settings)
		{
		}

		public XamlSchemaContext(IEnumerable<Assembly> referenceAssemblies, XamlSchemaContextSettings settings)
		{
			if (referenceAssemblies != null)
				reference_assemblies = new List<Assembly>(referenceAssemblies);
			/*else
				AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;*/

			if (settings == null)
				return;

			FullyQualifyAssemblyNamesInClrNamespaces = settings.FullyQualifyAssemblyNamesInClrNamespaces;
			SupportMarkupExtensionsWithDuplicateArity = settings.SupportMarkupExtensionsWithDuplicateArity;
		}

		~XamlSchemaContext ()
		{
			/*if (reference_assemblies == null)
				AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;*/
		}

		IList<Assembly> reference_assemblies;

		// assembly attribute caches
		Dictionary<string,List<string>> xaml_nss;
		Dictionary<string,string> prefixes;
		Dictionary<string,string> compat_nss;
		Dictionary<string,List<XamlType>> all_xaml_types;
		XamlType[] empty_xaml_types = new XamlType [0];
		Dictionary<Type, XamlType> run_time_types = new Dictionary<Type, XamlType>();

		public bool FullyQualifyAssemblyNamesInClrNamespaces { get; private set; }

		public IList<Assembly> ReferenceAssemblies
		{
			get { return reference_assemblies; }
		}

		struct AssemblyInfo
		{
			public AssemblyName Name;
			public Assembly Assembly;
		}

		IList<AssemblyInfo> assembliesInScope;

		IEnumerable<AssemblyInfo> AssembliesInScope
		{
			get
			{
				if (assembliesInScope != null)
					return assembliesInScope;
				var assemblies = reference_assemblies ?? GetAppDomainAssemblies();
				assembliesInScope = assemblies.Select(r => new AssemblyInfo { Name = r.GetName(), Assembly = r }).ToList();
				return assembliesInScope;
			}
		}

		IEnumerable<Assembly> GetAppDomainAssemblies()
		{
			try
			{
				var appDomainType = Type.GetType("System.AppDomain", false);
				if (appDomainType == null)
					return Enumerable.Empty<Assembly>();
				var getCurrentDomain = appDomainType.GetRuntimeProperty("CurrentDomain");
				if (getCurrentDomain == null)
					return Enumerable.Empty<Assembly>();
				var domain = getCurrentDomain.GetValue(null, null);

				var getAssemblies = domain.GetType().GetRuntimeMethod("GetAssemblies", new Type[] { });
				if (getAssemblies == null)
					return Enumerable.Empty<Assembly>();
				var assemblies = getAssemblies.Invoke(domain, null) as IEnumerable<Assembly>;
				if (assemblies == null)
					return Enumerable.Empty<Assembly>();
				return assemblies;
			}
			catch
			{
				return Enumerable.Empty<Assembly>();
			}
		}

		public bool SupportMarkupExtensionsWithDuplicateArity { get; private set; }

		internal string GetXamlNamespace(string clrNamespace)
		{
			if (clrNamespace == null) // could happen on nested generic type (see bug #680385-comment#4). Not sure if null is correct though.
				return null;
			if (xaml_nss == null) // fill it first
				GetAllXamlNamespaces();
			List<string> ret;
			return xaml_nss.TryGetValue(clrNamespace, out ret) ? ret.FirstOrDefault() : null;
		}

		public virtual IEnumerable<string> GetAllXamlNamespaces()
		{
			if (xaml_nss == null)
			{
				xaml_nss = new Dictionary<string,List<string>>();
				foreach (var ass in AssembliesInScope)
					FillXamlNamespaces(ass);
			}
			return xaml_nss.Values.SelectMany(r => r).Distinct();
		}

		public virtual ICollection<XamlType> GetAllXamlTypes(string xamlNamespace)
		{
			if (xamlNamespace == null)
				throw new ArgumentNullException("xamlNamespace");
			if (all_xaml_types == null)
			{
				var types = new Dictionary<string,List<XamlType>>();
				foreach (var ass in AssembliesInScope)
					FillAllXamlTypes(types, ass);
				all_xaml_types = types;
			}

			List<XamlType> l;
			if (all_xaml_types.TryGetValue(xamlNamespace, out l))
				return l;
			else
				return empty_xaml_types;
		}

		public virtual string GetPreferredPrefix(string xmlns)
		{
			if (xmlns == null)
				throw new ArgumentNullException("xmlns");
			if (xmlns == XamlLanguage.Xaml2006Namespace)
				return "x";
			if (prefixes == null)
			{
				prefixes = new Dictionary<string,string>();
				foreach (var ass in AssembliesInScope)
					FillPrefixes(ass.Assembly);
			}
			string ret;
			return prefixes.TryGetValue(xmlns, out ret) ? ret : "p"; // default
		}

		protected internal XamlValueConverter<TConverterBase> GetValueConverter<TConverterBase>(Type converterType, XamlType targetType)
			where TConverterBase : class
		{
			return new XamlValueConverter<TConverterBase>(converterType, targetType);
		}

		Dictionary<Pair,XamlDirective> xaml_directives = new Dictionary<Pair,XamlDirective>();

		public virtual XamlDirective GetXamlDirective(string xamlNamespace, string name)
		{
			XamlDirective t;
			var p = new Pair(xamlNamespace, name);
			if (!xaml_directives.TryGetValue(p, out t))
			{
				t = new XamlDirective(xamlNamespace, name);
				xaml_directives.Add(p, t);
			}
			return t;
		}

		public virtual XamlType GetXamlType(Type type)
		{
			XamlType xt;
			if (run_time_types.TryGetValue(type, out xt))
				return xt;

			xt = new XamlType(type, this);

			run_time_types[type] = xt;
			return xt;
		}

		public XamlType GetXamlType(XamlTypeName xamlTypeName)
		{
			if (xamlTypeName == null)
				throw new ArgumentNullException("xamlTypeName");

			var n = xamlTypeName;
			if (n.TypeArguments.Count == 0) // non-generic
				return GetXamlType(n.Namespace, n.Name);

			// generic
			XamlType[] typeArgs = new XamlType [n.TypeArguments.Count];
			for (int i = 0; i < typeArgs.Length; i++)
				typeArgs[i] = GetXamlType(n.TypeArguments[i]);
			return GetXamlType(n.Namespace, n.Name, typeArgs);
		}

		Dictionary<Tuple<string, string>, XamlType> type_lookup = new Dictionary<Tuple<string, string>, XamlType>();

		protected internal virtual XamlType GetXamlType(string xamlNamespace, string name, params XamlType[] typeArguments)
		{
			XamlType ret;
			var key = Tuple.Create(xamlNamespace, name);
			var useLookup = typeArguments == null || typeArguments.Length == 0;
			if (useLookup && type_lookup.TryGetValue(key, out ret))
				return ret;
			
			string dummy;
			if (TryGetCompatibleXamlNamespace(xamlNamespace, out dummy))
				xamlNamespace = dummy;

			ret = ResolveXamlTypeName(xamlNamespace, name, typeArguments, false);

			if (useLookup)
				type_lookup[key] = ret;

			// If the type was not found, it just returns null.
			return ret;
		}

		bool TypeMatches(XamlType t, string ns, string name, XamlType[] typeArgs)
		{
			if (t.PreferredXamlNamespace == ns && t.Name == name && t.TypeArguments.ListEquals(typeArgs))
				return true;
			if (t.IsMarkupExtension)
				return t.PreferredXamlNamespace == ns && t.GetInternalXmlName() == name && t.TypeArguments.ListEquals(typeArgs);
			else
				return false;
		}

		protected internal virtual Assembly OnAssemblyResolve(string assemblyName)
		{
			var aname = new AssemblyName(assemblyName);
			var ainfo = AssembliesInScope.FirstOrDefault(r => r.Name.Matches(aname));
			if (ainfo.Assembly != null)
				return ainfo.Assembly;

			// fallback if not found
#if PCL136
			return Assembly.Load(assemblyName);
#else
			return Assembly.Load(aname);
#endif
		}

		public virtual bool TryGetCompatibleXamlNamespace(string xamlNamespace, out string compatibleNamespace)
		{
			if (xamlNamespace == null)
				throw new ArgumentNullException("xamlNamespace");
			if (compat_nss == null)
			{
				compat_nss = new Dictionary<string,string>();
				foreach (var ass in AssembliesInScope)
					FillCompatibilities(ass.Assembly);
			}
			if (compat_nss.TryGetValue(xamlNamespace, out compatibleNamespace)
			    && GetAllXamlNamespaces().Contains(compatibleNamespace))
				return true;
			if (GetAllXamlNamespaces().Contains(xamlNamespace))
			{
				compatibleNamespace = xamlNamespace;
				return true;
			}
			return false;
		}

		/*
		void OnAssemblyLoaded (object o, AssemblyLoadEventArgs e)
		{
			if (reference_assemblies != null)
				return; // do nothing

			if (xaml_nss != null)
				FillXamlNamespaces (e.LoadedAssembly);
			if (prefixes != null)
				FillPrefixes (e.LoadedAssembly);
			if (compat_nss != null)
				FillCompatibilities (e.LoadedAssembly);
			if (all_xaml_types != null)
				FillAllXamlTypes (e.LoadedAssembly);
		}*/

		// cache updater methods
		void FillXamlNamespaces(AssemblyInfo ass)
		{
			try
			{
				foreach (XmlnsDefinitionAttribute xda in ass.Assembly.GetCustomAttributes(typeof(XmlnsDefinitionAttribute)))
				{
					List<string> namespaces;
					if (!xaml_nss.TryGetValue(xda.ClrNamespace, out namespaces))
					{
						namespaces = new List<string>();
						xaml_nss.Add(xda.ClrNamespace, namespaces);
					}
					namespaces.Add(xda.XmlNamespace);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Error getting namespaces for assembly '{0}': {1}", ass.Name, ex);
			}
		}

		void FillPrefixes(Assembly ass)
		{
			foreach (XmlnsPrefixAttribute xpa in ass.GetCustomAttributes (typeof (XmlnsPrefixAttribute)))
				prefixes.Add(xpa.XmlNamespace, xpa.Prefix);
		}

		void FillCompatibilities(Assembly ass)
		{
			foreach (XmlnsCompatibleWithAttribute xca in ass.GetCustomAttributes (typeof (XmlnsCompatibleWithAttribute)))
				compat_nss.Add(xca.OldNamespace, xca.NewNamespace);
		}

		Type FindType(string xamlNamespace, string name, Type[] genArgs)
		{
			if (genArgs != null)
				name += "`" + genArgs.Length;
			foreach (var ass in AssembliesInScope)
				foreach (XmlnsDefinitionAttribute xda in ass.Assembly.GetCustomAttributes (typeof (XmlnsDefinitionAttribute)))
				{
					if (xamlNamespace != xda.XmlNamespace)
						continue;

					var assembly = ass.Assembly;
					if (!string.IsNullOrEmpty(xda.AssemblyName))
						#if PCL136
						assembly = Assembly.Load (xda.AssemblyName);
						#else
						assembly = Assembly.Load(new AssemblyName(xda.AssemblyName));
					#endif
					var n = xda.ClrNamespace + "." + name;
					var t = assembly.GetType(n);
					if (t == null)
					{
						t = assembly.GetType(n + "Extension");
						if (t != null && !GetXamlType(t).IsMarkupExtension)
							continue;
					}
					if (t != null && t.Namespace == xda.ClrNamespace)
					{
						var ti = t.GetTypeInfo();
						if (!ti.IsNested)
						{
							if (genArgs != null && (!ti.IsGenericType || !ti.GetGenericArguments().SequenceEqual(genArgs)))
								continue;
						
							return t;
						}
					}
				}
			return null;
		}

		void FillAllXamlTypes(Dictionary<string,List<XamlType>> types, AssemblyInfo ass)
		{
			try
			{
				foreach (XmlnsDefinitionAttribute xda in ass.Assembly.GetCustomAttributes (typeof (XmlnsDefinitionAttribute)))
				{
					var l = types.FirstOrDefault(p => p.Key == xda.XmlNamespace).Value;
					if (l == null)
					{
						l = new List<XamlType>();
						types.Add(xda.XmlNamespace, l);
					}
					var assembly = ass.Assembly;
					if (!string.IsNullOrEmpty(xda.AssemblyName))
#if PCL136
						assembly = Assembly.Load (xda.AssemblyName);
#else
						assembly = Assembly.Load(new AssemblyName(xda.AssemblyName));
#endif
					foreach (var t in assembly.GetExportedTypes())
						if (t.Namespace == xda.ClrNamespace && !t.GetTypeInfo().IsNested)
							l.Add(GetXamlType(t));
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Error getting xaml types for assembly '{0}': {1}", ass.Name, ex);
			}
		}

		// XamlTypeName -> Type resolution

		const string clr_ns = "clr-namespace:";
		static readonly int clr_ns_len = clr_ns.Length;
		static readonly int clr_ass_len = "assembly=".Length;

		XamlType ResolveXamlTypeName(string xmlNamespace, string xmlLocalName, XamlType[] typeArguments, bool required)
		{
			if (xmlNamespace == XamlLanguage.Xaml2006Namespace)
			{
				var xt = XamlLanguage.SpecialNames.Find(xmlLocalName, xmlNamespace);
				if (xt == null)
					xt = XamlLanguage.AllTypes.FirstOrDefault(t => TypeMatches(t, xmlNamespace, xmlLocalName, typeArguments));
				if (xt != null)
					return xt;
			}

			Type[] genArgs = null;
			if (typeArguments != null && typeArguments.Length > 0)
			{
				genArgs = (from t in typeArguments
					select t.UnderlyingType).ToArray();
				if (genArgs.Any(t => t == null))
					return null;
			}

			Type ret;
			if (!xmlNamespace.StartsWith(clr_ns, StringComparison.Ordinal))
			{
				ret = FindType(xmlNamespace, xmlLocalName, genArgs);
				if (ret == null)
					return null;
			}
			else
			{
				// convert xml namespace to clr namespace and assembly
				string[] split = xmlNamespace.Split(';');
				if (split.Length != 2 || split[0].Length < clr_ns_len || split[1].Length <= clr_ass_len)
					throw new XamlParseException(string.Format("Cannot resolve runtime namespace from XML namespace '{0}'", xmlNamespace));
				string tns = split[0].Substring(clr_ns_len);
				string aname = split[1].Substring(clr_ass_len);

				string taqn = GetTypeName(tns, xmlLocalName, genArgs);
				var ass = OnAssemblyResolve(aname);
				// MarkupExtension type could omit "Extension" part in XML name.
				ret = ass?.GetType(taqn) ?? ass?.GetType(taqn + "Extension");
				if (required && ret == null)
					throw new XamlParseException(string.Format("Cannot resolve runtime type from XML namespace '{0}', local name '{1}' with {2} type arguments ({3})", xmlNamespace, xmlLocalName, typeArguments != null ? typeArguments.Length : 0, taqn));
			}


			// ensure only the referenced types are allowed
			if (
				ret == null
				|| (reference_assemblies != null && !reference_assemblies.Contains(ret.GetTypeInfo().Assembly))
			)
				return null;

			return GetXamlType(genArgs == null ? ret : ret.MakeGenericType(genArgs));
		}

		static string GetTypeName(string tns, string name, Type[] genArgs)
		{
			string tfn = tns.Length > 0 ? tns + '.' + name : name;
			if (genArgs != null)
				tfn += "`" + genArgs.Length;
			return tfn;
		}

		Dictionary<Tuple<MemberInfo, MemberInfo>, XamlMember> member_cache = new Dictionary<Tuple<MemberInfo, MemberInfo>, XamlMember>();
		Dictionary<ParameterInfo, XamlMember> parameter_cache = new Dictionary<ParameterInfo, XamlMember>();

		[EnhancedXaml]
		protected internal virtual XamlMember GetParameter(ParameterInfo parameterInfo)
		{
			XamlMember member;
			if (parameter_cache.TryGetValue(parameterInfo, out member))
				return member;
			return parameter_cache[parameterInfo] = new XamlMember(parameterInfo, this);
		}

		[EnhancedXaml]
		protected internal virtual XamlMember GetProperty(PropertyInfo propertyInfo)
		{
			var key = new Tuple<MemberInfo, MemberInfo>(propertyInfo, null);
			XamlMember member;
			if (member_cache.TryGetValue(key, out member))
				return member;
			return member_cache[key] = new XamlMember(propertyInfo, this);
		}

		[EnhancedXaml]
		protected internal virtual XamlMember GetEvent(EventInfo eventInfo)
		{
			var key = new Tuple<MemberInfo, MemberInfo>(eventInfo, null);
			XamlMember member;
			if (member_cache.TryGetValue(key, out member))
				return member;
			return member_cache[key] = new XamlMember(eventInfo, this);
		}

		[EnhancedXaml]
		protected internal virtual XamlMember GetAttachableProperty(string attachablePropertyName, MethodInfo getter, MethodInfo setter)
		{
			var key = new Tuple<MemberInfo, MemberInfo>(getter, setter);
			XamlMember member;
			if (member_cache.TryGetValue(key, out member))
				return member;
			return member_cache[key] = new XamlMember(attachablePropertyName, getter, setter, this);
		}

		[EnhancedXaml]
		protected internal virtual XamlMember GetAttachableEvent(string attachablePropertyName, MethodInfo adder)
		{
			var key = new Tuple<MemberInfo, MemberInfo>(adder, null);
			XamlMember member;
			if (member_cache.TryGetValue(key, out member))
				return member;
			return member_cache[key] = new XamlMember(attachablePropertyName, adder, this);
		}

		[EnhancedXaml]
		protected internal virtual ICustomAttributeProvider GetCustomAttributeProvider(Type type)
		{
			return new TypeAttributeProvider(type);
		}

		[EnhancedXaml]
		protected internal virtual ICustomAttributeProvider GetCustomAttributeProvider(MemberInfo member)
		{
			return new MemberAttributeProvider(member);
		}
	}
}
