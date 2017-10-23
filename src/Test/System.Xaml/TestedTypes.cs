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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using NUnit.Framework;
using System.ComponentModel;
using sc = System.ComponentModel;
#if !PCL136
using System.Collections.Immutable;
#endif

#if NET_4_5 && !PCL
using StringConverter = Portable.Xaml.ComponentModel.StringConverter;
#endif

#if NETSTANDARD
using ISupportInitialize = System.ComponentModel.ISupportInitialize;
using StringConverter = System.ComponentModel.StringConverter;
#if !NETFX_CORE
using DateTimeConverter = System.ComponentModel.DateTimeConverter;
#endif
#elif PCL
using DateTimeConverter = Portable.Xaml.ComponentModel.DateTimeConverter;
using StringConverter = Portable.Xaml.ComponentModel.StringConverter;
#endif

#if PCL
using Portable.Xaml.Markup;
using Portable.Xaml.ComponentModel;
using Portable.Xaml;
using Portable.Xaml.Schema;
#else
using System.Windows.Markup;
using System.Xaml;
using System.Xaml.Schema;
#endif

[assembly: XmlnsDefinition("http://www.domain.com/path", "XamlTest")]
// bug #680385
[assembly: XmlnsDefinition("http://www.domain.com/path", "SecondTest")]
// bug #681045, same xmlns key for different clrns.

[assembly: XmlnsDefinition("http://schemas.example.com/test", "XamarinBug3003")]
// bug #3003
[assembly: XmlnsPrefix("http://schemas.example.com/test", "test")]
// bug #3003

[assembly: XmlnsDefinition("urn:mono-test", "MonoTests.Portable.Xaml.NamespaceTest")]
[assembly: XmlnsDefinition("urn:mono-test2", "MonoTests.Portable.Xaml.NamespaceTest2")]

// comment out the following to get mono's System.Xaml to go further in the tests
[assembly: XmlnsDefinition("urn:bar", "MonoTests.Portable.Xaml.NamespaceTest")]

[assembly: XmlnsCompatibleWith("urn:foo", "urn:bar")]
[assembly: XmlnsCompatibleWith("urn:foo2", "urn:bar2")]

namespace MonoTests.Portable.Xaml.NamespaceTest
{
	public class NamespaceTestClass
	{
		public string Foo { get; set; }

		public string Bar { get; set; }
	}

	public abstract class AbstractObject
	{
		public abstract string Foo { get; set; }
	}

	public class DerivedObject : AbstractObject
	{
		public override string Foo { get; set; }
	}

	[ContentProperty("Contents")]
	public class CustomGenericType<T>
	{
		public List<T> Contents { get; } = new List<T>();
	}
}

namespace MonoTests.Portable.Xaml.NamespaceTest2
{
	public class TestClassWithDifferentBaseNamespace : MonoTests.Portable.Xaml.TestClass5WithName
	{
		public string SomeOtherProperty { get; set; }
	}
}

namespace MonoTests.Portable.Xaml
{

	public class ArgumentAttributed
	{
		public ArgumentAttributed(string s1, string s2)
		{
			Arg1 = s1;
			Arg2 = s2;
		}

		[ConstructorArgument("s1")]
		public string Arg1 { get; private set; }

		[ConstructorArgument("s2")]
		public string Arg2 { get; private set; }
	}

	[ContentProperty("Contents")]
	public class CustomGenericType<T>
		where T : struct
	{
		public List<T> Contents { get; } = new List<T>();
	}

	public class ArgumentNonAttributed
	{
		public ArgumentNonAttributed(string s1, string s2)
		{
			Arg1 = s1;
			Arg2 = s2;
		}

		public string Arg1 { get; private set; }

		public string Arg2 { get; private set; }
	}

	public class ArgumentMultipleTypes
	{
		public int IntArg { get; private set; }

		public string StringArg { get; private set; }

		public ArgumentMultipleTypes(int arg1)
		{
			IntArg = arg1;
		}

		public ArgumentMultipleTypes(string arg1)
		{
			StringArg = arg1;
		}

	}

	public class ArgumentWithIntConstructor
	{
		public int IntArg { get; private set; }

		public ArgumentWithIntConstructor(int arg1)
		{
			IntArg = arg1;
		}
	}

	public class ComplexPositionalParameterWrapper
	{
		public ComplexPositionalParameterClass Param { get; set; }
	}

	public class ComplexPositionalParameterWrapper2
	{
		public string Param { get; set; }
	}

	[TypeConverter(typeof(ComplexPositionalParameterClassConverter))]
	public class ComplexPositionalParameterClass : MarkupExtension
	{
		public ComplexPositionalParameterClass(ComplexPositionalParameterValue value)
		{
			this.Value = value;
		}

		[ConstructorArgument("value")]
		public ComplexPositionalParameterValue Value { get; private set; }

		public override object ProvideValue(IServiceProvider sp)
		{
			return Value.Foo;
		}
	}

	public class ComplexPositionalParameterClassConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object valueToConvert)
		{
			return new ComplexPositionalParameterClass(new ComplexPositionalParameterValue() { Foo = (string)valueToConvert });
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			// conversion to string is not supported.
			return destinationType == typeof(ComplexPositionalParameterClass);
		}
	}

	public class ComplexPositionalParameterValue
	{
		public string Foo { get; set; }
	}

	//[MarkupExtensionReturnType (typeof (Array))]
	//[ContentProperty ("Items")]  ... so, these attributes do not affect XamlObjectReader.
	public class MyArrayExtension : MarkupExtension
	{
		public MyArrayExtension()
		{
			items = new ArrayList();
		}

		public MyArrayExtension(Array array)
		{
			items = new ArrayList(array);
			this.Type = array.GetType().GetElementType();
		}

		public MyArrayExtension(Type type)
			: this()
		{
			this.Type = type;
		}

		IList items;

		public IList Items
		{
			get { return items; }
			private set { items = value; }
		}

		[ConstructorArgument("type")]
		public Type Type { get; set; }

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			if (Type == null)
				throw new InvalidOperationException("Type property must be set before calling ProvideValue method");

			Array a = Array.CreateInstance(Type, Items.Count);
			Items.CopyTo(a, 0);
			return a;
		}
	}

	// The trailing "A" gives significant difference in XML output!
	public class MyArrayExtensionA : MarkupExtension
	{
		public MyArrayExtensionA()
		{
			items = new ArrayList();
		}

		public MyArrayExtensionA(Array array)
		{
			items = new ArrayList(array);
			this.Type = array.GetType().GetElementType();
		}

		public MyArrayExtensionA(Type type)
			: this()
		{
			this.Type = type;
		}

		IList items;

		public IList Items
		{
			get { return items; }
			private set { items = value; }
		}

		[ConstructorArgument("type")]
		public Type Type { get; set; }

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			if (Type == null)
				throw new InvalidOperationException("Type property must be set before calling ProvideValue method");

			Array a = Array.CreateInstance(Type, Items.Count);
			Items.CopyTo(a, 0);
			return a;
		}
	}

	class TestClass1
	{
	}

	public class TestClass3
	{
		public TestClass3 Nested { get; set; }
	}

	public class TestClass4
	{
		public string Foo { get; set; }

		public string Bar { get; set; }
	}

	public class TestClass5
	{
		public static string Foo { get; set; }

		public string Bar { get; set; }

		public string Baz { internal get; set; }

		public string WriteOnly
		{
			set { Baz = value; }
		}

		public string ReadOnly
		{
			get { return Foo; }
		}
	}

	public class TestClass6
	{
		public DateTime TheDateAndTime { get; set; }
	}

	[RuntimeNameProperty("TheName")]
	public class TestClass5WithName : TestClass5
	{
		[sc.DefaultValue(null)]
		public string TheName { get; set; }

		[sc.DefaultValue(null)]
		public TestClass5WithName Other { get; set; }
	}

	public class TestClassBase
	{

	}

	class TestClassInternal : TestClassBase
	{
		public string Foo { get; set; }
	}

	public class TestClassPropertyInternal
	{
		public string Foo { get; set; }

		public TestClassBase Bar { get; set; }
	}

	public class TestClassWithDefaultValuesString
	{
		public string NoDefaultValue { get; set; }

		[sc.DefaultValue("")]
		public string NullDefaultValue { get; set; } = "";

		[sc.DefaultValue("Some Default")]
		public string SpecificDefaultValue { get; set; } = "Some Default";
	}

	public class TestClassWithDefaultValuesInt
	{
		public int NoDefaultValue { get; set; }

		[sc.DefaultValue(0)]
		public int ZeroDefaultValue { get; set; }

		[sc.DefaultValue(100)]
		public int SpecificDefaultValue { get; set; } = 100;
	}

	public class TestClassWithDefaultValuesNullableInt
	{
		public int? NoDefaultValue { get; set; }

		[sc.DefaultValue(null)]
		public int? NullDefaultValue { get; set; }

		[sc.DefaultValue(0)]
		public int? ZeroDefaultValue { get; set; } = 0;

		[sc.DefaultValue(100)]
		public int? SpecificDefaultValue { get; set; } = 100;
	}

	public class MyExtension : MarkupExtension
	{
		public MyExtension()
		{
		}

		public MyExtension(Type arg1, string arg2, string arg3)
		{
			Foo = arg1;
			Bar = arg2;
			Baz = arg3;
		}

		[ConstructorArgument("arg1")]
		public Type Foo { get; set; }

		[ConstructorArgument("arg2")]
		public string Bar { get; set; }

		[ConstructorArgument("arg3")]
		public string Baz { get; set; }

		public override object ProvideValue(IServiceProvider provider)
		{
			return "provided_value";
		}
	}

	[TypeConverter(typeof(StringConverter))] // This attribute is the markable difference between MyExtension and this type.
	public class MyExtension2 : MarkupExtension
	{
		public MyExtension2()
		{
		}

		public MyExtension2(Type arg1, string arg2)
		{
			Foo = arg1;
			Bar = arg2;
		}

		[ConstructorArgument("arg1")]
		public Type Foo { get; set; }

		[ConstructorArgument("arg2")]
		public string Bar { get; set; }

		public override object ProvideValue(IServiceProvider provider)
		{
			return "provided_value";
		}
	}

	[TypeConverter(typeof(StringConverter))] // same as MyExtension2 except that it is *not* MarkupExtension.
	public class MyExtension3
	{
		public MyExtension3()
		{
		}

		// cf. According to [MS-XAML-2009] 3.2.1.11, constructors are invalid unless the type is derived from TypeExtension. So, it is likely *ignored*.
		public MyExtension3(Type arg1, string arg2)
		{
			Foo = arg1;
			Bar = arg2;
		}

		[ConstructorArgument("arg1")]
		public Type Foo { get; set; }

		[ConstructorArgument("arg2")]
		public string Bar { get; set; }
	}

	[TypeConverter(typeof(DateTimeConverter))] // same as MyExtension3 except for the type converter.
	public class MyExtension4
	{
		public MyExtension4()
		{
		}

		// cf. According to [MS-XAML-2009] 3.2.1.11, constructors are invalid unless the type is derived from TypeExtension. So, it is likely *ignored*.
		public MyExtension4(Type arg1, string arg2)
		{
			Foo = arg1;
			Bar = arg2;
		}

		[ConstructorArgument("arg1")]
		public Type Foo { get; set; }

		[ConstructorArgument("arg2")]
		public string Bar { get; set; }
	}

	// no type converter, and there are only simple-type arguments == _PositionalParameters is applicable.
	public class MyExtension5 : MarkupExtension
	{
		public MyExtension5(string arg1, string arg2)
		{
			Foo = arg1;
			Bar = arg2;
		}

		[ConstructorArgument("arg1")]
		public string Foo { get; set; }

		[ConstructorArgument("arg2")]
		public string Bar { get; set; }

		public override object ProvideValue(IServiceProvider sp)
		{
			return Foo;
		}
	}

	// Almost the same as MyExtension5, BUT there is default constructor which XamlObjectReader prefers.
	public class MyExtension6 : MarkupExtension
	{
		public MyExtension6()
		{
		}

		public MyExtension6(string arg1)
		{
			Foo = arg1;
		}

		[ConstructorArgument("arg1")]
		public string Foo { get; set; }

		public override object ProvideValue(IServiceProvider sp)
		{
			return Foo;
		}
	}

	/// <summary>
	/// No positional constructor arguments
	/// </summary>
	public class MyExtension7 : MarkupExtension
	{
		public MyExtension7()
		{
		}

		public string Foo { get; set; }

		public string Bar { get; set; }

		public override object ProvideValue(IServiceProvider sp)
		{
			return Foo;
		}
	}

	public class PositionalParametersClass1 : MarkupExtension
	{
		public PositionalParametersClass1(string foo)
			: this(foo, -1)
		{
		}

		public PositionalParametersClass1(string foo, int bar)
		{
			Foo = foo;
			Bar = bar;
		}

		[ConstructorArgument("foo")]
		public string Foo { get; set; }

		[ConstructorArgument("bar")]
		public int Bar { get; set; }

		public override object ProvideValue(IServiceProvider sp)
		{
			var target = sp.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
			if (target != null)
			{
				var propInfo = target.TargetProperty as PropertyInfo;
				if (propInfo != null && propInfo.PropertyType == typeof(PositionalParametersClass1))
					return this;
			}
			return Foo;
		}
	}

	public class ValueWrapper
	{
		public string StringValue { get; set; }

		public ValueWrapper()
		{
		}
	}

	public class PositionalParametersWrapper
	{
		public PositionalParametersClass1 Body { get; set; }

		public PositionalParametersWrapper()
		{
		}

		public PositionalParametersWrapper(string foo, int bar)
		{
			Body = new PositionalParametersClass1(foo, bar);
		}
	}

	public class ListWrapper
	{
		public ListWrapper()
		{
			Items = new List<int>();
		}

		public ListWrapper(List<int> items)
		{
			Items = items;
		}

		public List<int> Items { get; private set; }
	}

	public class ListWrapper2
	{
		public ListWrapper2()
		{
			Items = new List<int>();
		}

		public ListWrapper2(List<int> items)
		{
			Items = items;
		}

		public List<int> Items { get; set; }
		// it is settable, which makes difference.
	}

	[ContentProperty("Content")]
	public class ContentIncludedClass
	{
		public string Content { get; set; }
	}

	public class StaticClass1
	{
		static StaticClass1()
		{
			FooBar = "test";
		}

		public static string FooBar { get; set; }

		public enum MyEnum
		{
			EnumValue1,
			EnumValue2
		}
	}

	public class StaticExtensionWrapper
	{
		public StaticExtension Param { get; set; }

		public static string Foo = "foo";
	}

	public class StaticExtensionWrapper2
	{
		public string Param { get; set; }

		public static string Foo = "foo";
	}

	public class TypeExtensionWrapper
	{
		public TypeExtension Param { get; set; }
	}

	public class TypeExtensionWrapper2
	{
		public Type Param { get; set; }
	}

	public class XDataWrapper
	{
		public XData Markup { get; set; }
	}

	// FIXME: test it with XamlXmlReader (needs to create xml first)
	public class EventContainer
	{
#pragma warning disable 67
		public event Action Run;
#pragma warning restore 67
	}

	public class NamedItem
	{
		public NamedItem()
		{
			References = new List<NamedItem>();
		}

		public NamedItem(string name)
			: this()
		{
			ItemName = name;
		}

		public string ItemName { get; set; }

		public IList<NamedItem> References { get; private set; }
	}

	[RuntimeNameProperty("ItemName")]
	public class NamedItem2
	{
		public NamedItem2()
		{
			References = new List<NamedItem2>();
		}

		public NamedItem2(string name)
			: this()
		{
			ItemName = name;
		}

		public string ItemName { get; set; }

		public IList<NamedItem2> References { get; private set; }
	}

	public class NamedItem3 : NamedItem2
	{
		public NamedItem2 Other { get; set; }

#if !PCL136
		public ImmutableArray<NamedItem3> ImmutableReferences { get; set; }
#endif
	}

	[TypeConverter(typeof(TestValueConverter))]
	public class TestValueSerialized
	{
		public TestValueSerialized()
		{
		}

		public string Foo { get; set; }
	}

	public class TestValueConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			//Console.Error.WriteLine ("### {0}:{1}", sourceType, context);
			ValueSerializerContextTest.RunCanConvertFromTest(context, sourceType);
			return true;
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			//Console.Error.WriteLine ("##### {0}:{1}", source, context);
			ValueSerializerContextTest.RunConvertFromTest(context, culture, value);
			//var sp = context as IServiceProvider;
			// ValueSerializerContextTest.Context = (IValueSerializerContext) context; -> causes InvalidCastException
			if ((value as string) == "v")
				return new TestValueSerialized();
			throw new Exception("huh");
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			//Console.Error.WriteLine ("$$$ {0}:{1}", destinationType, context);
			if (destinationType != typeof(MarkupExtension))
			{
				ValueSerializerContextTest.RunCanConvertToTest(context, destinationType);
				return true;
			}
			return false;
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			ValueSerializerContextTest.RunConvertToTest(context, culture, value, destinationType);

			return base.ConvertTo(context, culture, value, destinationType);
		}
	}

	[ContentProperty("Value")]
	public class XmlSerializableWrapper
	{
		public XmlSerializableWrapper() // mandatory
			: this(new XmlSerializable())
		{
		}

		public XmlSerializableWrapper(XmlSerializable val)
		{
			this.val = val;
		}

		XmlSerializable val;

		public XmlSerializable Value
		{
			get { return val; }
			// To make it become XData, it cannot have a setter.
		}
	}

	public class XmlSerializable : IXmlSerializable
	{
		public XmlSerializable()
		{
		}

		public XmlSerializable(string raw)
		{
			this.raw = raw;
		}

		string raw;

		public string GetRaw()
		{
			return raw;
		}

		public void ReadXml(XmlReader reader)
		{
			reader.MoveToContent();
			raw = reader.ReadOuterXml();
		}

		public void WriteXml(XmlWriter writer)
		{
			if (raw != null)
			{
				var xr = XmlReader.Create(new StringReader(raw));
				while (!xr.EOF)
					writer.WriteNode(xr, false);
			}
		}

		public XmlSchema GetSchema()
		{
			return null;
		}
	}

	public class Attachable
	{
		public static readonly AttachableMemberIdentifier FooIdentifier = new AttachableMemberIdentifier(typeof(Attachable), "Foo");
		public static readonly AttachableMemberIdentifier ProtectedIdentifier = new AttachableMemberIdentifier(typeof(Attachable), "Protected");

		public static string GetFoo(object target)
		{
			string v;
			return AttachablePropertyServices.TryGetProperty(target, FooIdentifier, out v) ? v : null;
		}

		public static void SetFoo(object target, string value)
		{
			AttachablePropertyServices.SetProperty(target, FooIdentifier, value);
		}

		public static string GetBar(object target, object signatureMismatch)
		{
			return null;
		}

		public static void SetBar(object signatureMismatch)
		{
		}

		public static void GetBaz(object noReturnType)
		{
		}

		public static string SetBaz(object target, object extraReturnType)
		{
			return null;
		}

		protected static string GetProtected(object target)
		{
			string v;
			return AttachablePropertyServices.TryGetProperty(target, ProtectedIdentifier, out v) ? v : null;
		}

		protected static void SetProtected(object target, string value)
		{
			AttachablePropertyServices.SetProperty(target, ProtectedIdentifier, value);
		}

		static Dictionary<object, List<EventHandler>> handlers = new Dictionary<object, List<EventHandler>>();

		public static void AddXHandler(object target, EventHandler handler)
		{
			List<EventHandler> l;
			if (!handlers.TryGetValue(target, out l))
			{
				l = new List<EventHandler>();
				handlers[target] = l;
			}
			l.Add(handler);
		}

		public static void RemoveXHandler(object target, EventHandler handler)
		{
			handlers[target].Remove(handler);
		}
	}

	public class AttachedPropertyStore : IAttachedPropertyStore
	{
		public AttachedPropertyStore()
		{
		}

		Dictionary<AttachableMemberIdentifier, object> props = new Dictionary<AttachableMemberIdentifier, object>();

		public int PropertyCount
		{
			get { return props.Count; }
		}

		public void CopyPropertiesTo(KeyValuePair<AttachableMemberIdentifier, object>[] array, int index)
		{
			((ICollection<KeyValuePair<AttachableMemberIdentifier, object>>)props).CopyTo(array, index);
		}

		public bool RemoveProperty(AttachableMemberIdentifier attachableMemberIdentifier)
		{
			return props.Remove(attachableMemberIdentifier);
		}

		public void SetProperty(AttachableMemberIdentifier attachableMemberIdentifier, object value)
		{
			props[attachableMemberIdentifier] = value;
		}

		public bool TryGetProperty(AttachableMemberIdentifier attachableMemberIdentifier, out object value)
		{
			return props.TryGetValue(attachableMemberIdentifier, out value);
		}
	}

	public class AttachedWrapper : AttachedPropertyStore
	{
		public AttachedWrapper()
		{
			Value = new Attached();
		}

		public Attached Value { get; set; }
	}

	public class AttachedWrapper2
	{
		public static readonly AttachableMemberIdentifier FooIdentifier = new AttachableMemberIdentifier(typeof(AttachedWrapper2), "Foo");

		static AttachedPropertyStore store = new AttachedPropertyStore();

		public static string GetFoo(object target)
		{
			object v;
			return store.TryGetProperty(FooIdentifier, out v) ? (string)v : null;
		}

		public static void SetFoo(object target, string value)
		{
			store.SetProperty(FooIdentifier, value);
		}

		public static int PropertyCount
		{
			get { return store.PropertyCount; }
		}

		public AttachedWrapper2()
		{
			Value = new Attached();
		}

		public Attached Value { get; set; }
	}

	public class Attached : Attachable
	{
	}

	public class Attached2
	{
		internal String Property { get; set; }
	}

	public class AttachedWrapper3
	{
		public static void SetProperty(Attached2 a, string value)
		{
			a.Property = value;
		}
	}

	public class Attached4
	{
		internal List<TestClass4> Property { get; set;  } = new List<TestClass4>();
	}

	public class AttachedWrapper4
	{
		public static List<TestClass4> GetSomeCollection(Attached4 attached)
		{
			return attached.Property;
		}
	}
	public class AttachedWrapper5
	{
		public static List<TestClass4> GetSomeCollection(Attached4 attached)
		{
			return attached.Property;
		}
		public static void SetSomeCollection(Attached4 attached, List<TestClass4> value)
		{
			attached.Property = value;
		}
	}

	public class CustomEventArgs : EventArgs
	{
	}

	public class EventStore
	{
		public bool Method1Invoked;

		public event EventHandler<EventArgs> Event1;
		public event Func<object> Event2;

		public event EventHandler<CustomEventArgs> Event3;

		public object Examine()
		{
			if (Event1 != null)
				Event1(this, EventArgs.Empty);
			if (Event2 != null)
				return Event2();
			if (Event3 != null)
				Event3(this, new CustomEventArgs());
			return null;
		}

		public void Method1()
		{
			throw new Exception();
		}

		public void Method1(object o, EventArgs e)
		{
			Method1Invoked = true;
		}

		public object Method2()
		{
			return "foo";
		}
	}

	public class EventStore2<TEventArgs> where TEventArgs : EventArgs
	{
		public bool Method1Invoked;

		public event EventHandler<TEventArgs> Event1;
		public event Func<object> Event2;

		public object Examine()
		{
			if (Event1 != null)
				Event1(this, default(TEventArgs));
			if (Event2 != null)
				return Event2();
			else
				return null;
		}

		public void Method1()
		{
			throw new Exception();
		}

		public void Method1(object o, EventArgs e)
		{
			throw new Exception();
		}

		public void Method1(object o, TEventArgs e)
		{
			Method1Invoked = true;
		}

		public object Method2()
		{
			return "foo";
		}
	}

	public class AbstractContainer
	{
		public AbstractObject Value1 { get; set; }

		public AbstractObject Value2 { get; set; }
	}

	public abstract class AbstractObject
	{
		public abstract string Foo { get; set; }
	}

	public class DerivedObject : AbstractObject
	{
		public override string Foo { get; set; }
	}

	public class ReadOnlyPropertyContainer
	{
		string foo;

		public string Foo
		{
			get { return foo; }
			set { foo = Bar = value; }
		}

		public string Bar { get; private set; }
	}

	public class EnumContainer
	{
		public EnumValueType EnumProperty { get; set; }
	}

	public enum EnumValueType
	{
		One,
		Two,
		Three,
		Four
	}

	[ContentProperty("ListOfItems")]
	public class CollectionContentProperty
	{
		public IList<SimpleClass> ListOfItems { get; set; }

		public CollectionContentProperty()
		{
			this.ListOfItems = new List<SimpleClass>();
		}
	}

	[ContentProperty("ListOfItems")]
	public class CollectionContentPropertyX
	{
		public IList ListOfItems { get; set; }

		public CollectionContentPropertyX()
		{
			this.ListOfItems = new List<IEnumerable>();
		}
	}

	public class SimpleClass
	{
	}

	public class NullableContainer
	{
		public int? TestProp { get; set; }
	}

	public class NullableContainer2
	{
		public DateTime? NullableDate { get; set; }
	}

	class TestStructConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			var text = value as string;
			if (text != null)
				return new TestStruct { Text = text };
			return base.ConvertFrom(context, culture, value);
		}
	}

	[TypeConverter(typeof(TestStructConverter))]
	public struct TestStruct
	{
		public string Text;
	}

	public class NullableWithTypeConverterContainer
	{
		public TestStruct? TestProp { get; set; }
	}

	public class DirectListContainer // for such xml that directly contains items in <*.Items> element.
	{
		public IList<DirectListContent> Items { get; set; }

		public DirectListContainer()
		{
			this.Items = new List<DirectListContent>();
		}
	}

	public class DirectListContent
	{
		public string Value { get; set; }
	}

	public class DirectDictionaryContainer // for such xml that directly contains items in <*.Items> element.
	{
		public IDictionary<EnumValueType, int> Items { get; set; }

		public DirectDictionaryContainer()
		{
			this.Items = new Dictionary<EnumValueType, int>();
		}
	}

	public class CollectionItemConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || sourceType == typeof(OtherItem) || base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			var text = value as string;
			if (text != null)
				return new CollectionItem { Name = text };
			var otherItem = value as OtherItem;
			if (otherItem != null)
			{
				return otherItem.CollectionItem;
			}
			return base.ConvertFrom(context, culture, value);
		}
	}

	public class OtherItem
	{
		public CollectionItem CollectionItem { get { return new CollectionItem { Name = "FromOther" }; } }
	}

	[TypeConverter(typeof(CollectionItemConverter))]
	public class CollectionItem
	{
		public string Name { get; set; }
	}

	public class CollectionItemCollectionAddOverride : Collection<CollectionItem>, IList
	{
		int IList.Add(object item)
		{
			var text = item as string;
			if (text != null)
				Add(new CollectionItem { Name = text });
			var other = item as OtherItem;
			if (other != null)
				Add(other.CollectionItem);
			else
				Add((CollectionItem)item);
			return Count - 1;
		}
	}

	public class CollectionItemCollection : Collection<CollectionItem>
	{
	}

	[ContentProperty("Items")]
	public class CollectionParentCustomAddOverride
	{
		public CollectionItemCollectionAddOverride Items { get; } = new CollectionItemCollectionAddOverride();
	}

	[ContentProperty("Items")]
	public class CollectionParentGenericList
	{
		public List<CollectionItem> Items { get; } = new List<CollectionItem>();
	}

	[ContentProperty("Items")]
	public class CollectionParentCustomNoOverride
	{
		public CollectionItemCollection Items { get; } = new CollectionItemCollection();
	}

	[ContentProperty("Items")]
	public class CollectionParentItem
	{
		public bool OtherItem { get; set; }

		public CollectionItem CollectionItem { get; set; }

		public CollectionItemCollectionAddOverride Items { get; } = new CollectionItemCollectionAddOverride();
	}

	public class TestDeferredLoader : XamlDeferringLoader
	{
		public override object Load(XamlReader xamlReader, IServiceProvider serviceProvider)
		{
			return new DeferredLoadingChild(xamlReader);
		}

		public override XamlReader Save(object value, IServiceProvider serviceProvider)
		{
			return ((DeferredLoadingChild)value).List.GetReader();
		}
	}

	public class TestDeferredLoader2 : XamlDeferringLoader
	{
		public override object Load(XamlReader xamlReader, IServiceProvider serviceProvider)
		{
			return new DeferredLoadingChild2(xamlReader);
		}

		public override XamlReader Save(object value, IServiceProvider serviceProvider)
		{
			throw new NotImplementedException();
		}
	}


	public class DeferredLoadingChild
	{
		public XamlNodeList List { get; set; }
		public string Foo { get; set; }

		public DeferredLoadingChild()
		{
		}

		public DeferredLoadingChild(XamlReader reader)
		{
			List = new XamlNodeList(reader.SchemaContext);
			XamlServices.Transform(reader, List.Writer);
		}
	}

	[ContentProperty("Child")]
	public class DeferredLoadingContainerMember
	{
		[XamlDeferLoad(typeof(TestDeferredLoader), typeof(DeferredLoadingChild))]
		public DeferredLoadingChild Child { get; set; }
	}

	[ContentProperty("Item")]
	[XamlDeferLoad(typeof(TestDeferredLoader2), typeof(DeferredLoadingChild2))]
	public class DeferredLoadingChild2
	{
		public XamlNodeList List { get; set; }
		public string Foo { get; set; }

		public CollectionParentGenericList Item { get; set; }

		public DeferredLoadingChild2()
		{
		}

		public DeferredLoadingChild2(XamlReader reader)
		{
			List = new XamlNodeList(reader.SchemaContext);
			XamlServices.Transform(reader, List.Writer);
		}
	}

	[ContentProperty("Child")]
	public class DeferredLoadingContainerType
	{
		public DeferredLoadingChild2 Child { get; set; }
	}

	[ContentProperty("Child")]
	public class DeferredLoadingWithInvalidType
	{
		[XamlDeferLoad("Some.Invalid.Type", "Some.Invalid.Type")]
		public DeferredLoadingChild Child { get; set; }
	}

	[ContentProperty("Child")]
	public class DeferredLoadingContainerMemberStringType
	{
		[XamlDeferLoad("MonoTests.Portable.Xaml.TestDeferredLoader," + Compat.TestAssemblyName, "MonoTests.Portable.Xaml.DeferredLoadingChild," + Compat.TestAssemblyName)]
		public DeferredLoadingChild Child { get; set; }
	}

	public class ImmutableTypeSingleArgument
	{
		[ConstructorArgument("name")]
		public string Name { get; }

		public ImmutableTypeSingleArgument(string name)
		{
			Name = name;
		}
	}
	public class ImmutableTypeMultipleArguments
	{
		[ConstructorArgument("name")]
		public string Name { get; }

		[ConstructorArgument("flag")]
		public bool Flag { get; }

		[ConstructorArgument("num")]
		public int Num { get; }

		public ImmutableTypeMultipleArguments(string name, bool flag, int num)
		{
			Name = name;
			Flag = flag;
			Num = num;
		}
	}

	public class ImmutableTypeMultipleConstructors
	{
		[ConstructorArgument("name")]
		public string Name { get; }

		[ConstructorArgument("flag")]
		public bool Flag { get; }

		[ConstructorArgument("num")]
		public int Num { get; }

		public ImmutableTypeMultipleConstructors(string name)
		{
			Name = name;
		}

		public ImmutableTypeMultipleConstructors(string name, bool flag, int num)
		{
			Name = name;
			Flag = flag;
			Num = num;
		}
	}

	public class ImmutableTypeOptionalParameters
	{
		[ConstructorArgument("name")]
		public string Name { get; }

		[ConstructorArgument("flag")]
		public bool Flag { get; }

		[ConstructorArgument("num")]
		public int Num { get; }

		public ImmutableTypeOptionalParameters(string name, bool flag = true, int num = 100)
		{
			Name = name;
			Flag = flag;
			Num = num;
		}
	}

	[ContentProperty("Collection")]
	public class ImmutableTypeWithCollectionProperty
	{
		[ConstructorArgument("name")]
		public string Name { get; }

		[ConstructorArgument("flag")]
		public bool Flag { get; }

		[ConstructorArgument("num")]
		public int Num { get; }

		public ImmutableTypeWithCollectionProperty(string name, bool flag = true, int num = 100)
		{
			Name = name;
			Flag = flag;
			Num = num;
		}

		public IList<TestClass4> Collection { get; } = new List<TestClass4>();
	}

	public class ImmutableTypeWithWritableProperty
	{
		[ConstructorArgument("name")]
		public string Name { get; }

		[ConstructorArgument("flag")]
		public bool Flag { get; }

		[ConstructorArgument("num")]
		public int Num { get; }

		public ImmutableTypeWithWritableProperty(string name, bool flag = true, int num = 100)
		{
			Name = name;
			Flag = flag;
			Num = num;
		}

		public string Foo { get; set; }
	}

	public class ImmutableCollectionItem : IComparable
	{
		public string Foo { get; set; }

		public int CompareTo(object obj)
		{
			return string.Compare(Foo, ((ImmutableCollectionItem)obj)?.Foo);
		}
	}

