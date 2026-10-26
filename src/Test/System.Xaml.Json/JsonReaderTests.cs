#if PORTABLE_XAML
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Portable.Xaml;
using Portable.Xaml.Json;
using Tests.Portable.Xaml;

namespace Tests.Portable.Xaml.Json
{
	public class JsonItem
	{
		public string Text { get; set; }
		public int Number { get; set; }
		public bool Flag { get; set; }
		public JsonItem Child { get; set; }
		public List<JsonItem> Items { get; } = new List<JsonItem>();
	}

	[TypeConverter(typeof(JsonCellConverter))]
	public class JsonCell
	{
		public string Text { get; set; }
		public JsonTable Table { get; set; }
	}

	public class JsonCellConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) => sourceType == typeof(JsonTable) || base.CanConvertFrom(context, sourceType);

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
			=> value is JsonTable table ? new JsonCell { Table = table } : base.ConvertFrom(context, culture, value);
	}

	public class JsonRow
	{
		public List<JsonCell> Cells { get; } = new List<JsonCell>();
	}

	public class JsonTable
	{
		public List<JsonRow> Rows { get; } = new List<JsonRow>();
	}

	/// <summary>Fills in tables, rows and cells the JSON leaves out, like Eto's .jeto files.</summary>
	class TableJsonReader : XamlJsonReader
	{
		public TableJsonReader(TextReader reader, XamlSchemaContext context, XamlJsonReaderSettings settings)
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
	public class JsonReaderTests
	{
		static XamlJsonReaderSettings CreateSettings(XamlSchemaContext context, Type rootType = null)
		{
			var settings = new XamlJsonReaderSettings { LocalAssembly = typeof(JsonItem).Assembly, ProvideLineInfo = true };
			settings.AddNamespace(null, "clr-namespace:Tests.Portable.Xaml.Json");
			settings.AddNamespace("x", XamlLanguage.Xaml2006Namespace);
			if (rootType != null)
				settings.RootType = context.GetXamlType(rootType);
			return settings;
		}

		static T Load<T>(string json, Func<TextReader, XamlSchemaContext, XamlJsonReaderSettings, XamlReader> createReader = null)
		{
			var context = new XamlSchemaContext();
			var settings = CreateSettings(context, typeof(T));
			var reader = createReader?.Invoke(new StringReader(json), context, settings) ?? new XamlJsonReader(new StringReader(json), context, settings);
			return (T)XamlServices.Load(reader);
		}

		[Test]
		public void SavedJsonShouldLoad()
		{
			var json = XamlJsonServices.Save(new TestClass4 { Foo = "bar", Bar = null });
			var instance = (TestClass4)XamlJsonServices.Parse(json);
			Assert.AreEqual("bar", instance.Foo);
			Assert.IsNull(instance.Bar);
		}

		[Test]
		public void SavedCollectionShouldLoad()
		{
			var json = XamlJsonServices.Save(new ListWrapper { Items = { 1, 2, 5 } });
			var instance = (ListWrapper)XamlJsonServices.Parse(json);
			CollectionAssert.AreEqual(new[] { 1, 2, 5 }, instance.Items);
		}

		[Test]
		public void RelaxedJsonShouldLoad()
		{
			var item = Load<JsonItem>(@"
// a comment
{
	Text: 'single quoted',
	/* another comment */
	""Number"": 5,
	Flag: true,
	Child: { $type: ""JsonItem"", Text: ""escaped \""quote\"" A"", },
	Items: [ { Text: 'a' }, { Text: 'b' }, ],
}");
			Assert.AreEqual("single quoted", item.Text);
			Assert.AreEqual(5, item.Number);
			Assert.IsTrue(item.Flag);
			Assert.AreEqual("escaped \"quote\" A", item.Child.Text);
			CollectionAssert.AreEqual(new[] { "a", "b" }, item.Items.Select(r => r.Text));
		}

		[Test]
		public void TypeShouldBeFoundByNamespaceAndAssembly()
		{
			var item = Load<JsonItem>(@"{
	Child: { $type: ""Tests.Portable.Xaml.Json.JsonItem"", Text: ""local"" },
	Items: [ { $type: ""Tests.Portable.Xaml.Json.JsonItem, " + typeof(JsonItem).Assembly.GetName().Name + @""", Text: ""qualified"" } ]
}");
			Assert.AreEqual("local", item.Child.Text);
			Assert.AreEqual("qualified", item.Items[0].Text);
		}

		[Test]
		public void MarkupExtensionsShouldOnlyBeUsedWhenNamed()
		{
			var item = Load<JsonItem>(@"{
	Text: ""{x:Null}"",
	Items: [ { Text: ""{0} items"" }, { Text: ""{}{x:Null}"" }, { Text: ""{Unknown}"" } ]
}");
			Assert.IsNull(item.Text);
			CollectionAssert.AreEqual(new[] { "{0} items", "{x:Null}", "{Unknown}" }, item.Items.Select(r => r.Text));
		}

		[Test]
		public void NullShouldSetNull()
		{
			var item = Load<JsonItem>(@"{ Text: null, Items: [ null ] }");
			Assert.IsNull(item.Text);
			Assert.AreEqual(1, item.Items.Count);
			Assert.IsNull(item.Items[0]);
		}

		[Test]
		public void NameShouldBeXamlName()
		{
			var context = new XamlSchemaContext();
			var reader = new XamlJsonReader(new StringReader(@"{ Child: { $name: ""child"" } }"), context, CreateSettings(context, typeof(JsonItem)));
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
			var ex = Assert.Throws<XamlParseException>(() => Load<JsonItem>("{\n\tChild: { $type: \"Missing\" }\n}"));
			Assert.AreEqual(2, ex.LineNumber);
			Assert.AreEqual(18, ex.LinePosition);
		}

		[Test]
		public void LineInfoShouldStartAtOne()
		{
			var context = new XamlSchemaContext();
			var reader = new XamlJsonReader(new StringReader("{\n\tText: \"a\"\n}"), context, CreateSettings(context, typeof(JsonItem)));
			var lines = new List<string>();
			while (reader.Read())
				lines.Add($"{reader.NodeType} {reader.LineNumber},{reader.LinePosition}");
			CollectionAssert.AreEqual(new[] { "StartObject 1,1", "StartMember 2,2", "Value 2,8", "EndMember 2,8", "EndObject 1,1" }, lines);
		}

		[Test]
		public void StringsAcrossReadBufferShouldLoad()
		{
			// lengths vary so strings start, end, and escape at every point of the reader's buffer
			var texts = Enumerable.Range(0, 100).Select(i => new string((char)('a' + i % 26), i * 13) + (i % 2 == 0 ? "\"" : "")).ToList();
			var escaped = texts.Select(r => r.Replace("\"", "\\\"")).ToList();
			var json = "{ Items: [\n" + string.Join(",\n", escaped.Select(r => "{ Text: \"" + r + "\", Flag: true }")) + "\n] }";
			var context = new XamlSchemaContext();
			var reader = new XamlJsonReader(new StringReader(json), context, CreateSettings(context, typeof(JsonItem)));
			var positions = new List<string>();
			while (reader.Read())
			{
				if (reader.NodeType == XamlNodeType.Value || (reader.NodeType == XamlNodeType.StartMember && reader.Member.Name == "Flag"))
					positions.Add($"{reader.LineNumber},{reader.LinePosition}");
			}
			// the Flag after each string shows the string moved the position by its length
			CollectionAssert.AreEqual(escaped.SelectMany((r, i) => new[] { $"{i + 2},9", $"{i + 2},{r.Length + 13}", $"{i + 2},{r.Length + 19}" }), positions);

			CollectionAssert.AreEqual(texts, Load<JsonItem>(json).Items.Select(r => r.Text));
		}

		[Test]
		public void SubclassShouldFillInTypes()
		{
			var table = Load<JsonTable>(@"{
	Rows: [
		[ ""a"", { Text: ""b"" } ],
		[ [ [ ""nested"" ] ] ],
		[ { Rows: [ [ ""c"" ] ] }, null ],
	]
}", (r, c, s) => new TableJsonReader(r, c, s));

			Assert.AreEqual(3, table.Rows.Count);
			CollectionAssert.AreEqual(new[] { "a", "b" }, table.Rows[0].Cells.Select(r => r.Text));
			Assert.AreEqual("nested", table.Rows[1].Cells[0].Table.Rows[0].Cells[0].Text);
			Assert.AreEqual("c", table.Rows[2].Cells[0].Table.Rows[0].Cells[0].Text);
			Assert.IsNull(table.Rows[2].Cells[1]);
		}

		[Test]
		public void ArrayWithoutCollectionShouldThrow()
		{
			Assert.Throws<XamlParseException>(() => Load<JsonItem>(@"{ Child: [ ""a"" ] }"));
		}
	}
}
#endif
