using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes.Columns;

namespace Portable.Xaml.Benchmark
{
	public class SaveSimpleBenchmark : SaveBenchmark
	{
		public override object Instance => new TestObject { StringProperty = "Hello" };
	}
}