#if !PCL136

	public class ImmutableCollectionContainer
	{
		public ImmutableArray<ImmutableCollectionItem> ImmutableArray { get; set; }
		public ImmutableList<ImmutableCollectionItem> ImmutableList { get; set; }
		public ImmutableHashSet<ImmutableCollectionItem> ImmutableHashSet { get; set; }
		public ImmutableQueue<ImmutableCollectionItem> ImmutableQueue { get; set; }
		public ImmutableStack<ImmutableCollectionItem> ImmutableStack { get; set; }
		public ImmutableSortedSet<ImmutableCollectionItem> ImmutableSortedSet { get; set; }
		public ImmutableDictionary<string, ImmutableCollectionItem> ImmutableDictionary { get; set; }
		public ImmutableSortedDictionary<string, ImmutableCollectionItem> ImmutableSortedDictionary { get; set; }
	}

#endif

	public class NumericValues
	{
		public double DoubleValue { get; set; }

		public decimal DecimalValue { get; set; }

		public float FloatValue { get; set; }

		public byte ByteValue { get; set; }

		public int IntValue { get; set; }

		public long LongValue { get; set; }
	}

	public class TestObjectWithShouldSerialize
	{
		public string Text { get; set; }

		internal int ShouldSerializeCalled { get; set; }

		bool ShouldSerializeText()
		{
			ShouldSerializeCalled++;
			return !string.IsNullOrEmpty(Text) && Text != "bar";
		}
	}
}

