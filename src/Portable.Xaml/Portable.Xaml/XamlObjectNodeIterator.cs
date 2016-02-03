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
using System.Collections;
using System.Collections.Generic;
using Portable.Xaml.ComponentModel;
using System.IO;
using System.Linq;
using Portable.Xaml.Markup;
using Portable.Xaml;
using Portable.Xaml.Schema;
using System.Xml;
using System.Xml.Serialization;
using System.Reflection;

namespace Portable.Xaml
{
	internal class XamlObjectNodeIterator
	{
		static readonly XamlObject null_object = new XamlObject (XamlLanguage.Null, null);

		public XamlObjectNodeIterator (object root, XamlSchemaContext schemaContext, IValueSerializerContext vctx)
		{
			ctx = schemaContext;
			this.root = root;
			value_serializer_ctx = vctx;
		}
		
		XamlSchemaContext ctx;
		object root;
		IValueSerializerContext value_serializer_ctx;
		
		PrefixLookup PrefixLookup {
			get { return (PrefixLookup) value_serializer_ctx.GetService (typeof (INamespacePrefixLookup)); }
		}
		XamlNameResolver NameResolver {
			get { return (XamlNameResolver) value_serializer_ctx.GetService (typeof (IXamlNameResolver)); }
		}

		public XamlSchemaContext SchemaContext {
			get { return ctx; }
		}
		
		XamlType GetType (object obj)
		{
			return obj == null ? XamlLanguage.Null : ctx.GetXamlType (obj.GetType ());
		}
		
		// returns StartObject, StartMember, Value, EndMember and EndObject. (NamespaceDeclaration is not included)
		public IEnumerable<XamlNodeInfo> GetNodes ()
		{
			var xobj = new XamlObject (GetType (root), root);
			foreach (var node in GetNodes (null, xobj))
				yield return node;
		}
		
		IEnumerable<XamlNodeInfo> GetNodes (XamlMember xm, XamlObject xobj)
		{
			return GetNodes (xm, xobj, null, false);
		}

