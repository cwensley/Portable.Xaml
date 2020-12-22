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
	internal class PrefixLookup : INamespacePrefixLookup
	{
		public PrefixLookup (XamlSchemaContext schemaContext)
		{
			sctx = schemaContext;
			Namespaces = new List<NamespaceDeclaration> ();
		}
		
		XamlSchemaContext sctx;
		
		public bool IsCollectingNamespaces { get; set; }
		
		public List<NamespaceDeclaration> Namespaces { get; private set; }

		public string LookupPrefix (string ns)
		{
			NamespaceDeclaration nd = null;
			foreach (var nsd in Namespaces)
			{
				if (nsd.Namespace == ns)
				{
					nd = nsd;
					break;
				}
			}

			if (nd == null && IsCollectingNamespaces)
				return AddNamespace (ns);
			else
				return nd != null ? nd.Prefix : null;
		}
		
		public string AddNamespace (string ns)
		{
			var l = Namespaces;
			string prefix, s;
			if (ns == XamlLanguage.Xaml2006Namespace)
				prefix = "x";
			else if (!AnyHavePrefix(l, String.Empty))
				prefix = String.Empty;
			else
			{
				s = GetAcronym(ns) ?? sctx.GetPreferredPrefix(ns);
				prefix = AnyHavePrefix(l, s) ? MakePrefixAddNumber(l, s) : s;
			}
			l.Add (new NamespaceDeclaration (ns, prefix));
			return prefix;
		}

		string MakePrefixAddNumber(List<NamespaceDeclaration> namespaces, string prefix) 
		{
			var prefixLen = prefix.Length;

			int max = 0;
			for (int i = 0; i < namespaces.Count; i++)
			{
				var p = namespaces[i].Prefix;
				var suffix = !p.StartsWith(prefix) ? 0 :
						     int.TryParse(p.Substring(prefixLen), out var idx) ? idx :
						     0;
				max = Math.Max(suffix, max);
			}

			return prefix + (max+1);
		}

		bool AnyHavePrefix(List<NamespaceDeclaration> namespaces, string prefix)
		{
			for (int i = 0; i < namespaces.Count; i++)
			{
				var ns = namespaces[i];
				if (ns.Prefix == prefix)
					return true;
			}
			return false;
		}

		const string pre = "clr-namespace:";

		string GetAcronym (string ns)
		{
			int idx = ns.IndexOf (';');
			if (idx < 0)
				return null;
			if (!ns.StartsWith (pre, StringComparison.Ordinal))
				return null;
			ns = ns.Substring (pre.Length, idx - pre.Length);
			string ac = "";
			foreach (string nsp in ns.Split ('.'))
				if (nsp.Length > 0)
					ac += nsp [0];
			return ac.Length > 0 ? ac.ToLower () : null;
		}
	}
}