namespace XamlTest
{
	public class Configurations : List<Configuration>
	{
		private Configuration active;
		//private bool isFrozen;

		public Configuration Active
		{
			get { return this.active; }
			set
			{
				/*
				if (this.isFrozen)
				{
					throw new InvalidOperationException("The 'Active' configuration can only be changed via modifying the source file (" + this.Source + ").");
				}*/

				this.active = value;
			}
		}

		public string Source { get; private set; }
	}

	public class Configuration
	{
		public string Version { get; set; }

		public string Path { get; set; }
	}
}

// see bug #681480
namespace SecondTest
{
	public class TypeOtherAssembly
	{
		[TypeConverter(typeof(NullableUintListConverter))]
		public List<uint?> Values { get; set; }

		public TypeOtherAssembly()
		{
			this.Values = new List<uint?>();
		}
	}

	public class NullableUintListConverter : CustomTypeConverterBase
	{
		public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
		{
			string configValue = value as string;
			if (string.IsNullOrWhiteSpace(configValue))
				return null;

			string delimiterStr = ", ";
			char[] delimiters = delimiterStr.ToCharArray();
			string[] tokens = configValue.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

			List<uint?> parsedList = new List<uint?>(tokens.Length);
			foreach (string token in tokens)
				parsedList.Add(uint.Parse(token));

			return parsedList;
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			var v = (List<uint?>)value;
			return String.Join(", ", (from i in v
				                          select i.ToString()).ToArray());
		}
	}