		IEnumerable<XamlNodeInfo> GetNodes (XamlMember xm, XamlObject xobj, XamlType overrideMemberType, bool partOfPositionalParameters)
		{
			// collection items: each item is exposed as a standalone object that has StartObject, EndObject and contents.
			if (xm == XamlLanguage.Items) {
				foreach (var xn in GetItemsNodes (xm, xobj))
					yield return xn;
				yield break;
			}
			
			// Arguments: each argument is written as a standalone object
			if (xm == XamlLanguage.Arguments) {
				foreach (var argm in xobj.Type.GetSortedConstructorArguments ()) {
					var argv = argm.Invoker.GetValue (xobj.GetRawValue ());
					var xarg = new XamlObject (argm.Type, argv);
					foreach (var cn in GetNodes (null, xarg))
						yield return cn;
				}
				yield break;
			}

			// PositionalParameters: items are from constructor arguments, written as Value node sequentially. Note that not all of them are in simple string value. Also, null values are not written as NullExtension
			if (xm == XamlLanguage.PositionalParameters) {
				foreach (var argm in xobj.Type.GetSortedConstructorArguments ()) {
					foreach (var cn in GetNodes (argm, new XamlObject (argm.Type, xobj.GetMemberValue (argm)), null, true))
						yield return cn;
				}
				yield break;
			}

			if (xm == XamlLanguage.Initialization) {
				yield return new XamlNodeInfo (TypeExtensionMethods.GetStringValue (xobj.Type, xm, xobj.GetRawValue (), value_serializer_ctx));
				yield break;
			}

			// Value - only for non-top-level node (thus xm != null)
			if (xm != null) {
				// overrideMemberType is (so far) used for XamlLanguage.Key.
				var xtt = overrideMemberType ?? xm.Type;
				if (!xtt.IsMarkupExtension && // this condition is to not serialize MarkupExtension whose type has TypeConverterAttribute (e.g. StaticExtension) as a string.
				    (xtt.IsContentValue (value_serializer_ctx) || xm.IsContentValue (value_serializer_ctx))) {
					// though null value is special: it is written as a standalone object.
					var val = xobj.GetRawValue ();
					if (val == null) {
						if (!partOfPositionalParameters)
							foreach (var xn in GetNodes (null, null_object))
								yield return xn;
						else
							yield return new XamlNodeInfo (String.Empty);
					}
					else
						yield return new XamlNodeInfo (TypeExtensionMethods.GetStringValue (xtt, xm, val, value_serializer_ctx));
					yield break;
				}
			}

			// collection items: return GetObject and Items.
			if (xm != null && xm.Type.IsCollection && !xm.IsWritePublic) {
				yield return new XamlNodeInfo (XamlNodeType.GetObject, xobj);
				// Write Items member only when there are items (i.e. do not write it if it is empty).
				var xnm = new XamlNodeMember (xobj, XamlLanguage.Items);
				var en = GetNodes (XamlLanguage.Items, xnm.Value).GetEnumerator ();
				if (en.MoveNext ()) {
					yield return new XamlNodeInfo (XamlNodeType.StartMember, xnm);
					do {
						yield return en.Current;
					} while (en.MoveNext ());
					yield return new XamlNodeInfo (XamlNodeType.EndMember, xnm);
				}
				yield return new XamlNodeInfo (XamlNodeType.EndObject, xobj);
			} else if (xm != null && xm.Type.IsXData) {
				var sw = new StringWriter ();
				var xw = XmlWriter.Create (sw, new XmlWriterSettings () { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Auto });
				var val = xobj.GetRawValue () as IXmlSerializable;
				if (val == null)
					yield break; // do not output anything
				val.WriteXml (xw);
				xw.Dispose();
				var obj = new XData () { Text = sw.ToString () };
				foreach (var xn in GetNodes (null, new XamlObject (XamlLanguage.XData, obj)))
					yield return xn;
			} else {
				// Object - could become Reference
				var val2 = xobj.GetRawValue ();
				if (val2 != null && xobj.Type != XamlLanguage.Reference) {
					
					if (!xobj.Type.IsContentValue (value_serializer_ctx)) {
						string refName = NameResolver.GetReferenceName (xobj, val2);
						if (refName != null) {
							// The target object is already retrieved, so we don't return the same object again.
							NameResolver.SaveAsReferenced (val2); // Record it as named object.
							// Then return Reference object instead.
							foreach (var xn in GetNodes (null, new XamlObject (XamlLanguage.Reference, new Reference (refName))))
								yield return xn;
							yield break;
						} else {
							// The object appeared in the xaml tree for the first time. So we store the reference with a unique name so that it could be referenced later.
							NameResolver.SetNamedObject (val2, true); // probably fullyInitialized is always true here.
						}
					}
					yield return new XamlNodeInfo (XamlNodeType.StartObject, xobj);

					// If this object is referenced and there is no [RuntimeNameProperty] member, then return Name property in addition.
					if (!NameResolver.IsCollectingReferences && xobj.Type.GetAliasedProperty (XamlLanguage.Name) == null) {
						string name = NameResolver.GetReferencedName (xobj, val2);
						if (name != null) {
							var sobj = new XamlObject (XamlLanguage.String, name);
							foreach (var cn in GetMemberNodes (new XamlNodeMember (sobj, XamlLanguage.Name), new [] { new XamlNodeInfo (name)}))
								yield return cn;
						}
					}
				}
				else
					yield return new XamlNodeInfo (XamlNodeType.StartObject, xobj);


				foreach (var xn in GetObjectMemberNodes (xobj))
					yield return xn;
				
				yield return new XamlNodeInfo (XamlNodeType.EndObject, xobj);
			}
		}
		

		IEnumerable<XamlNodeInfo> GetMemberNodes (XamlNodeMember member, IEnumerable<XamlNodeInfo> contents)
		{
				yield return new XamlNodeInfo (XamlNodeType.StartMember, member);
				foreach (var cn in contents)
					yield return cn;
				yield return new XamlNodeInfo (XamlNodeType.EndMember, member);
		}

