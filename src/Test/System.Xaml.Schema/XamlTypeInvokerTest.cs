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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using NUnit.Framework;
#if PORTABLE_XAML
using Portable.Xaml.Markup;
using Portable.Xaml.ComponentModel;
using Portable.Xaml;
using Portable.Xaml.Schema;
#else
using System.Windows.Markup;
using System.ComponentModel;
using System.Xaml;
using System.Xaml.Schema;
#endif

namespace Tests.Portable.Xaml.Schema
{
	[TestFixture]
	public class XamlTypeInvokerTest
	{
		XamlSchemaContext sctx = new XamlSchemaContext (new XamlSchemaContextSettings ());

		[Test]
		public void ConstructorTypeNull ()
		{
			Assert.Throws<ArgumentNullException> (() => new XamlTypeInvoker (null));
		}

		[Test]
		public void DefaultValues ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (object), sctx));
			Assert.IsNull (i.SetMarkupExtensionHandler, "#1");
			Assert.IsNull (i.SetTypeConverterHandler, "#2");
		}

		[XamlSetMarkupExtension ("HandleMarkupExtension")]
		public class TestClassMarkupExtension1
		{
		}
		
		// SetMarkupExtensionHandler
		
		[Test]
		public void SetHandleMarkupExtensionInvalid ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (TestClassMarkupExtension1), sctx));
			Assert.Throws<ArgumentException> (() => { var t = i.SetMarkupExtensionHandler; }, "#1");
		}

		[XamlSetMarkupExtension ("HandleMarkupExtension")]
		public class TestClassMarkupExtension2
		{
			// delegate type mismatch
			void HandleMarkupExtension ()
			{
			}
		}
		
		[Test]
		public void SetHandleMarkupExtensionInvalid2 ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (TestClassMarkupExtension2), sctx));
			Assert.Throws<ArgumentException> (() => { var t = i.SetMarkupExtensionHandler; }, "#1");
		}

		[XamlSetMarkupExtension ("HandleMarkupExtension")]
		public class TestClassMarkupExtension3
		{
			// must be static
			public void HandleMarkupExtension (object o, XamlSetMarkupExtensionEventArgs a)
			{
			}
		}
		
		[Test]
		public void SetHandleMarkupExtensionInvalid3 ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (TestClassMarkupExtension3), sctx));
			Assert.Throws<ArgumentException> (() => { var t = i.SetMarkupExtensionHandler; }, "#1");
		}

		[XamlSetMarkupExtension ("HandleMarkupExtension")]
		public class TestClassMarkupExtension4
		{
			// can be private.
			static void HandleMarkupExtension (object o, XamlSetMarkupExtensionEventArgs a)
			{
			}
		}
		
		[Test]
		public void SetHandleMarkupExtension ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (TestClassMarkupExtension4), sctx));
			Assert.IsNotNull (i.SetMarkupExtensionHandler, "#1");
		}

		// SetTypeConverterHandler
		
		[XamlSetTypeConverter ("HandleTypeConverter")]
		public class TestClassTypeConverter1
		{
		}
		
		[Test]
		public void SetHandleTypeConverterInvalid ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (TestClassTypeConverter1), sctx));
			Assert.Throws<ArgumentException> (() => { var t = i.SetTypeConverterHandler; }, "#1");
		}

		[XamlSetTypeConverter ("HandleTypeConverter")]
		public class TestClassTypeConverter2
		{
			// delegate type mismatch
			void HandleTypeConverter ()
			{
			}
		}
		
		[Test]
		public void SetHandleTypeConverterInvalid2 ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (TestClassTypeConverter2), sctx));
			Assert.Throws<ArgumentException> (() => { var t = i.SetTypeConverterHandler; }, "#1");
		}

		[XamlSetTypeConverter ("HandleTypeConverter")]
		public class TestClassTypeConverter3
		{
			// must be static
			public void HandleTypeConverter (object o, XamlSetTypeConverterEventArgs a)
			{
			}
		}
		
		[Test]
		public void SetHandleTypeConverterInvalid3 ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (TestClassTypeConverter3), sctx));
			Assert.Throws<ArgumentException> (() => { var t = i.SetTypeConverterHandler; }, "#1");
		}

		[XamlSetTypeConverter ("HandleTypeConverter")]
		public class TestClassTypeConverter4
		{
			// can be private.
			static void HandleTypeConverter (object o, XamlSetTypeConverterEventArgs a)
			{
			}
		}
		
		[Test]
		public void SetHandleTypeConverter ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (TestClassTypeConverter4), sctx));
			Assert.IsNotNull (i.SetTypeConverterHandler, "#1");
		}

		// AddToCollection

		[Test]
		public void AddToCollectionNoUnderlyingType ()
		{
			var i = new XamlTypeInvoker (new XamlType ("urn:foo", "FooType", null, sctx));
			i.AddToCollection (new List<int> (), 5); // ... passes.
		}

		[Test]
		public void AddToCollectionArrayExtension ()
		{
			var i = XamlLanguage.Array.Invoker;
			var ax = new ArrayExtension ();
			Assert.Throws<NotSupportedException> (() => i.AddToCollection (ax, 5));
		}
		
		[Test]
		public void AddToCollectionArrayInstance ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (int []), sctx));
			var ax = new ArrayExtension ();
			Assert.Throws<NotSupportedException> (() => i.AddToCollection (ax, 5));
		}
		
		[Test]
		public void AddToCollectionList_ObjectTypeMismatch ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (List<int>), sctx));
			try {
				i.AddToCollection (new ArrayExtension (), 5);
				Assert.Fail ("not supported operation.");
			} catch (NotSupportedException) {
			} catch (TargetException) {
				// .NET throws this, but the difference should not really matter.
			}
		}
		
		[Test]
		public void AddToCollectionList_ObjectTypeMismatch2 ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (List<int>), sctx));
			i.AddToCollection (new List<object> (), 5); // it is allowed.
		}
		
		[Test]
		public void AddToCollectionList_ObjectTypeMismatch3 ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (List<object>), sctx));
			i.AddToCollection (new List<int> (), 5); // it is allowed too.
		}
		
		[Test]
		public void AddToCollectionList_ObjectTypeMismatch4 ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (List<Uri>), sctx));
			i.AddToCollection (new List<TimeSpan> (), TimeSpan.Zero); // it is allowed too.
		}
		
		[Test]
		public void AddToCollectionList_NonCollectionType ()
		{
			// so, the source collection type is not checked at all.
			var i = new XamlTypeInvoker (new XamlType (typeof (Uri), sctx));
			i.AddToCollection (new List<TimeSpan> (), TimeSpan.Zero); // it is allowed too.
		}
		
		[Test]
		public void AddToCollectionList ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (List<int>), sctx));
			var l = new List<int> ();
			i.AddToCollection (l, 5);
			i.AddToCollection (l, 3);
			i.AddToCollection (l, -12);
			Assert.AreEqual (3, l.Count, "#1");
			Assert.AreEqual (-12, l [2], "#2");
		}
		
		[Test]
		public void AddToCollectionTypeMismatch ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (List<int>), sctx));
			var l = new List<int> ();
			Assert.Throws<ArgumentException> (() => i.AddToCollection (l, "5"));
		}

		// CreateInstance

		[Test]
		public void CreateInstanceNoUnderlyingType ()
		{
			var i = new XamlTypeInvoker (new XamlType ("urn:foo", "FooType", null, sctx));
			Assert.Throws<NotSupportedException> (() => i.CreateInstance (new object [0])); // unkown type is not supported
		}

		[Test]
		public void CreateInstanceArrayExtension ()
		{
			var i = XamlLanguage.Array.Invoker;
			i.CreateInstance (new object [0]);
		}
		
		[Test]
		public void CreateInstanceArray ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (int []), sctx));
			Assert.Throws<MissingMethodException> (() => i.CreateInstance (new object [0])); // no default constructor.
		}
		
		[Test]
		public void CreateInstanceList_ArgumentMismatch ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (List<int>), sctx));
			Assert.Throws<MissingMethodException> (() => i.CreateInstance (new object [] {"foo"}));
		}
		
		[Test]
		public void CreateInstanceList ()
		{
			var i = new XamlTypeInvoker (new XamlType (typeof (List<int>), sctx));
			i.CreateInstance (new object [0]);
		}
		
		[Test]
		public void GetItems ()
		{
			var i = new XamlType (typeof (List<int>), sctx).Invoker;
			var list = new int [] {5, -3, 0}.ToList ();
			var items = i.GetItems (list);
			var arr = new List<object> ();
			while (items.MoveNext ())
				arr.Add (items.Current);
			Assert.AreEqual (5, arr [0], "#1");
			Assert.AreEqual (0, arr [2], "#2");
		}

		[Test]
		public void GetItems2 ()
		{
			// GetItems() returns IEnumerable<KeyValuePair<,>>
			var i = new XamlType (typeof (Dictionary<int,string>), sctx).Invoker;
			var dic = new Dictionary<int,string> ();
			dic [5] = "foo";
			dic [-3] = "bar";
			dic [0] = "baz";
			var items = i.GetItems (dic);
			var arr = new List<object> ();
			while (items.MoveNext ())
				arr.Add (items.Current);
			Assert.AreEqual (new KeyValuePair<int,string> (5, "foo"), arr [0], "#1");
			Assert.AreEqual (new KeyValuePair<int,string> (0, "baz"), arr [2], "#1");
		}

		[Test]
		public void UnknownInvokerCreateInstance ()
		{
			Assert.Throws<NotSupportedException> (() => XamlTypeInvoker.UnknownInvoker.CreateInstance (new object [0]));
		}

		[Test]
		public void UnknownInvokerGetItems ()
		{
			var items = XamlTypeInvoker.UnknownInvoker.GetItems (new object [] {1});
			Assert.IsNotNull (items, "#1");
			Assert.IsTrue (items.MoveNext (), "#2");
			Assert.AreEqual (1, items.Current, "#3");
			Assert.IsFalse (items.MoveNext (), "#4");
		}

		[Test]
		public void UnknownInvokerAddToCollection ()
		{
			// this does not check Unknown-ness.
			var c = new List<object> ();
			XamlTypeInvoker.UnknownInvoker.AddToCollection (c, 1);
			Assert.AreEqual (1, c.Count, "#1");
		}

		[Test]
		public void UnknownInvokerAddToDictionary ()
		{
			var dic = new Dictionary<object,object> ();
			// this does not check Unknown-ness.
			XamlTypeInvoker.UnknownInvoker.AddToDictionary (dic, 1, 2);
			Assert.AreEqual (1, dic.Count, "#1");
		}

		[Test]
		public void UnknownInvokerGetEnumeratorMethod ()
		{
			try {
				Assert.IsNull (XamlTypeInvoker.UnknownInvoker.GetEnumeratorMethod (), "#1");
			} catch (Exception) {
				// .NET is buggy, returns NRE.
			}
		}

		[Test]
		public void UnknownInvoker ()
		{
			Assert.IsNull (XamlTypeInvoker.UnknownInvoker.SetMarkupExtensionHandler, "#1");
			Assert.IsNull (XamlTypeInvoker.UnknownInvoker.SetTypeConverterHandler, "#2");
			Assert.IsNull (XamlTypeInvoker.UnknownInvoker.GetAddMethod (XamlLanguage.Object), "#3");
		}

