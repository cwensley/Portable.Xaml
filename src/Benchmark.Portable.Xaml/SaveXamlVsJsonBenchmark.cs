using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark.Portable.Xaml
{
	public class SaveXamlVsJsonBenchmark : IXamlBenchmark
	{
		Json.JsonSaveComplexBenchmark json = new Json.JsonSaveComplexBenchmark();
		Xml.XmlSaveComplexBenchmark xml = new Xml.XmlSaveComplexBenchmark();
		Yaml.YamlSaveComplexBenchmark yaml = new Yaml.YamlSaveComplexBenchmark();

		[Benchmark(Baseline = true)]
		public void PortableXamlXml() => xml.PortableXaml();
		[Benchmark]
		public void PortableXamlJson() => json.PortableXaml();
		[Benchmark]
		public void PortableXamlYaml() => yaml.PortableXaml();
	}
}
