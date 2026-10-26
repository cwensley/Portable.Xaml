using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Portable.Xaml.Markup;

[assembly: Portable.Xaml.Markup.XmlnsDefinition ("http://example.com/benchmark", "Benchmark.Portable.Xaml")]

#if SYSTEM_XAML
[assembly: System.Windows.Markup.XmlnsDefinition ("http://example.com/benchmark", "Benchmark.Portable.Xaml")]
#endif

namespace Benchmark.Portable.Xaml
{
	[ContentProperty ("Children")]
#if SYSTEM_XAML
	[System.Windows.Markup.ContentProperty ("Children")]
#endif
	public class TestObject
	{
		public string StringProperty { get; set; }

		public List<ChildObject> Children { get; } = new List<ChildObject> ();
	}

	public class ChildObject
	{
		public string StringProperty { get; set; }
		public bool BoolProperty { get; set; }
		public double DoubleProperty { get; set; }
		public int IntProperty { get; set; }
	}

	public enum ItemKind
	{
		None,
		Primary,
		Secondary,
		Hidden
	}

	public class ItemStyle
	{
		public string FontFamily { get; set; }
		public double FontSize { get; set; }
		public bool Bold { get; set; }
		public string Color { get; set; }
		public ItemKind Kind { get; set; }
	}

	public abstract class ItemBase
	{
		public string Id { get; set; }
		public string Title { get; set; }
		public bool Visible { get; set; } = true;
		public int? Order { get; set; }
		public ItemStyle Style { get; set; }
	}

	public class TextItem : ItemBase
	{
		public string Text { get; set; }
		public int MaxLength { get; set; }
	}

	public class NumberItem : ItemBase
	{
		public double Value { get; set; }
		public double Minimum { get; set; }
		public double Maximum { get; set; }
		public double? Step { get; set; }
	}

	public class ChoiceItem : ItemBase
	{
		public ItemKind Kind { get; set; }
		public List<string> Options { get; } = new List<string> ();
	}

	[ContentProperty ("Items")]
#if SYSTEM_XAML
	[System.Windows.Markup.ContentProperty ("Items")]
#endif
	public class GroupItem : ItemBase
	{
		public bool Expanded { get; set; }
		public List<ItemBase> Items { get; } = new List<ItemBase> ();
	}

	[ContentProperty ("Groups")]
#if SYSTEM_XAML
	[System.Windows.Markup.ContentProperty ("Groups")]
#endif
	public class ComplexObject
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public int Version { get; set; }
		public ItemKind Kind { get; set; }
		public ItemStyle DefaultStyle { get; set; }
		public List<string> Tags { get; } = new List<string> ();
		public Dictionary<string, string> Properties { get; } = new Dictionary<string, string> ();
		public List<GroupItem> Groups { get; } = new List<GroupItem> ();
	}

	/// <summary>Objects the save benchmarks write, matching what the load benchmarks read.</summary>
	public static class TestData
	{
		public static TestObject Simple { get; } = CreateSimple ();

		public static ComplexObject Complex { get; } = CreateComplex ();

		static TestObject CreateSimple ()
		{
			var obj = new TestObject { StringProperty = "Hello!" };
			for (int i = 0; i < 50; i++)
			{
				obj.Children.Add (new ChildObject
				{
					StringProperty = "Child " + i,
					BoolProperty = i % 2 == 0,
					DoubleProperty = 100 + i * 0.25,
					IntProperty = i * 10
				});
			}
			return obj;
		}

		static ComplexObject CreateComplex ()
		{
			var obj = new ComplexObject
			{
				Name = "Settings",
				Description = "Application settings with \"quoted\" text, <markup> & a\nsecond line",
				Version = 3,
				Kind = ItemKind.Primary,
				DefaultStyle = new ItemStyle { FontFamily = "Segoe UI", FontSize = 12, Color = "#FF202020", Kind = ItemKind.Primary },
				Tags = { "general", "appearance", "advanced", "experimental" },
				Properties =
				{
					["author"] = "Benchmark",
					["license"] = "MIT",
					["culture"] = "en-US",
					["theme"] = "dark"
				}
			};
			for (int g = 0; g < 5; g++)
			{
				var group = CreateGroup ("group" + g, "Group " + g, 8, g);
				// one level deeper so the tree isn't just a single level of items
				for (int s = 0; s < 2; s++)
					group.Items.Add (CreateGroup ($"group{g}.{s}", $"Group {g}.{s}", 4, g + s));
				obj.Groups.Add (group);
			}
			return obj;
		}

		static GroupItem CreateGroup (string id, string title, int count, int seed)
		{
			var group = new GroupItem { Id = id, Title = title, Expanded = seed % 2 == 0, Order = seed };
			for (int i = 0; i < count; i++)
			{
				var itemId = id + ".item" + i;
				switch ((i + seed) % 3)
				{
					case 0:
						group.Items.Add (new TextItem
						{
							Id = itemId,
							Title = "Text " + i,
							Text = "Some text value " + i,
							MaxLength = 100 + i,
							Style = i % 2 == 0 ? new ItemStyle { FontFamily = "Consolas", FontSize = 10.5, Bold = true, Color = "#FF0000FF", Kind = ItemKind.Secondary } : null
						});
						break;
					case 1:
						group.Items.Add (new NumberItem
						{
							Id = itemId,
							Title = "Number " + i,
							Value = i * 1.25,
							Minimum = -100,
							Maximum = 100,
							Step = i % 2 == 0 ? 0.5 : (double?)null,
							Order = i
						});
						break;
					default:
						group.Items.Add (new ChoiceItem
						{
							Id = itemId,
							Title = "Choice " + i,
							Visible = i % 4 != 0,
							Kind = (ItemKind)(i % 4),
							Options = { "First", "Second", "Third" }
						});
						break;
				}
			}
			return group;
		}
	}
}
