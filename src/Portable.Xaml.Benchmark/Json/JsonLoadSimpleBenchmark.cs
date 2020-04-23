using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Portable.Xaml.Benchmark.Json
{
	public class JsonLoadSimpleBenchmark : LoadBenchmark
	{
		public override string TestName => "JsonLoadSimpleBenchmark.json";
	}
	
}
