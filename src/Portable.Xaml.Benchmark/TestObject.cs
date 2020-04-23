using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[assembly: Portable.Xaml.Markup.XmlnsDefinition ("http://example.com/benchmark", "Portable.Xaml.Benchmark")]

#if NETFRAMEWORK
[assembly: System.Windows.Markup.XmlnsDefinition ("http://example.com/benchmark", "Portable.Xaml.Benchmark")]
#endif

namespace Portable.Xaml.Benchmark
{
	[Portable.Xaml.Markup.ContentProperty ("Children")]
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
