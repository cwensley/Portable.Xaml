using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Benchmark.Portable.Xaml.Json
{
	public class JsonLoadComplexBenchmark : LoadBenchmark
	{
		public override string TestName => "JsonLoadComplexBenchmark.json";
	}
}
