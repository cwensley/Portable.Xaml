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
using System.ComponentModel;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SystemValueSerializerContext")]

namespace Portable.Xaml
{
	internal class ValueSerializerContext : IValueSerializerContext, IXamlSchemaContextProvider, ITypeDescriptorContext
	{
		XamlNameResolver name_resolver;
		XamlTypeResolver type_resolver;
		NamespaceResolver namespace_resolver;
		PrefixLookup prefix_lookup;
		XamlSchemaContext sctx;
		IAmbientProvider ambient_provider;
		IProvideValueTarget provideValue;
		IRootObjectProvider rootProvider;
		IDestinationTypeProvider destinationProvider;
		IXamlObjectWriterFactory objectWriterFactory;

#if !HAS_TYPE_CONVERTER

		static bool s_valueSerializerTypeInitialized;
		static Type s_valueSerializerType;

		static Type GetValueSerializerType()
		{
			if (s_valueSerializerTypeInitialized)
				return s_valueSerializerType;
			s_valueSerializerTypeInitialized = true;

			// use reflection.emit to create a subclass of ValueSerializerContext that implements 
			// System.ComponentModel.ITypeDescriptorContext since we can't access it here.
			var typeSignature = "SystemValueSerializerContext";

			var appDomainType = Type.GetType("System.AppDomain, mscorlib");
			var assemblyBuilderAccess = Type.GetType("System.Reflection.Emit.AssemblyBuilderAccess, mscorlib");
			var typeAttributesType = Type.GetType("System.Reflection.TypeAttributes, mscorlib");
			var currentDomainProp = appDomainType?.GetRuntimeProperty("CurrentDomain");
			var typeDescriptorContentType = Type.GetType("System.ComponentModel.ITypeDescriptorContext, System");
			var containerType = Type.GetType("System.ComponentModel.IContainer, System");
			var propertyDescriptorType = Type.GetType("System.ComponentModel.PropertyDescriptor, System");
			if (appDomainType == null
				|| assemblyBuilderAccess == null
				|| typeAttributesType == null
				|| currentDomainProp == null
			    || typeDescriptorContentType == null
			    || containerType == null
			    || propertyDescriptorType == null
			   )
				return null;

			object currentDomain = currentDomainProp.GetValue(null);

			dynamic assemblyBuilder = appDomainType
				.GetRuntimeMethod("DefineDynamicAssembly", new Type[] { typeof(AssemblyName), assemblyBuilderAccess })
				.Invoke(currentDomain, new object[] { new AssemblyName(typeSignature), 1 });

			object moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

			dynamic typeBuilder = moduleBuilder
				.GetType()
				.GetRuntimeMethod("DefineType", new Type[] { typeof(string), typeAttributesType, typeof(Type) })
				.Invoke(moduleBuilder, new object[] { typeSignature, 0, typeof(ValueSerializerContext) }); // 0 = Class

			typeBuilder.AddInterfaceImplementation(typeDescriptorContentType);

			Type notImplementedException = typeof(NotImplementedException);
			var notImplementedExceptionConstructor = notImplementedException.GetTypeInfo().GetConstructors().First(r => r.GetParameters().Length == 0);

			var getSetAttr = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual;;
			//public IContainer Container => throw new NotImplementedException();
			var propertyBuilder = typeBuilder.DefineProperty("Container", PropertyAttributes.None, containerType, null);
			var getter = typeBuilder.DefineMethod("get_Container", getSetAttr, containerType, null);
			var il = getter.GetILGenerator();
			il.ThrowException(notImplementedException);
			//il.Emit(OpCodes.Ldnull);
			//il.Emit(OpCodes.Ret);
			propertyBuilder.SetGetMethod(getter);

			//public PropertyDescriptor PropertyDescriptor => throw new NotImplementedException();
			propertyBuilder = typeBuilder.DefineProperty("PropertyDescriptor", PropertyAttributes.None, propertyDescriptorType, null);
			getter = typeBuilder.DefineMethod("get_PropertyDescriptor", getSetAttr, propertyDescriptorType, null);
			il = getter.GetILGenerator();
			il.ThrowException(notImplementedException);
			//il.Emit(OpCodes.Ldnull);
			//il.Emit(OpCodes.Ret);
			propertyBuilder.SetGetMethod(getter);

			s_valueSerializerType = typeBuilder.CreateType();
			return s_valueSerializerType;
		}
#endif

