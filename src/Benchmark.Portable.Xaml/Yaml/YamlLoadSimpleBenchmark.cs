using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Benchmark.Portable.Xaml.Yaml
{
	public class YamlLoadSimpleBenchmark : LoadBenchmark
	{
		public override string TestName => "YamlLoadSimpleBenchmark.yaml";
	}
	
}
