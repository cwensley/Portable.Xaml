using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Benchmark.Portable.Xaml.Xml
{
	public class XmlLoadComplexBenchmark : LoadBenchmark
	{
		public override string TestName => "XmlLoadComplexBenchmark.xml";
	}
}
