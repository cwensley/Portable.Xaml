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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Portable.Xaml.Markup;
using Portable.Xaml;
using Portable.Xaml.Schema;
using System.Xml;

namespace Portable.Xaml
{
	internal class ValueSerializerContext : IValueSerializerContext, IXamlSchemaContextProvider, ITypeDescriptorContext
	{
		XamlNameResolver name_resolver = new XamlNameResolver ();
		XamlTypeResolver type_resolver;
		NamespaceResolver namespace_resolver;
		PrefixLookup prefix_lookup;
		XamlSchemaContext sctx;
		IAmbientProvider ambient_provider;
		IProvideValueTarget provideValue;
		IRootObjectProvider rootProvider;
		IDestinationTypeProvider destinationProvider;
		IXamlObjectWriterFactory objectWriterFactory;

		public ValueSerializerContext (PrefixLookup prefixLookup, XamlSchemaContext schemaContext, IAmbientProvider ambientProvider, IProvideValueTarget provideValue, IRootObjectProvider rootProvider, IDestinationTypeProvider destinationProvider, IXamlObjectWriterFactory objectWriterFactory)
		{
			if (prefixLookup == null)
				throw new ArgumentNullException ("prefixLookup");
			if (schemaContext == null)
				throw new ArgumentNullException ("schemaContext");
			prefix_lookup = prefixLookup;
			namespace_resolver = new NamespaceResolver (prefix_lookup.Namespaces);
			type_resolver = new XamlTypeResolver (namespace_resolver, schemaContext);
			sctx = schemaContext;
			ambient_provider = ambientProvider;
			this.provideValue = provideValue;
			this.rootProvider = rootProvider;
			this.destinationProvider = destinationProvider;
			this.objectWriterFactory = objectWriterFactory;
		}

		public object GetService (Type serviceType)
		{
			if (serviceType == typeof (INamespacePrefixLookup))
				return prefix_lookup;
			if (serviceType == typeof (IXamlNamespaceResolver))
				return namespace_resolver;
			if (serviceType == typeof (IXamlNameResolver))
				return name_resolver;
			if (serviceType == typeof (IXamlNameProvider))
				return name_resolver;
			if (serviceType == typeof (IXamlTypeResolver))
				return type_resolver;
			if (serviceType == typeof (IAmbientProvider))
				return ambient_provider;
			if (serviceType == typeof (IXamlSchemaContextProvider))
				return this;
			if (serviceType == typeof (IProvideValueTarget))
				return provideValue;
			if (serviceType == typeof(IRootObjectProvider))
				return rootProvider;
			if (serviceType == typeof(IDestinationTypeProvider))
				return destinationProvider;
			if (serviceType == typeof(IXamlObjectWriterFactory))
				return objectWriterFactory;
			return null;
		}
		
		XamlSchemaContext IXamlSchemaContextProvider.SchemaContext {
			get { return sctx; }
		}

		/*
		public IContainer Container {
			get { throw new NotImplementedException (); }
		}*/
		public object Instance {
			get { throw new NotImplementedException (); }
		}
		public PropertyInfo PropertyDescriptor {
			get { throw new NotImplementedException (); }
		}
		public void OnComponentChanged ()
		{
			throw new NotImplementedException ();
		}
		public bool OnComponentChanging ()
		{
			throw new NotImplementedException ();
		}
		public ValueSerializer GetValueSerializerFor (PropertyInfo descriptor)
		{
			throw new NotImplementedException ();
		}
		public ValueSerializer GetValueSerializerFor (Type type)
		{
			throw new NotImplementedException ();
		}
	}

	internal class XamlTypeResolver : IXamlTypeResolver
	{
		NamespaceResolver ns_resolver;
		XamlSchemaContext schema_context;

		public XamlTypeResolver (NamespaceResolver namespaceResolver, XamlSchemaContext schemaContext)
		{
			ns_resolver = namespaceResolver;
			schema_context = schemaContext;
		}

		public Type Resolve (string typeName)
		{
			var tn = XamlTypeName.Parse (typeName, ns_resolver);
			var xt = schema_context.GetXamlType (tn);
			return xt != null ? xt.UnderlyingType : null;
		}
	}

	internal class NamespaceResolver : IXamlNamespaceResolver
	{
		public NamespaceResolver (IList<NamespaceDeclaration> source)
		{
			this.source = source;
		}
	
		IList<NamespaceDeclaration> source;
	
		public string GetNamespace (string prefix)
		{
			foreach (var nsd in source)
				if (nsd.Prefix == prefix)
					return nsd.Namespace;
			return null;
		}
	
		public IEnumerable<NamespaceDeclaration> GetNamespacePrefixes ()
		{
			return source;
		}
	}
}
