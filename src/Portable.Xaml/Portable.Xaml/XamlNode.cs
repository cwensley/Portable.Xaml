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
using System.Globalization;
using System.Linq;
using System.Reflection;
using Portable.Xaml.Markup;
using Portable.Xaml.Schema;
using System.Xml;

namespace Portable.Xaml
{
	class XamlNodeInfo
	{
		public XamlNodeInfo()
		{
		}

		public XamlNodeInfo Set(XamlNodeType nodeType, object value)
		{
			NodeType = nodeType;
			Value = value;
			return this;
		}
		public XamlNodeInfo Set(object value)
		{
			NodeType = XamlNodeType.Value;
			Value = value;
			return this;
		}

		public XamlNodeInfo Set(XamlNodeInfo node)
		{
			NodeType = node.NodeType;
			Value = node.Value;
			return this;
		}

		public XamlNodeInfo(XamlNodeType nodeType, XamlObject value)
		{
			NodeType = nodeType;
			Value = value;
		}

		public XamlNodeInfo(XamlNodeType nodeType, XamlNodeMember member)
		{
			NodeType = nodeType;
			Value = member;
		}

		public XamlNodeInfo(object value)
		{
			NodeType = XamlNodeType.Value;
			Value = value;
		}

		public XamlNodeInfo(NamespaceDeclaration ns)
		{
			NodeType = XamlNodeType.NamespaceDeclaration;
			Value = ns;
		}

		public XamlNodeType NodeType { get; private set; }

		public XamlObject Object => (XamlObject)Value;

		public XamlNodeMember Member => (XamlNodeMember)Value;

		public object Value { get; private set; }

		public XamlNodeInfo Copy() => new XamlNodeInfo().Set(this);
	}

	struct XamlNodeLineInfo
	{
		public readonly XamlNodeInfo Node;
		public readonly int LineNumber, LinePosition;
		public XamlNodeLineInfo (XamlNodeInfo node, int line, int column)
		{
			Node = node;
			LineNumber = line;
			LinePosition = column;
		}
	}
	
	class XamlObject
	{
		public XamlObject()
		{
		}

		public XamlObject Set(XamlType type, object instance)
		{
			Type = type;
			Value = instance;
			return this;
		}

		public XamlObject (XamlType type, object instance)
		{
			Type = type;
			Value = instance;
		}
		
		public object Value { get; private set; }
		
		public XamlType Type { get; private set; }

		public object RawValue => Value;

		public object GetMemberValue(XamlMember xm)
		{
			if (xm.IsUnknown)
				return null;

			var obj = RawValue;
			// FIXME: this looks like an ugly hack. Is this really true? What if there's MarkupExtension that uses another MarkupExtension type as a member type.
			if (xm.IsAttachable 
				|| xm.IsDirective // is this correct?
				/*
				|| ReferenceEquals(xm, XamlLanguage.Initialization)
				|| ReferenceEquals(xm, XamlLanguage.Items) // collection itself
				|| ReferenceEquals(xm, XamlLanguage.Arguments) // object itself
				|| ReferenceEquals(xm, XamlLanguage.PositionalParameters) // dummy value
				*/
				)
				return obj;
			return xm.Invoker.GetValue(obj);
		}

	}

	class XamlNodeMember
	{
		public XamlNodeMember()
		{
		}

		public XamlNodeMember Set(XamlObject owner, XamlMember member)
		{
			Owner = owner;
			Member = member;
			return this;
		}

		public XamlNodeMember (XamlObject owner, XamlMember member)
		{
			Owner = owner;
			Member = member;
		}

		public XamlObject Owner;

		public XamlMember Member;

		public XamlObject GetValue(XamlObject xobj)
		{
			var mv = Owner.GetMemberValue(Member);
			return xobj.Set(GetType(mv), mv);
		}

		public XamlObject Value
		{
			get
			{
				var mv = Owner.GetMemberValue(Member);
				return new XamlObject(GetType(mv), mv);
			}
		}

		XamlType GetType (object obj)
		{
			return obj == null ? XamlLanguage.Null : Owner.Type.SchemaContext.GetXamlType(obj.GetType());
		}
	}
	
	internal static class TypeExtensionMethods2
	{
		// Note that this returns XamlMember which might not actually appear in XamlObjectReader. For example, XamlLanguage.Items won't be returned when there is no item in the collection.
		public static IEnumerable<XamlMember> GetAllObjectReaderMembersByType (this XamlType type, IValueSerializerContext vsctx)
		{
			if (type.HasPositionalParameters (vsctx)) {
				yield return XamlLanguage.PositionalParameters;
				yield break;
			}

			// Note that if the XamlType has the default constructor, we don't need "Arguments".
			IEnumerable<XamlMember> args = type.ConstructionRequiresArguments ? type.GetSortedConstructorArguments () : null;
			if (args != null && args.Any ())
				yield return XamlLanguage.Arguments;

			if (type.IsContentValue (vsctx)) {
				yield return XamlLanguage.Initialization;
				yield break;
			}

			if (type.IsDictionary) {
				yield return XamlLanguage.Items;
				yield break;
			}

			foreach (var m in type.GetAllMembers ()) {
				// do not read constructor arguments twice (they are written inside Arguments).
				if (args != null && args.Contains (m))
					continue;
				// do not return non-public members (of non-collection/xdata). Not sure why .NET filters out them though.
				if (!m.IsReadPublic)
					continue;
				if (!m.IsWritePublic &&
				    !m.Type.IsXData &&
				    !m.Type.IsArray &&
				    !m.Type.IsCollection &&
				    !m.Type.IsDictionary)
					continue;

				yield return m;
			}
			
			if (type.IsCollection)
				yield return XamlLanguage.Items;
		}
	}
	
}
