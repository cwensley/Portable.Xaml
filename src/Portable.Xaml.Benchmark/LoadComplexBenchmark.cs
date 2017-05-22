using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Portable.Xaml.Benchmark
{
	public class LoadComplexBenchmark : LoadBenchmark
	{
		public override string TestName => "LoadComplexBenchmark.xml";
	}
}
