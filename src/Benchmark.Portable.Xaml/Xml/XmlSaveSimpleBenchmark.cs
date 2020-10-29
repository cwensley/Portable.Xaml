using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Benchmark.Portable.Xaml.Xml
{
	public class XmlSaveSimpleBenchmark : SaveBenchmark
	{
		public override object Instance => new TestObject { StringProperty = "Hello" };
	}
}
