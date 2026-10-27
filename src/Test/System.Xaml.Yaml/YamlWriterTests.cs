#if PORTABLE_XAML
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Portable.Xaml;
using Portable.Xaml.Yaml;

namespace Tests.Portable.Xaml.Yaml
{
	[TestFixture]
	public class YamlWriterTests
	{
		[Test]
		public void Write_TestClass4()
		{
			var instance = new TestClass4 { Foo = "bar", Bar = "foo" };
			var yaml = XamlYamlServices.Save(instance);
			var expected = @"$ns: clr-namespace:Tests.Portable.Xaml;assembly=Tests.Portable.Xaml
$type: TestClass4
Bar: foo
Foo: bar
";
			Assert.AreEqual(expected.UpdateXml(), yaml);
		}

		[Test]
		public void Write_TestClass4_WithNull()
		{
			var instance = new TestClass4 { Foo = "bar", Bar = null };
			var yaml = XamlYamlServices.Save(instance);
			var expected = @"$ns: clr-namespace:Tests.Portable.Xaml;assembly=Tests.Portable.Xaml
$ns:x: http://schemas.microsoft.com/winfx/2006/xaml
$type: TestClass4
Bar: null
Foo: bar
";
			Assert.AreEqual(expected.UpdateXml(), yaml);
		}

		[Test]
		public void Write_ListWrapper()
		{
			var instance = new ListWrapper { Items = { 1, 2, 5 } };
			var yaml = XamlYamlServices.Save(instance);
			var expected = @"$ns: clr-namespace:Tests.Portable.Xaml;assembly=Tests.Portable.Xaml
$ns:x: http://schemas.microsoft.com/winfx/2006/xaml
$type: ListWrapper
Items:
  - 1
  - 2
  - 5
";
			Assert.AreEqual(expected.UpdateXml(), yaml);
		}

		[Test]
		public void Write_Items()
		{
			var instance = new YamlItem
			{
				Text = "multiple\nlines\n",
				Items = { new YamlItem { Text = "a", Child = new YamlItem() }, new YamlItem { Text = "b" } },
				Lookup = { ["key"] = new YamlItem { Text = "c" } },
				Values = { 5, "five", TimeSpan.FromMinutes(1) }
			};
			var yaml = XamlYamlServices.Save(instance);
			var expected = @"$ns: clr-namespace:Tests.Portable.Xaml.Yaml;assembly=Tests.Portable.Xaml
$ns:x: http://schemas.microsoft.com/winfx/2006/xaml
$type: YamlItem
Duration: 00:00:00
Flag: False
Number: 0
Text: |
  multiple
  lines
Child: null
Items:
  - Duration: 00:00:00
    Flag: False
    Number: 0
    Text: a
    Child:
      Duration: 00:00:00
      Flag: False
      Number: 0
      Text: null
      Child: null
  - Duration: 00:00:00
    Flag: False
    Number: 0
    Text: b
    Child: null
Lookup:
  key:
    Duration: 00:00:00
    Flag: False
    Number: 0
    Text: c
    Child: null
Values:
  - $type: x:Int32
    $value: 5
  - five
  - $type: x:TimeSpan
    $value: 00:01:00
";
			Assert.AreEqual(expected.UpdateXml(), yaml);
		}

		[Test]
		public void Write_Null()
		{
			var expected = @"$ns:x: http://schemas.microsoft.com/winfx/2006/xaml
$type: x:Null
";
			Assert.AreEqual(expected.UpdateXml(), XamlYamlServices.Save(null));
			Assert.IsNull(XamlYamlServices.Parse(expected));
		}

		[TestCase("true")]
		[TestCase("null")]
		[TestCase("~")]
		[TestCase("5")]
		[TestCase("-1.5e3")]
		[TestCase("yes")]
		[TestCase("")]
		[TestCase(" padded ")]
		[TestCase("- item")]
		[TestCase("key: value")]
		[TestCase("text # not a comment")]
		[TestCase("#hash")]
		[TestCase("'quoted'")]
		[TestCase("\"double\"")]
		[TestCase("{0} items")]
		[TestCase("{x:Null}")]
		[TestCase("[array]")]
		[TestCase("&anchor")]
		[TestCase("*alias")]
		[TestCase("!tag")]
		[TestCase("---")]
		[TestCase("tab\tand\\backslash")]
		[TestCase("line\r\nbreaks")]
		[TestCase("\n\nleading breaks")]
		[TestCase("  indented\nlines")]
		[TestCase("trailing breaks\n\n\n")]
		public void TextShouldRoundTrip(string text)
		{
			var yaml = XamlYamlServices.Save(new TestClass4 { Foo = text });
			var instance = (TestClass4)XamlYamlServices.Parse(yaml);
			Assert.AreEqual(text, instance.Foo, yaml);
		}

		[Test]
		public void UnicodeShouldRoundTrip()
		{
			// accented, line separator, control and byte order mark characters
			TextShouldRoundTrip("unicode " + (char)0xE9 + " " + (char)0x2028 + " " + (char)0x1 + " " + (char)0xFEFF);
		}

		[Test]
		public void MergeKeyShouldRoundTrip()
		{
			var yaml = XamlYamlServices.Save(new Dictionary<string, TestClass4> { ["<<"] = new TestClass4 { Foo = "f" } });
			var instance = (Dictionary<string, TestClass4>)XamlYamlServices.Parse(yaml);
			Assert.AreEqual("f", instance["<<"].Foo, yaml);
		}

		[Test]
		public void MarkupExtensionsShouldBeText()
		{
			var yaml = XamlYamlServices.Save(new Dictionary<string, Type> { ["int"] = typeof(int), ["list"] = typeof(List<int>) });
			StringAssert.Contains("int: '{x:Type x:Int32}'", yaml);
			StringAssert.Contains("list: '{x:Type List(x:Int32)}'", yaml);
		}

		static IEnumerable<string> XmlFiles()
		{
			var prefix = Compat.TestAssemblyName + ".XmlFiles.";
			return Assembly.GetExecutingAssembly().GetManifestResourceNames()
				.Where(r => r.StartsWith(prefix, StringComparison.Ordinal) && !r.EndsWith("ChangeLog", StringComparison.Ordinal))
				.Select(r => r.Substring(prefix.Length))
				// the order of a hash set's items isn't kept, and a stack is reversed, when loaded again
				.Where(r => r != "ImmutableCollectionContainer.xml" && CanRoundTripXaml(r))
				.OrderBy(r => r);
		}

		// some files test invalid XAML
		static bool CanRoundTripXaml(string fileName)
		{
			try
			{
				XamlServices.Save(XamlServices.Parse(Compat.GetTestFileText(fileName).UpdateXml()));
				return true;
			}
			catch
			{
				return false;
			}
		}

		[TestCaseSource(nameof(XmlFiles))]
		public void XamlShouldRoundTrip(string fileName)
		{
			var instance = XamlServices.Parse(Compat.GetTestFileText(fileName).UpdateXml());
			var expected = XamlServices.Save(instance);
			var yaml = XamlYamlServices.Save(instance);
			var loaded = XamlYamlServices.Parse(yaml);
			Assert.AreEqual(expected, XamlServices.Save(loaded), yaml);
		}
	}
}
#endif
