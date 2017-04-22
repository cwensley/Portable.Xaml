using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Xaml.Benchmark
{
	public class LoadXamlVsJsonBenchmark : IXamlBenchmark
	{
		Json.JsonLoadComplexBenchmark json = new Json.JsonLoadComplexBenchmark();
		Xml.XmlLoadComplexBenchmark xml = new Xml.XmlLoadComplexBenchmark();

		[Benchmark(Baseline = true)]
		public void PortableXamlXml() => xml.PortableXaml();
		[Benchmark]
		public void PortableXamlJson() => json.PortableXaml();
	}
}