		public static ValueSerializerContext Create(PrefixLookup prefixLookup, XamlSchemaContext schemaContext, IAmbientProvider ambientProvider, IProvideValueTarget provideValue, IRootObjectProvider rootProvider, IDestinationTypeProvider destinationProvider, IXamlObjectWriterFactory objectWriterFactory)
		{
#if !HAS_TYPE_CONVERTER
			ValueSerializerContext context;
			var type = GetValueSerializerType();
			if (type != null)
				context = Activator.CreateInstance(type) as ValueSerializerContext;
			else
				context = new ValueSerializerContext();
#else
			var context = new ValueSerializerContext();
#endif
			context.Initialize(prefixLookup, schemaContext, ambientProvider, provideValue, rootProvider, destinationProvider, objectWriterFactory);
			return context;
		}

		void Initialize(PrefixLookup prefixLookup, XamlSchemaContext schemaContext, IAmbientProvider ambientProvider, IProvideValueTarget provideValue, IRootObjectProvider rootProvider, IDestinationTypeProvider destinationProvider, IXamlObjectWriterFactory objectWriterFactory)
		{
			prefix_lookup = prefixLookup ?? throw new ArgumentNullException("prefixLookup");
			sctx = schemaContext ?? throw new ArgumentNullException("schemaContext");
			ambient_provider = ambientProvider;
			this.provideValue = provideValue;
			this.rootProvider = rootProvider;
			this.destinationProvider = destinationProvider;
			this.objectWriterFactory = objectWriterFactory;
		}

		NamespaceResolver NamespaceResolver => namespace_resolver ?? (namespace_resolver = new NamespaceResolver(prefix_lookup.Namespaces));

		XamlTypeResolver TypeResolver => type_resolver ?? (type_resolver = new XamlTypeResolver(NamespaceResolver, sctx));

		XamlNameResolver NameResolver => name_resolver ?? (name_resolver = new XamlNameResolver());

		public object GetService(Type serviceType)
		{
			if (serviceType == typeof(INamespacePrefixLookup))
				return prefix_lookup;
			if (serviceType == typeof(IXamlNamespaceResolver))
				return NamespaceResolver;
			if (serviceType == typeof(IXamlNameResolver) || serviceType == typeof(IXamlNameProvider))
				return NameResolver;
			if (serviceType == typeof(IXamlTypeResolver))
				return TypeResolver;
			if (serviceType == typeof(IAmbientProvider))
				return ambient_provider;
			if (serviceType == typeof(IXamlSchemaContextProvider))
				return this;
			if (serviceType == typeof(IProvideValueTarget))
				return provideValue;
			if (serviceType == typeof(IRootObjectProvider))
				return rootProvider;
			if (serviceType == typeof(IDestinationTypeProvider))
				return destinationProvider;
			if (serviceType == typeof(IXamlObjectWriterFactory))
				return objectWriterFactory;
			return null;
		}

		XamlSchemaContext IXamlSchemaContextProvider.SchemaContext => sctx;

		public virtual object Instance => throw new NotImplementedException();

#if HAS_TYPE_CONVERTER
		public IContainer Container => throw new NotImplementedException();

		public PropertyDescriptor PropertyDescriptor => throw new NotImplementedException();
#else
		public PropertyInfo PropertyDescriptor => throw new NotImplementedException();
#endif

		public virtual void OnComponentChanged()
		{
			throw new NotImplementedException();
		}
		public virtual bool OnComponentChanging ()
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