	public class CustomTypeConverterBase : TypeConverter
	{
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			return base.ConvertFrom(context, culture, value);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof(string))
			{
				return true;
			}
			return base.CanConvertFrom(context, sourceType);
		}
	}

#region bug #681202

	[MarkupExtensionReturnType(typeof(object))]
	public class ResourceExtension : MarkupExtension
	{
		[ConstructorArgument("key")]
		public object Key { get; set; }

		public ResourceExtension(object key)
		{
			this.Key = key;
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			IXamlSchemaContextProvider service = serviceProvider.GetService(typeof(IXamlSchemaContextProvider)) as IXamlSchemaContextProvider;
			IAmbientProvider provider = serviceProvider.GetService(typeof(IAmbientProvider)) as IAmbientProvider;
			Debug.Assert(provider != null, "The provider should not be null!");

			XamlSchemaContext schemaContext = service.SchemaContext;
			var types = new XamlType[] { schemaContext.GetXamlType(typeof(ResourcesDict)) };

			// Getting based on types alone should return the value, not the AmbientPropertyValue
			var objectValues = provider.GetAllAmbientValues(types).ToList();
			Assert.AreEqual (1, objectValues.Count, "#1");

			// ResourceDict is marked as Ambient, so the instance current being deserialized should be in this list.
			var ambientValues = provider.GetAllAmbientValues(null, false, types).ToList();
			Assert.AreEqual(1, ambientValues.Count, "#2");
			CollectionAssert.AreEqual (objectValues, ambientValues.Select (r => r.Value), "#3");
			foreach (var dict in ambientValues.Select(r => r.Value).OfType<ResourcesDict>())
			{
				if (dict.ContainsKey(this.Key))
					return dict[this.Key];
			}
			return null;
		}
	}

	[MarkupExtensionReturnType(typeof(object))]
	public class Resource2Extension : MarkupExtension
	{
		[ConstructorArgument("key")]
		public object Key { get; set; }

		public Resource2Extension(object key)
		{
			this.Key = key;
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			IXamlSchemaContextProvider service = serviceProvider.GetService(typeof(IXamlSchemaContextProvider)) as IXamlSchemaContextProvider;
			IAmbientProvider provider = serviceProvider.GetService(typeof(IAmbientProvider)) as IAmbientProvider;
			Debug.Assert(provider != null, "The provider should not be null!");

			XamlSchemaContext schemaContext = service.SchemaContext;

			// odd, specifying a type that is not ambient does not throw...
			provider.GetAllAmbientValues(null, false, new[]
				{
					schemaContext.GetXamlType(typeof(ResourcesDict2))
				});

			Assert.Throws<ArgumentException>(() =>
				{
					// getting ambient values for a property that is not flagged as ambient throws
					provider.GetAllAmbientValues(null, false, null, new[]
						{
							schemaContext.GetXamlType(typeof(ResourceContainer)).GetMember("Resources3")
						});
				}, "#1");

			var types = new XamlType[] { schemaContext.GetXamlType(typeof(ResourcesDict)) };
			var properties = new XamlMember[]
			{
				schemaContext.GetXamlType(typeof(ResourceContainer)).GetMember("Resources"),
				schemaContext.GetXamlType(typeof(ResourceContainer)).GetMember("Resources2")
			};

			var values = provider.GetAllAmbientValues (types).ToList ();
			int count = 0;
			if (Equals (Key, "TestDictItem")) {
				// inside ambient value, should be returned as well
				Assert.AreEqual (1, values.Count, "#2");
				Assert.IsInstanceOf<ResourcesDict> (values [0]);
			} else {
				Assert.AreEqual (0, values.Count, "#3");
			}

			var ambientValues = provider.GetAllAmbientValues(null, false, types, properties).ToList();
			count = 0;
			if (Equals(Key, "TestDictItem"))
			{
				// inside ambient value, should be returned as well
				Assert.AreEqual(3, ambientValues.Count, "#4");
				Assert.IsInstanceOf<ResourcesDict>(ambientValues[count].Value);
				Assert.IsNull(ambientValues[count++].RetrievedProperty);
			}
			else
			{
				Assert.AreEqual(2, ambientValues.Count, "#5");
			}
			Assert.IsInstanceOf<ResourcesDict>(ambientValues[count].Value, "#6");
			Assert.AreEqual(properties[0], ambientValues[count++].RetrievedProperty, "#7");
			Assert.IsNull(ambientValues[count].Value, "#8");
			Assert.AreEqual(properties[1], ambientValues[count++].RetrievedProperty, "#9");

			foreach (var dict in ambientValues.Select(r => r.Value).OfType<ResourcesDict>())
			{
				if (dict.ContainsKey(this.Key))
					return dict[this.Key];
			}
			Assert.Fail("#10. Did not find resource");
			return null;
		}
	}

	public class ResourceContainer
	{
		[Ambient]
		public ResourcesDict Resources { get; } = new ResourcesDict();

		[Ambient]
		public ResourcesDict2 Resources2 { get; set; }

		// non-ambient, should throw exception when we try to get it
		public ResourcesDict Resources3 { get; set; }

		public TestObject TestObject { get; set; }
	}

	public class ResourceContainerSubclass : ResourceContainer
	{
	}

	[UsableDuringInitialization(true), Ambient]
	public class ResourcesDict : Dictionary<object, object>
	{
	}

	public class TestObject
	{
		public TestObject TestProperty { get; set; }
	}

