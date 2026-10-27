#if PORTABLE_XAML
using System;
using System.IO;
using NUnit.Framework;
using Portable.Xaml.Json;

namespace Tests.Portable.Xaml.Json
{
	[TestFixture]
	public class JsonWriterTests
	{
		[Test]
		public void Write_TestClass4()
		{
			var instance = new TestClass4 { Foo = "bar", Bar = "foo" };
			var json = XamlJsonServices.Save(instance);
			var expected = @"{""$ns"":""clr-namespace:Tests.Portable.Xaml;assembly=Tests.Portable.Xaml"",""$type"":""TestClass4"",""Bar"":""foo"",""Foo"":""bar""}";
			Assert.AreEqual(expected.UpdateJson(), json);
		}

		[Test]
		public void Write_TestClass4_WithNull()
		{
			var instance = new TestClass4 { Foo = "bar", Bar = null};
			var json = XamlJsonServices.Save(instance);
			var expected = @"{""$ns"":""clr-namespace:Tests.Portable.Xaml;assembly=Tests.Portable.Xaml"",""$ns:x"":""http://schemas.microsoft.com/winfx/2006/xaml"",""$type"":""TestClass4"",""Bar"":null,""Foo"":""bar""}";
			Assert.AreEqual(expected.UpdateJson(), json);
		}

		[Test]
		public void Write_ListWrapper()
		{
			var instance = new ListWrapper { Items = { 1, 2, 5 } };
			var json = XamlJsonServices.Save(instance);
			var expected = @"{""$ns"":""clr-namespace:Tests.Portable.Xaml;assembly=Tests.Portable.Xaml"",""$ns:x"":""http://schemas.microsoft.com/winfx/2006/xaml"",""$type"":""ListWrapper"",""Items"":[1,2,5]}";
			Assert.AreEqual(expected.UpdateJson(), json);
		}

		[Test]
		public void FileShouldRoundTrip()
		{
			var fileName = Path.GetTempFileName();
			try
			{
				XamlJsonServices.Save(fileName, new TestClass4 { Foo = "f" });
				Assert.AreEqual("f", ((TestClass4)XamlJsonServices.Load(fileName)).Foo);
			}
			finally
			{
				File.Delete(fileName);
			}
		}

		[Test]
		public void StringsShouldBeEscaped()
		{
			var text = "\"quoted\" \\ a\nline\u0001" + new string('a', 2000) + "\"";
			var json = XamlJsonServices.Save(new TestClass4 { Foo = text });
			StringAssert.Contains(@"""Foo"":""\""quoted\"" \\ a\nline\u0001", json);
			Assert.AreEqual(text, ((TestClass4)XamlJsonServices.Parse(json)).Foo);
		}

		[Test]
		public void ValuesShouldUseJsonTypes()
		{
			var json = XamlJsonServices.Save(new JsonWriterValues { Flag = true, Number = 5, Value = 1.5, Optional = 3, Kind = DayOfWeek.Monday, NotANumber = double.NaN });
			StringAssert.Contains(@"""Flag"":true", json);
			StringAssert.Contains(@"""Number"":5", json);
			StringAssert.Contains(@"""Value"":1.5", json);
			StringAssert.Contains(@"""Optional"":3", json);
			StringAssert.Contains(@"""Kind"":""Monday""", json);
			StringAssert.Contains(@"""NotANumber"":""NaN""", json);
		}

		[Test]
		public void TypeShouldOnlyBeWrittenWhenDifferentFromMember()
		{
			var json = XamlJsonServices.Save(new JsonWriterValues { Child = new JsonWriterValues { Number = 1 }, Other = new JsonWriterValues { Number = 2 } });
			StringAssert.Contains(@"""Child"":{""Flag""", json);
			StringAssert.Contains(@"""Other"":{""$type"":""JsonWriterValues""", json);

			var loaded = (JsonWriterValues)XamlJsonServices.Parse(json);
			Assert.AreEqual(1, loaded.Child.Number);
			Assert.AreEqual(2, ((JsonWriterValues)loaded.Other).Number);
		}

		[Test]
		public void DictionaryShouldBeWritten()
		{
			var instance = new JsonWriterValues { Lookup = { ["a"] = "first", ["b"] = null }, Number = 2 };
			var json = XamlJsonServices.Save(instance);
			StringAssert.Contains(@"""Lookup"":{""a"":""first"",""b"":null}", json);

			var loaded = (JsonWriterValues)XamlJsonServices.Parse(json);
			Assert.AreEqual("first", loaded.Lookup["a"]);
			Assert.AreEqual(2, loaded.Number);
		}
	}

	public class JsonWriterValues
	{
		public bool Flag { get; set; }
		public int Number { get; set; }
		public double Value { get; set; }
		public double NotANumber { get; set; }
		public int? Optional { get; set; }
		public DayOfWeek Kind { get; set; }
		public JsonWriterValues Child { get; set; }
		public object Other { get; set; }
		public System.Collections.Generic.Dictionary<string, string> Lookup { get; } = new System.Collections.Generic.Dictionary<string, string>();
	}
}
#endif