		IEnumerable<XamlNodeMember> GetNodeMembers (XamlObject xobj, IValueSerializerContext vsctx)
		{
			// XData.XmlReader is not returned.
			if (xobj.Type == XamlLanguage.XData) {
				yield return new XamlNodeMember (xobj, XamlLanguage.XData.GetMember ("Text"));
				yield break;
			}

			// FIXME: find out why root Reference has PositionalParameters.
			if (xobj.GetRawValue () != root && xobj.Type == XamlLanguage.Reference)
				yield return new XamlNodeMember (xobj, XamlLanguage.PositionalParameters);
			else {
				var inst = xobj.GetRawValue ();
				var atts = new KeyValuePair<AttachableMemberIdentifier,object> [AttachablePropertyServices.GetAttachedPropertyCount (inst)];
				AttachablePropertyServices.CopyPropertiesTo (inst, atts, 0);
				foreach (var p in atts) {
					var axt = ctx.GetXamlType (p.Key.DeclaringType);
					yield return new XamlNodeMember (new XamlObject (axt, p.Value), axt.GetAttachableMember (p.Key.MemberName));
				}
				foreach (var xm in xobj.Type.GetAllObjectReaderMembersByType (vsctx))
					yield return new XamlNodeMember (xobj, xm);
			}
		}

		IEnumerable<XamlNodeInfo> GetObjectMemberNodes (XamlObject xobj)
		{
			var xce = GetNodeMembers (xobj, value_serializer_ctx).GetEnumerator ();
			while (xce.MoveNext ()) {
				// XamlLanguage.Items does not show up if the content is empty.
				if (xce.Current.Member == XamlLanguage.Items) {
					// FIXME: this is nasty, but this name resolution is the only side effect of this iteration model. Save-Restore procedure is required.
					NameResolver.Save ();
					try {
						if (!GetNodes (xce.Current.Member, xce.Current.Value).GetEnumerator ().MoveNext ())
							continue;
					} finally {
						NameResolver.Restore ();
					}
				}

				// Other collections as well, but needs different iteration (as nodes contain GetObject and EndObject).
				if (!xce.Current.Member.IsWritePublic && xce.Current.Member.Type != null && xce.Current.Member.Type.IsCollection) {
					var e = GetNodes (xce.Current.Member, xce.Current.Value).GetEnumerator ();
					// FIXME: this is nasty, but this name resolution is the only side effect of this iteration model. Save-Restore procedure is required.
					NameResolver.Save ();
					try {
						if (!(e.MoveNext () && e.MoveNext () && e.MoveNext ())) // GetObject, EndObject and more
							continue;
					} finally {
						NameResolver.Restore ();
					}
				}

				foreach (var cn in GetMemberNodes (xce.Current, GetNodes (xce.Current.Member, xce.Current.Value)))
					yield return cn;
			}
		}

		IEnumerable<XamlNodeInfo> GetItemsNodes (XamlMember xm, XamlObject xobj)
		{
			var obj = xobj.GetRawValue ();
			if (obj == null)
				yield break;
			var ie = xobj.Type.Invoker.GetItems (obj);
			while (ie.MoveNext ()) {
				var iobj = ie.Current;
				// If it is dictionary, then retrieve the key, and rewrite the item as the Value part.
				object ikey = null;
				if (xobj.Type.IsDictionary) {
					Type kvpType = iobj.GetType ();
					bool isNonGeneric = kvpType == typeof (DictionaryEntry);
					var kp = isNonGeneric ? null : kvpType.GetRuntimeProperty ("Key");
					var vp = isNonGeneric ? null : kvpType.GetRuntimeProperty ("Value");
					ikey = isNonGeneric ? ((DictionaryEntry) iobj).Key : kp.GetValue (iobj, null);
					iobj = isNonGeneric ? ((DictionaryEntry) iobj).Value : vp.GetValue (iobj, null);
				}

				var wobj = TypeExtensionMethods.GetExtensionWrapped (iobj);
				var xiobj = new XamlObject (GetType (wobj), wobj);
				if (ikey != null) {

					var en = GetNodes (null, xiobj).ToList ();
					yield return en [0]; // StartObject

					var xknm = new XamlNodeMember (xobj, XamlLanguage.Key);
					var nodes1 = en.Skip (1).Take (en.Count - 2);
					var nodes2 = GetKeyNodes (ikey, xobj.Type.KeyType, xknm);

					// group the members then sort to put the key nodes in the correct order
					var grouped = GroupMemberNodes (nodes1.Concat (nodes2))
            .OrderBy (r => r.Item1, TypeExtensionMethods.MemberComparer);
					foreach (var item in grouped) {
						foreach (var node in item.Item2)
							yield return node;
					}

					yield return en [en.Count - 1]; // EndObject
				}
				else
					foreach (var xn in GetNodes (null, xiobj))
						yield return xn;
			}
		}