#endregion

	public class ResourcesDict2 : Dictionary<object, object>
	{
	}

	public class TestObject2
	{
		public string TestProperty { get; set; }
	}

#region bug #683290
	[ContentProperty("Items")]
	public class SimpleType
	{
		public IList<SimpleType> Items { get; set; }

		public IList<SimpleType> NonContentItems { get; set; }

		public string TestProperty { get; set; }

		public SimpleType()
		{
			this.Items = new List<SimpleType>();
			this.NonContentItems = new List<SimpleType>();
		}
	}

	public class ContentPropertyContainer : Dictionary<object, object>
	{
	}
#endregion
}

#region "xamarin bug #2927"
namespace XamarinBug2927
{
	public class RootClass
	{
		public RootClass()
		{
			Child = new MyChildClass();
		}

		public bool Invoked;

		public ChildClass Child { get; set; }
	}

	public class MyRootClass : RootClass
	{
		public void HandleMyEvent(object sender, EventArgs e)
		{
			Invoked = true;
		}
	}

	public class RootClass2
	{
		public RootClass2()
		{
			Child = new MyChildClass();
		}

		public bool Invoked;

		public ChildClass Child { get; set; }

		public void HandleMyEvent(object sender, EventArgs e)
		{
			Invoked = true;
		}
	}

