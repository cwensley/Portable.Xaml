using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Portable.Xaml.Benchmark
{
	public class LoadSimpleBenchmark : LoadBenchmark
	{
		public override string TestName => "LoadSimpleBenchmark.xml";
	}
	
}
