#if PORTABLE_XAML
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Portable.Xaml;
using Portable.Xaml.Yaml;
using Tests.Portable.Xaml.Json;

namespace Tests.Portable.Xaml.Yaml
{
	public class YamlItem
	{
		public string Text { get; set; }
		public int Number { get; set; }
		public bool Flag { get; set; }
		public TimeSpan Duration { get; set; }
		public YamlItem Child { get; set; }
		public List<YamlItem> Items { get; } = new List<YamlItem>();
		public List<object> Values { get; } = new List<object>();
		public Dictionary<string, YamlItem> Lookup { get; } = new Dictionary<string, YamlItem>();
	}

	/// <summary>Fills in tables, rows and cells the YAML leaves out, like Eto's .jeto files.</summary>
	class TableYamlReader : XamlYamlReader
	{
		public TableYamlReader(TextReader reader, XamlSchemaContext context, XamlYamlReaderSettings settings)
			: base(reader, context, settings)
		{
		}

		XamlType Get<T>() => SchemaContext.GetXamlType(typeof(T));

		protected override XamlType GetObjectType(XamlType expectedType, IList<string> propertyNames)
			=> expectedType == Get<JsonCell>() && propertyNames.Contains("Rows") ? Get<JsonTable>() : expectedType;

		protected override XamlMember GetArrayMember(XamlType expectedType, out XamlType objectType)
		{
			objectType = null;
			if (expectedType == Get<JsonCell>())
				return Get<JsonTable>().GetMember("Rows");
			if (expectedType == Get<JsonRow>())
				return Get<JsonRow>().GetMember("Cells");
			return null;
		}

		protected override XamlMember GetTextMember(XamlType expectedType, out XamlType objectType)
		{
			objectType = null;
			return expectedType == Get<JsonCell>() ? Get<JsonCell>().GetMember("Text") : null;
		}
	}

	[TestFixture]
	public class YamlReaderTests
	{
		static XamlYamlReaderSettings CreateSettings(XamlSchemaContext context, Type rootType = null)
		{
			var settings = new XamlYamlReaderSettings { LocalAssembly = typeof(YamlItem).Assembly, ProvideLineInfo = true };
			settings.AddNamespace(null, "clr-namespace:Tests.Portable.Xaml.Yaml");
			settings.AddNamespace("j", "clr-namespace:Tests.Portable.Xaml.Json");
			settings.AddNamespace("x", XamlLanguage.Xaml2006Namespace);
			if (rootType != null)
				settings.RootType = context.GetXamlType(rootType);
			return settings;
		}

		static T Load<T>(string yaml, Func<TextReader, XamlSchemaContext, XamlYamlReaderSettings, XamlReader> createReader = null)
		{
			var context = new XamlSchemaContext();
			var settings = CreateSettings(context, typeof(T));
			var reader = createReader?.Invoke(new StringReader(yaml), context, settings) ?? new XamlYamlReader(new StringReader(yaml), context, settings);
			return (T)XamlServices.Load(reader);
		}

		static List<string> ReadNodes(string yaml)
		{
			var context = new XamlSchemaContext();
			var reader = new XamlYamlReader(new StringReader(yaml), context, CreateSettings(context, typeof(YamlItem)));
			var nodes = new List<string>();
			while (reader.Read())
				nodes.Add($"{reader.NodeType} {reader.LineNumber},{reader.LinePosition}");
			return nodes;
		}

		[Test]
		public void SavedYamlShouldLoad()
		{
			var yaml = XamlYamlServices.Save(new TestClass4 { Foo = "bar", Bar = null });
			var instance = (TestClass4)XamlYamlServices.Parse(yaml);
			Assert.AreEqual("bar", instance.Foo);
			Assert.IsNull(instance.Bar);
		}

		[Test]
		public void SavedCollectionShouldLoad()
		{
			var yaml = XamlYamlServices.Save(new ListWrapper { Items = { 1, 2, 5 } });
			var instance = (ListWrapper)XamlYamlServices.Parse(yaml);
			CollectionAssert.AreEqual(new[] { 1, 2, 5 }, instance.Items);
		}