#if PORTABLE_XAML
		class ExplicitAddCollection : ICollection<string>
		{
			public readonly List<string> Items = new List<string> ();
			void ICollection<string>.Add (string item) => Items.Add (item);
			public int Count => Items.Count;
			public bool IsReadOnly => false;
			public void Clear () => Items.Clear ();
			public bool Contains (string item) => Items.Contains (item);
			public void CopyTo (string [] array, int arrayIndex) => Items.CopyTo (array, arrayIndex);
			public bool Remove (string item) => Items.Remove (item);
			public IEnumerator<string> GetEnumerator () => Items.GetEnumerator ();
			IEnumerator IEnumerable.GetEnumerator () => Items.GetEnumerator ();
		}

		class ExplicitAddDictionary : IDictionary
		{
			readonly Hashtable items = new Hashtable ();
			void IDictionary.Add (object key, object value) => items.Add (key, value);
			public object this [object key] { get => items [key]; set => items [key] = value; }
			public ICollection Keys => items.Keys;
			public ICollection Values => items.Values;
			public bool IsReadOnly => false;
			public bool IsFixedSize => false;
			public int Count => items.Count;
			public object SyncRoot => items.SyncRoot;
			public bool IsSynchronized => false;
			public void Clear () => items.Clear ();
			public bool Contains (object key) => items.Contains (key);
			public IDictionaryEnumerator GetEnumerator () => items.GetEnumerator ();
			public void Remove (object key) => items.Remove (key);
			public void CopyTo (Array array, int index) => items.CopyTo (array, index);
			IEnumerator IEnumerable.GetEnumerator () => items.GetEnumerator ();
		}

		[TestCase (XamlInvokerOptions.None)]
		[TestCase (XamlInvokerOptions.Compile)]
		[TestCase (XamlInvokerOptions.DeferCompile)]
		public void ExplicitAddShouldBeUsed (XamlInvokerOptions options)
		{
			var context = new XamlSchemaContext (new XamlSchemaContextSettings { InvokerOptions = options });
			var collection = new ExplicitAddCollection ();
			var collectionInvoker = context.GetXamlType (typeof (ExplicitAddCollection)).Invoker;
			collectionInvoker.AddToCollection (collection, "a");
			collectionInvoker.AddToCollection (collection, "b");
			CollectionAssert.AreEqual (new [] { "a", "b" }, collection.Items);

			var dictionary = new ExplicitAddDictionary ();
			var dictionaryInvoker = context.GetXamlType (typeof (ExplicitAddDictionary)).Invoker;
			dictionaryInvoker.AddToDictionary (dictionary, "x", 1);
			dictionaryInvoker.AddToDictionary (dictionary, "y", 2);
			Assert.AreEqual (1, dictionary ["x"]);
			Assert.AreEqual (2, dictionary ["y"]);
		}
#endif
	}
}