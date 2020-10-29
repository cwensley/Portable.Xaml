using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Portable.Xaml.Markup;

[assembly: Portable.Xaml.Markup.XmlnsDefinition ("http://example.com/benchmark", "Benchmark.Portable.Xaml")]

#if NETFRAMEWORK
[assembly: System.Windows.Markup.XmlnsDefinition ("http://example.com/benchmark", "Benchmark.Portable.Xaml")]
#endif

namespace Benchmark.Portable.Xaml
{
	[ContentProperty ("Children")]
#if NETFRAMEWORK
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

}
