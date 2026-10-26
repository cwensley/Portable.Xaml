using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark.Portable.Xaml
{
	public class LoadXamlVsJsonBenchmark : IXamlBenchmark
	{
		Json.JsonLoadComplexBenchmark json = new Json.JsonLoadComplexBenchmark();
		Xml.XmlLoadComplexBenchmark xml = new Xml.XmlLoadComplexBenchmark();
		Yaml.YamlLoadComplexBenchmark yaml = new Yaml.YamlLoadComplexBenchmark();

		[Benchmark(Baseline = true)]
		public void PortableXamlXml() => xml.PortableXaml();
		[Benchmark]
		public void PortableXamlJson() => json.PortableXaml();
		[Benchmark]
		public void PortableXamlYaml() => yaml.PortableXaml();
	}
}
