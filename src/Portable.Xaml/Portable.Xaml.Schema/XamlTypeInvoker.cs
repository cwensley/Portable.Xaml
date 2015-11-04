//
// Copyright (C) 2010 Novell Inc. http://novell.com
// Copyright (C) 2012 Xamarin Inc. http://xamarin.com
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Portable.Xaml.Markup;
using System.Linq;

namespace Portable.Xaml.Schema
{
	public class XamlTypeInvoker
	{
		static readonly XamlTypeInvoker unknown = new XamlTypeInvoker ();
		public static XamlTypeInvoker UnknownInvoker {
			get { return unknown; }
		}

		protected XamlTypeInvoker ()
		{
		}
		
		public XamlTypeInvoker (XamlType type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
			this.type = type;
		}
		
		XamlType type;

		void ThrowIfUnknown ()
		{
			if (type == null || type.UnderlyingType == null)
				throw new NotSupportedException (String.Format ("Current operation is valid only when the underlying type on a XamlType is known, but it is unknown for '{0}'", type));
		}

		public EventHandler<XamlSetMarkupExtensionEventArgs> SetMarkupExtensionHandler {
			get { return type == null ? null : type.SetMarkupExtensionHandler; }
		}

		public EventHandler<XamlSetTypeConverterEventArgs> SetTypeConverterHandler {
			get { return type == null ? null : type.SetTypeConverterHandler; }
		}

		Dictionary<Tuple<Type, Type, Type>, MethodInfo> add_method_cache = new Dictionary<Tuple<Type, Type, Type>, MethodInfo>();

		public virtual void AddToCollection (object instance, object item)
		{
			if (instance == null)
				throw new ArgumentNullException ("instance");
			if (item == null)
				throw new ArgumentNullException ("item");

			var collectionType = instance.GetType ();
			var itemType = item.GetType();
			var key = Tuple.Create(collectionType, itemType, (Type)null);

            MethodInfo mi = null;
			if (!add_method_cache.TryGetValue(key, out mi))
			{
				// FIXME: this method lookup should be mostly based on GetAddMethod(). At least iface method lookup must be done there.
				if (type != null && type.UnderlyingType != null)
				{
					var xct = type.SchemaContext.GetXamlType(collectionType);
					if (!xct.IsCollection) // not sure why this check is done only when UnderlyingType exists...
						throw new NotSupportedException(String.Format("Non-collection type '{0}' does not support this operation", xct));
					if (collectionType.GetTypeInfo().IsAssignableFrom(type.UnderlyingType.GetTypeInfo()))
						mi = GetAddMethod(type.SchemaContext.GetXamlType(itemType));
				}

				if (mi == null)
				{
					if (collectionType.GetTypeInfo().IsGenericType)
					{
						mi = collectionType.GetRuntimeMethod("Add", collectionType.GetTypeInfo().GetGenericArguments());
						if (mi == null)
							mi = LookupAddMethod(collectionType, typeof(ICollection<>).MakeGenericType(collectionType.GetTypeInfo().GetGenericArguments()));
					}
					else
					{
						mi = collectionType.GetRuntimeMethod("Add", new Type[] { typeof(object) });
						if (mi == null)
							mi = LookupAddMethod(collectionType, typeof(IList));
					}
				}

				if (mi == null)
					throw new InvalidOperationException(String.Format("The collection type '{0}' does not have 'Add' method", collectionType));
				add_method_cache[key] = mi;
            }
			
			mi.Invoke (instance, new object [] {item});
		}

		public virtual void AddToDictionary (object instance, object key, object item)
		{
			if (instance == null)
				throw new ArgumentNullException ("instance");

			var t = instance.GetType ();
			// FIXME: this likely needs similar method lookup to AddToCollection().
			var lookupKey = Tuple.Create(t, key?.GetType(), item?.GetType());
			MethodInfo mi = null;
			if (!add_method_cache.TryGetValue(lookupKey, out mi))
			{

				if (t.GetTypeInfo().IsGenericType)
				{
					mi = instance.GetType().GetRuntimeMethod("Add", t.GetTypeInfo().GetGenericArguments());
					if (mi == null)
						mi = LookupAddMethod(t, typeof(IDictionary<,>).MakeGenericType(t.GetTypeInfo().GetGenericArguments()));
				}
				else
				{
					mi = instance.GetType().GetRuntimeMethod("Add", new Type[] { typeof(object), typeof(object) });
					if (mi == null)
						mi = LookupAddMethod(t, typeof(IDictionary));
				}
				add_method_cache[lookupKey] = mi;
			}
			mi.Invoke (instance, new object [] {key, item});
		}
		
		MethodInfo LookupAddMethod (Type ct, Type iface)
		{
			var map = ct.GetTypeInfo().GetRuntimeInterfaceMap(iface);
			for (int i = 0; i < map.TargetMethods.Length; i++)
				if (map.InterfaceMethods [i].Name == "Add")
					return map.TargetMethods [i];
			return null;
		}

		public virtual object CreateInstance (object [] arguments)
		{
			ThrowIfUnknown ();
			if (arguments == null)
				return Activator.CreateInstance(type.UnderlyingType);
			else
				return Activator.CreateInstance (type.UnderlyingType, arguments);
		}

		public virtual MethodInfo GetAddMethod (XamlType contentType)
		{
			return type == null || type.UnderlyingType == null || type.ItemType == null || type.LookupCollectionKind () == XamlCollectionKind.None ? null : type.UnderlyingType.GetRuntimeMethod ("Add", new Type [] {contentType.UnderlyingType});
		}

		public virtual MethodInfo GetEnumeratorMethod ()
		{
			return type.UnderlyingType == null || type.LookupCollectionKind() == XamlCollectionKind.None ? null : type.UnderlyingType.GetRuntimeMethod("GetEnumerator", new Type[0]);
		}
		
		public virtual IEnumerator GetItems (object instance)
		{
			if (instance == null)
				throw new ArgumentNullException ("instance");
			return ((IEnumerable) instance).GetEnumerator ();
		}
	}
}