	public class MyRootClass2 : RootClass2
	{
	}

	public class ChildClass
	{
		public bool Invoked;

		public DescendantClass Descendant { get; set; }
	}

	public class MyChildClass : ChildClass
	{
		public MyChildClass()
		{
			Descendant = new DescendantClass() { Value = "x" };
		}

		public void HandleMyEvent(object sender, EventArgs e)
		{
			Invoked = true;
		}
	}

	public class DescendantClass
	{
		public bool Invoked;

		public event EventHandler DoWork;

		public string Value { get; set; }

		public void Work()
		{
			DoWork(this, EventArgs.Empty);
		}

		public void HandleMyEvent(object sender, EventArgs e)
		{
			Invoked = true;
		}
	}
}
#endregion

#region "xamarin bug 3003"

namespace XamarinBug3003
{
	public static class TestContext
	{
		public static StringWriter Writer = new StringWriter();
		
		public const string XmlInput = @"<Parent xmlns='http://schemas.example.com/test' Title='Parent Title'>
	<Child Parent.AssociatedProperty='child 1' Title='Child Title 1'></Child>	
	<Child Parent.AssociatedProperty='child 2' Title='Child Title 2'></Child>	
</Parent>";
		
		// In bug #3003 repro, there is output "Item 'Child' inserted at index 'x'" , but I couldn't get it in the output either on .NET or Mono.
		// On the other hand, in the standalone repro case they are in the output either in mono or in .NET. So I just stopped caring about that as it works as expected.
		public const string ExpectedResult = @"
Parent Constructed
ISupportInitialize.BeginInit: Parent
XamlObjectWriterSettings.AfterBeginInit: Parent
XamlObjectWriterSettings.BeforeProperties: Parent
XamlObjectWriterSettings.XamlSetValue: Parent Title, Member: Title
Parent.Title_set: Parent
Child Constructed
ISupportInitialize.BeginInit: Child
XamlObjectWriterSettings.AfterBeginInit: Child
XamlObjectWriterSettings.BeforeProperties: Child
XamlObjectWriterSettings.XamlSetValue: child 1, Member: AssociatedProperty
Parent.SetAssociatedProperty: child 1
XamlObjectWriterSettings.XamlSetValue: Child Title 1, Member: Title
Child.Title_set: Child
XamlObjectWriterSettings.AfterProperties: Child
ISupportInitialize.EndInit: Child
XamlObjectWriterSettings.AfterEndInit: Child
Child Constructed
ISupportInitialize.BeginInit: Child
XamlObjectWriterSettings.AfterBeginInit: Child
XamlObjectWriterSettings.BeforeProperties: Child
XamlObjectWriterSettings.XamlSetValue: child 2, Member: AssociatedProperty
Parent.SetAssociatedProperty: child 2
XamlObjectWriterSettings.XamlSetValue: Child Title 2, Member: Title
Child.Title_set: Child
XamlObjectWriterSettings.AfterProperties: Child
ISupportInitialize.EndInit: Child
XamlObjectWriterSettings.AfterEndInit: Child
XamlObjectWriterSettings.AfterProperties: Parent
ISupportInitialize.EndInit: Parent
XamlObjectWriterSettings.AfterEndInit: Parent
Loaded Parent
";
	}

