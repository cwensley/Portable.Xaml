#if PCL
using System;
using NUnit.Framework;
using Portable.Xaml.Json;

namespace MonoTests.Portable.Xaml.Json
{
	[TestFixture]
	public class JsonWriterTests
	{
		[Test]
		public void Write_TestClass4()
		{
			var instance = new TestClass4 { Foo = "bar", Bar = "foo" };
			var json = XamlJsonServices.Save(instance);
			var expected = @"{""$ns"":""clr-namespace:MonoTests.Portable.Xaml;assembly=Portable.Xaml_test_net_4_0"",""$type"":""TestClass4"",""Bar"":""foo"",""Foo"":""bar""}";
			Assert.AreEqual(expected.UpdateJson(), json);
		}

		[Test]
		public void Write_TestClass4_WithNull()
		{
			var instance = new TestClass4 { Foo = "bar", Bar = null};
			var json = XamlJsonServices.Save(instance);
			var expected = @"{""$ns"":""clr-namespace:MonoTests.Portable.Xaml;assembly=Portable.Xaml_test_net_4_0"",""$ns:x"":""http://schemas.microsoft.com/winfx/2006/xaml"",""$type"":""TestClass4"",""Bar"":null,""Foo"":""bar""}";
			Assert.AreEqual(expected.UpdateJson(), json);
		}

		[Test]
		public void Write_ListWrapper()
		{
			var instance = new ListWrapper { Items = { 1, 2, 5 } };
			var json = XamlJsonServices.Save(instance);
			var expected = @"{""$ns"":""clr-namespace:MonoTests.Portable.Xaml;assembly=Portable.Xaml_test_net_4_0"",""$ns:x"":""http://schemas.microsoft.com/winfx/2006/xaml"",""$type"":""ListWrapper"",""Items"":[1,2,5]}";
			Assert.AreEqual(expected.UpdateJson(), json);
		}
	}
}
#endif