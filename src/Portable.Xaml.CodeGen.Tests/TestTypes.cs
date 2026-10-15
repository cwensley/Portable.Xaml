using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Portable.Xaml;
using Portable.Xaml.Markup;

[assembly: XmlnsDefinition("urn:codegen-test", "Portable.Xaml.CodeGen.Tests.Types")]
[assembly: XmlnsPrefix("urn:codegen-test", "t")]
// compiled conversions for types that don't reference Portable.Xaml themselves
[assembly: XamlCodeGen(typeof(Portable.Xaml.CodeGen.Tests.Types.TestSize), typeof(Portable.Xaml.CodeGen.Tests.Types.TestConverters), nameof(Portable.Xaml.CodeGen.Tests.Types.TestConverters.Size))]
// ignored, as TestPoint's own attribute wins
[assembly: XamlCodeGen(typeof(Portable.Xaml.CodeGen.Tests.Types.TestPoint), typeof(Portable.Xaml.CodeGen.Tests.Types.TestConverters), nameof(Portable.Xaml.CodeGen.Tests.Types.TestConverters.Size))]

namespace Portable.Xaml.CodeGen.Tests.Types
{
	public enum TestMode { None, First, Second }

	[Flags]
	public enum TestFlags { None = 0, A = 1, B = 2, C = 4 }

	/// <summary>An enum with its own converter, like Eto's Keys ("Control+O").</summary>
	[Flags]
	[TypeConverter(typeof(TestKeysConverter))]
	public enum TestKeys { None = 0, Control = 1, Shift = 2, O = 4 }