	public class BaseItemCollection : Collection<BaseItem>
	{
		protected override void InsertItem(int index, BaseItem item)
		{
			base.InsertItem(index, item);
#if !NETFX_CORE
			Console.WriteLine("Item '{0}' inserted at index '{1}'", item, index);
#endif
		}
	}

	public class BaseItem : ISupportInitialize
	{
		Dictionary<string, object> properties = new Dictionary<string, object>();

		public Dictionary<string, object> Properties
		{
			get { return properties; }
		}

		string title;

		public string Title
		{
			get { return title; }
			set
			{
				title = value;
				TestContext.Writer.WriteLine("{0}.Title_set: {0}", this.GetType().Name, value);
			}
		}

		public BaseItem()
		{
			TestContext.Writer.WriteLine("{0} Constructed", this.GetType().Name);
		}


		public void BeginInit()
		{
			TestContext.Writer.WriteLine("ISupportInitialize.BeginInit: {0}", this);
		}

		public void EndInit()
		{
			TestContext.Writer.WriteLine("ISupportInitialize.EndInit: {0}", this);
		}

		public override string ToString()
		{
			return this.GetType().Name.ToString();
		}
	}

	public class Child : BaseItem
	{
	}

	[ContentProperty("Children")]
	public class Parent : BaseItem
	{
		BaseItemCollection children = new BaseItemCollection();

		public BaseItemCollection Children
		{
			get { return children; }
		}

		
		public static string GetAssociatedProperty(Child child)
		{
			object value;
			if (child.Properties.TryGetValue("myassociatedproperty", out value))
				return value as string;
			return null;
		}

		public static void SetAssociatedProperty(Child child, string value)
		{
			TestContext.Writer.WriteLine("Parent.SetAssociatedProperty: {0}", value);
			child.Properties["myassociatedproperty"] = value;
		}
		
	}
}

#endregion
