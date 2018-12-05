using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[assembly: Portable.Xaml.Markup.XmlnsDefinition ("http://example.com/benchmark", "Portable.Xaml.Benchmark")]

[assembly: System.Windows.Markup.XmlnsDefinition ("http://example.com/benchmark", "Portable.Xaml.Benchmark")]

namespace Portable.Xaml.Benchmark
{
	[Portable.Xaml.Markup.ContentProperty ("Children")]
	[System.Windows.Markup.ContentProperty ("Children")]
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

}