	class TestKeysConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) => sourceType == typeof(string);

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
			=> ((string)value).Split('+').Aggregate(TestKeys.None, (keys, part) => keys | (TestKeys)Enum.Parse(typeof(TestKeys), part));
	}

	[ContentProperty("Children")]
	[RuntimeNameProperty("Id")]
	public class TestPanel
	{
		public string Id { get; set; }
		public string Title { get; set; }
		public int Count { get; set; }
		public double Ratio { get; set; }
		public bool Flag { get; set; }
		public int? Optional { get; set; }
		public TestMode Mode { get; set; }
		public TestFlags Flags { get; set; }
		public TestKeys Keys { get; set; }
		public TimeSpan Delay { get; set; }
		public Type ItemType { get; set; }
		public TestColor Color { get; set; }
		public TestPoint Point { get; set; }
		public TestSize Size { get; set; }
		public object Value { get; set; }
		public TestItem Item { get; set; }
		public string[] Names { get; set; }
		public List<object> Children { get; } = new List<object>();
		public Dictionary<string, object> Resources { get; } = new Dictionary<string, object>();
		public List<TestRow> Rows { get; } = new List<TestRow>();

		public event EventHandler Clicked;

		public void RaiseClicked() => Clicked?.Invoke(this, EventArgs.Empty);
	}

	[RuntimeNameProperty("Name")]
	public class TestItem
	{
		public string Name { get; set; }
		public string Text { get; set; }
		public TestItem Other { get; set; }
		public TestRow Row { get; set; }
	}

	/// <summary>Converted to from a TestItem by an internal converter, like Eto's TableRow.</summary>
	[TypeConverter(typeof(TestRowConverter))]
	public class TestRow
	{
		public TestItem Item { get; set; }
	}

	class TestRowConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) => sourceType == typeof(TestItem) || base.CanConvertFrom(context, sourceType);

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
			=> value is TestItem item ? new TestRow { Item = item } : base.ConvertFrom(context, culture, value);
	}

	/// <summary>Has an internal type converter, so generated code goes through TypeDescriptor.</summary>
	[TypeConverter(typeof(TestColorConverter))]
	public struct TestColor
	{
		public int R, G, B;

		public override string ToString() => $"{R},{G},{B}";
	}

	class TestColorConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) => sourceType == typeof(string);

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			var parts = ((string)value).Split(',');
			return new TestColor { R = int.Parse(parts[0]), G = int.Parse(parts[1]), B = int.Parse(parts[2]) };
		}
	}

	/// <summary>Has a converter and a codegen parse method, so generated code calls Parse directly.</summary>
	[TypeConverter(typeof(TestPointConverter))]
	[XamlCodeGen(nameof(Parse))]
	public struct TestPoint
	{
		public int X, Y;

		public static int ParseCount;

		public static TestPoint Parse(string value)
		{
			ParseCount++;
			var parts = value.Split(',');
			return new TestPoint { X = int.Parse(parts[0], CultureInfo.InvariantCulture), Y = int.Parse(parts[1], CultureInfo.InvariantCulture) };
		}
	}

	/// <summary>Has an internal converter, with a compiled conversion registered on the assembly.</summary>
	[TypeConverter(typeof(TestSizeConverter))]
	public struct TestSize
	{
		public int Width, Height;
	}

	class TestSizeConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) => sourceType == typeof(string);

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) => TestConverters.Size((string)value);
	}

	public static class TestConverters
	{
		public static TestSize Size(string value)
		{
			var parts = value.Split(',');
			return new TestSize { Width = int.Parse(parts[0], CultureInfo.InvariantCulture), Height = int.Parse(parts[1], CultureInfo.InvariantCulture) };
		}
	}

	public class TestPointConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) => sourceType == typeof(string);

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) => TestPoint.Parse((string)value);
	}

	public static class TestAttached
	{
		static readonly Dictionary<object, string> s_tags = new Dictionary<object, string>();

		public static string GetTag(object obj) => s_tags.TryGetValue(obj, out var tag) ? tag : null;

		public static void SetTag(object obj, string value) => s_tags[obj] = value;
	}

	public static class TestStatics
	{
		public const string Constant = "constant value";

		public static string Property => "static property";
	}

	/// <summary>Picks a value per "platform", compiled into a direct call.</summary>
	[XamlCodeGen(nameof(Provide))]
	public class TestOnExtension : MarkupExtension
	{
		public static string Platform = "Wpf";

		public TestOnExtension() { }

		public TestOnExtension(object @default) { Default = @default; }

		[ConstructorArgument("default")]
		public object Default { get; set; }
		public object Wpf { get; set; }
		public object Mac { get; set; }

		public override object ProvideValue(IServiceProvider serviceProvider)
			=> Platform == "Wpf" && Wpf != null ? Wpf : Platform == "Mac" && Mac != null ? Mac : Default;

		public static T Provide<T>(T @default = default, T wpf = default, T mac = default)
			=> Platform == "Wpf" && !Equals(wpf, default(T)) ? wpf : Platform == "Mac" && !Equals(mac, default(T)) ? mac : @default;
	}

	/// <summary>Uses XamlOptional so an explicit default value (e.g. False) still counts as set.</summary>
	[XamlCodeGen(nameof(Provide))]
	public class TestOptionalExtension : MarkupExtension
	{
		public TestOptionalExtension() { }

		public TestOptionalExtension(object @default) { Default = @default; }

		[ConstructorArgument("default")]
		public object Default { get; set; }
		public object Wpf { get; set; }

		public override object ProvideValue(IServiceProvider serviceProvider) => TestOnExtension.Platform == "Wpf" && Wpf != null ? Wpf : Default;

		public static T Provide<T>(XamlOptional<T> @default = default, XamlOptional<T> wpf = default)
			=> TestOnExtension.Platform == "Wpf" && wpf.HasValue ? wpf.Value : @default.Value;
	}

	/// <summary>No codegen method, so compiled code calls ProvideValue at runtime.</summary>
	public class DescribeExtension : MarkupExtension
	{
		public DescribeExtension() { }

		public DescribeExtension(string prefix) { Prefix = prefix; }

		[ConstructorArgument("prefix")]
		public string Prefix { get; set; }

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var target = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
			var root = (IRootObjectProvider)serviceProvider.GetService(typeof(IRootObjectProvider));
			var property = target?.TargetProperty as System.Reflection.PropertyInfo;
			return $"{Prefix}:{target?.TargetObject?.GetType().Name}.{property?.Name}:{root?.RootObject?.GetType().Name}";
		}
	}

	/// <summary>Returns null, like Eto's binding when the property has no value yet.</summary>
	[MarkupExtensionReturnType(typeof(object))]
	public class NothingExtension : MarkupExtension
	{
		public override object ProvideValue(IServiceProvider serviceProvider) => null;
	}
}