		IEnumerable<XamlNodeInfo> GetMemberNodes(IEnumerator<XamlNodeInfo> e)
		{
			int nest = 1;
			yield return e.Current;
			while (e.MoveNext ()) {
				if (e.Current.NodeType == XamlNodeType.StartMember) {
					nest++;
				} else if (e.Current.NodeType == XamlNodeType.EndMember) {
					nest--;
					if (nest == 0) {
						yield return e.Current;
						break;
					}
				}
				yield return e.Current;
			}
		}

		IEnumerable<Tuple<XamlMember, IEnumerable<XamlNodeInfo>>> GroupMemberNodes(IEnumerable<XamlNodeInfo> nodes)
		{
			var e1 = nodes.GetEnumerator ();

			while (e1.MoveNext ()) {
        if (e1.Current.NodeType == XamlNodeType.StartMember)
        {
          // split into chunks by member
          yield return Tuple.Create(e1.Current.Member.Member, (IEnumerable<XamlNodeInfo>)GetMemberNodes(e1).ToList());
        }
        else
          throw new InvalidOperationException("Unexpected node");
			}
		}
		
		IEnumerable<XamlNodeInfo> GetKeyNodes (object ikey, XamlType keyType, XamlNodeMember xknm)
		{
			foreach (var xn in GetMemberNodes (xknm, GetNodes (XamlLanguage.Key, new XamlObject (GetType (ikey), ikey), keyType, false)))
				yield return xn;
		}

		// Namespace and Reference retrieval.
		// It is iterated before iterating the actual object nodes,
		// and results are cached for use in XamlObjectReader.
		public void PrepareReading ()
		{
			PrefixLookup.IsCollectingNamespaces = true;
			NameResolver.IsCollectingReferences = true;
			foreach (var xn in GetNodes ()) {
				if (xn.NodeType == XamlNodeType.GetObject)
					continue; // it is out of consideration here.
				if (xn.NodeType == XamlNodeType.StartObject) {
					foreach (var ns in NamespacesInType (xn.Object.Type))
						PrefixLookup.LookupPrefix (ns);
				} else if (xn.NodeType == XamlNodeType.StartMember) {
					var xm = xn.Member.Member;
					// This filtering is done as a black list so far. There does not seem to be any usable property on XamlDirective.
					if (xm == XamlLanguage.Items || xm == XamlLanguage.PositionalParameters || xm == XamlLanguage.Initialization)
						continue;
					PrefixLookup.LookupPrefix (xn.Member.Member.PreferredXamlNamespace);
				} else {
					if (xn.NodeType == XamlNodeType.Value && xn.Value is Type)
						// this tries to lookup existing prefix, and if there isn't any, then adds a new declaration.
						TypeExtensionMethods.GetStringValue (XamlLanguage.Type, xn.Member.Member, xn.Value, value_serializer_ctx);
					continue;
				}
			}
			PrefixLookup.Namespaces.Sort ((nd1, nd2) => String.CompareOrdinal (nd1.Prefix, nd2.Prefix));
			PrefixLookup.IsCollectingNamespaces = false;
			NameResolver.IsCollectingReferences = false;
			NameResolver.NameScopeInitializationCompleted (this);
		}
		
		IEnumerable<string> NamespacesInType (XamlType xt)
		{
			yield return xt.PreferredXamlNamespace;
			if (xt.TypeArguments != null) {
				// It is for x:TypeArguments
				yield return XamlLanguage.Xaml2006Namespace;
				foreach (var targ in xt.TypeArguments)
					foreach (var ns in NamespacesInType (targ))
						yield return ns;
			}
		}
	}
}