		[Test]
		public void BlockAndFlowStylesShouldLoad()
		{
			// a line starting with # in a verbatim string looks like a directive to the editor
			var item = Load<YamlItem>("# a comment\n" + @"
Text: plain text # trailing comment
Number: 5
Flag: true
Child: { $type: YamlItem, Text: 'single ''quoted''', Child: {Text: ""double \""quoted\"" A""} }
Items:
- Text: a
  Number: 1
- { Text: b, }
-
  Text: c
Values: [ one, 'two', ]
");
			Assert.AreEqual("plain text", item.Text);
			Assert.AreEqual(5, item.Number);
			Assert.IsTrue(item.Flag);
			Assert.AreEqual("single 'quoted'", item.Child.Text);
			Assert.AreEqual("double \"quoted\" A", item.Child.Child.Text);
			CollectionAssert.AreEqual(new[] { "a", "b", "c" }, item.Items.Select(r => r.Text));
			Assert.AreEqual(1, item.Items[0].Number);
			CollectionAssert.AreEqual(new[] { "one", "two" }, item.Values);
		}

		[Test]
		public void IndentedSequencesShouldLoad()
		{
			var item = Load<YamlItem>(@"
Items:
  - Text: a
    Items:
    - Text: nested
  - Text: b
Number: 2
");
			CollectionAssert.AreEqual(new[] { "a", "b" }, item.Items.Select(r => r.Text));
			Assert.AreEqual("nested", item.Items[0].Items[0].Text);
			Assert.AreEqual(2, item.Number);
		}

		[Test]
		public void JsonShouldLoad()
		{
			var item = Load<YamlItem>(@"{""Text"":""a"",""Number"":5,""Items"":[{""Text"":""b""}]}");
			Assert.AreEqual("a", item.Text);
			Assert.AreEqual(5, item.Number);
			Assert.AreEqual("b", item.Items[0].Text);
		}

		[Test]
		public void MultiLineTextShouldLoad()
		{
			var item = Load<YamlItem>(@"
Text: |
  line 1
    indented

  line 3
Items:
  - Text: >
      folded
      text

      new paragraph
  - Text: |-
      no final break
  - Text: |+
      keep breaks

  - Text: plain text
      over lines
  - Text: ""quoted
      over lines""
  - Text: |2
       leading space
");
			Assert.AreEqual("line 1\n  indented\n\nline 3\n", item.Text);
			CollectionAssert.AreEqual(new[] {
				"folded text\nnew paragraph\n",
				"no final break",
				"keep breaks\n\n",
				"plain text over lines",
				"quoted over lines",
				" leading space\n"
			}, item.Items.Select(r => r.Text));
		}

		[Test]
		public void DocumentMarkersShouldBeSkipped()
		{
			var item = Load<YamlItem>("%YAML 1.2\n---\nText: a\n...\n");
			Assert.AreEqual("a", item.Text);
		}

		[Test]
		public void AnchorsAndMergeKeysShouldCopy()
		{
			var item = Load<YamlItem>(@"
Items:
  - &first
    Text: a
    Number: 1
  - *first
  - <<: *first
    Number: 2
");
			CollectionAssert.AreEqual(new[] { "a", "a", "a" }, item.Items.Select(r => r.Text));
			CollectionAssert.AreEqual(new[] { 1, 1, 2 }, item.Items.Select(r => r.Number));
			Assert.AreNotSame(item.Items[0], item.Items[1]);
		}

		[TestCase("'{'")]
		[TestCase("'{ not markup'")]
		public void TextThatIsNotMarkupShouldBeText(string text)
		{
			var item = Load<YamlItem>("Text: " + text);
			Assert.AreEqual(text.Trim('\''), item.Text);
		}

		[Test]
		public void TagOnAliasShouldNotChangeAnchor()
		{
			var item = Load<YamlItem>(@"
Values:
  - &a !j:JsonCell { Text: a }
  - !YamlItem *a
  - *a
");
			CollectionAssert.AreEqual(new[] { typeof(JsonCell), typeof(YamlItem), typeof(JsonCell) }, item.Values.Select(r => r.GetType()));
		}

		[Test]
		public void QuotedMergeKeyShouldBeAKey()
		{
			var item = Load<YamlItem>("Lookup:\n  '<<':\n    Text: a\n");
			Assert.AreEqual("a", item.Lookup["<<"].Text);
			item = Load<YamlItem>("Lookup: { \"<<\": { Text: b } }");
			Assert.AreEqual("b", item.Lookup["<<"].Text);
		}

		[Test]
		public void TagsShouldSetType()
		{
			var item = Load<YamlItem>(@"
Child: !YamlItem
  Text: tagged
Values:
  - !x:Int32 5
  - !!str 5
  - 5
  - !x:TimeSpan 00:01:00
");
			Assert.AreEqual("tagged", item.Child.Text);
			CollectionAssert.AreEqual(new object[] { 5, "5", "5", TimeSpan.FromMinutes(1) }, item.Values);
		}

		[Test]
		public void TypedValuesShouldLoad()
		{
			var item = Load<YamlItem>(@"
Duration: 00:02:00
Values:
  - $type: x:Int32
    $value: 5
  - $type: x:Double
    $value: .inf
");
			Assert.AreEqual(TimeSpan.FromMinutes(2), item.Duration);
			CollectionAssert.AreEqual(new object[] { 5, double.PositiveInfinity }, item.Values);
		}

		[Test]
		public void DictionaryShouldLoad()
		{
			var item = Load<YamlItem>(@"
Lookup:
  a: { Text: first }
  $b: { Text: second }
");
			// a key starting with $ is an item when given with $items
			Assert.AreEqual("first", item.Lookup["a"].Text);
			Assert.IsFalse(item.Lookup.ContainsKey("$b"));

			item = Load<YamlItem>(@"
Lookup:
  $items:
    $b: { Text: second }
");
			Assert.AreEqual("second", item.Lookup["$b"].Text);
		}

		[Test]
		public void TypeShouldBeFoundByNamespaceAndAssembly()
		{
			var item = Load<YamlItem>(@"
Child: { $type: Tests.Portable.Xaml.Yaml.YamlItem, Text: local }
Items: [ { $type: 'Tests.Portable.Xaml.Yaml.YamlItem, " + typeof(YamlItem).Assembly.GetName().Name + @"', Text: qualified } ]
Values:
  - $type: scg:List(x:Int32)
    $ns:scg: clr-namespace:System.Collections.Generic;assembly=mscorlib
    $items: [ 1, 2 ]
");
			Assert.AreEqual("local", item.Child.Text);
			Assert.AreEqual("qualified", item.Items[0].Text);
			CollectionAssert.AreEqual(new[] { 1, 2 }, (List<int>)item.Values[0]);
		}

		[Test]
		public void MarkupExtensionsShouldOnlyBeUsedWhenNamed()
		{
			var item = Load<YamlItem>(@"
Text: '{x:Null}'
Items: [ { Text: '{0} items' }, { Text: '{}{x:Null}' }, { Text: '{Unknown}' } ]
");
			Assert.IsNull(item.Text);
			CollectionAssert.AreEqual(new[] { "{0} items", "{x:Null}", "{Unknown}" }, item.Items.Select(r => r.Text));
		}

		[Test]
		public void NullShouldSetNull()
		{
			var item = Load<YamlItem>("Text: null\nItems: [ ~, null ]\nChild:\nValues:\n  - \n  - ''\n");
			Assert.IsNull(item.Text);
			CollectionAssert.AreEqual(new YamlItem[] { null, null }, item.Items);
			Assert.IsNull(item.Child);
			CollectionAssert.AreEqual(new object[] { null, string.Empty }, item.Values);
		}

		[Test]
		public void NameShouldBeXamlName()
		{
			var context = new XamlSchemaContext();
			var reader = new XamlYamlReader(new StringReader("Child: { $name: child }"), context, CreateSettings(context, typeof(YamlItem)));
			var members = new List<XamlMember>();
			while (reader.Read())
			{
				if (reader.NodeType == XamlNodeType.StartMember)
					members.Add(reader.Member);
			}
			CollectionAssert.Contains(members, XamlLanguage.Name);
		}

		[Test]
		public void MissingTypeShouldThrowWithLineInfo()
		{
			var ex = Assert.Throws<XamlParseException>(() => Load<YamlItem>("Text: a\nChild:\n  $type: Missing\n"));
			Assert.AreEqual(3, ex.LineNumber);
			Assert.AreEqual(10, ex.LinePosition);
		}

		[Test]
		public void LineInfoShouldStartAtOne()
		{
			CollectionAssert.AreEqual(new[] { "StartObject 1,1", "StartMember 1,1", "Value 1,7", "EndMember 1,7", "StartMember 2,1", "Value 2,9", "EndMember 2,9", "EndObject 1,1" }, ReadNodes("Text: a\nNumber: 5\n"));
		}

		[TestCase("Text: a\n\tNumber: 5", 2, 1)]
		[TestCase("Text: a\nText: b", 2, 1)]
		[TestCase("Text: a\n  Number: 5", 2, 9)]
		[TestCase("Text: 'a", 1, 9)]
		[TestCase("Items: [ a", 1, 11)]
		[TestCase("Text: a\n---\nText: b", 2, 1)]
		[TestCase("Items: *missing", 1, 8)]
		[TestCase("Text: \"\\U00110000\"", 1, 10)]
		[TestCase("Text: \"\\UFFFFFFFF\"", 1, 10)]
		[TestCase("$type: ,", 1, 8)]
		[TestCase("$type: Missing, MissingAssembly", 1, 8)]
		[TestCase("$type: Missing,", 1, 8)]
		public void InvalidYamlShouldThrowWithLineInfo(string yaml, int line, int column)
		{
			var ex = Assert.Throws<XamlParseException>(() => ReadNodes(yaml));
			Assert.AreEqual(line, ex.LineNumber, ex.Message);
			Assert.AreEqual(column, ex.LinePosition, ex.Message);
		}

		[Test]
		public void SubclassShouldFillInTypes()
		{
			var table = Load<JsonTable>(@"
Rows:
  - [ a, { Text: b } ]
  - [ [ [ nested ] ] ]
  - - Rows: [ [ c ] ]
    - null
", (r, c, s) => new TableYamlReader(r, c, s));

			Assert.AreEqual(3, table.Rows.Count);
			CollectionAssert.AreEqual(new[] { "a", "b" }, table.Rows[0].Cells.Select(r => r.Text));
			Assert.AreEqual("nested", table.Rows[1].Cells[0].Table.Rows[0].Cells[0].Text);
			Assert.AreEqual("c", table.Rows[2].Cells[0].Table.Rows[0].Cells[0].Text);
			Assert.IsNull(table.Rows[2].Cells[1]);
		}

		[Test]
		public void SequenceWithoutCollectionShouldThrow()
		{
			Assert.Throws<XamlParseException>(() => Load<YamlItem>("Child: [ a ]"));
		}
	}
}
#endif
