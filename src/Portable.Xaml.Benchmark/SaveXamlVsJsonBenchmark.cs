using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Xaml.Benchmark
{
	public class SaveXamlVsJsonBenchmark : IXamlBenchmark
	{
		Json.JsonSaveComplexBenchmark json = new Json.JsonSaveComplexBenchmark();
		Xml.XmlSaveComplexBenchmark xml = new Xml.XmlSaveComplexBenchmark();

		[Benchmark(Baseline = true)]
		public void PortableXamlXml() => xml.PortableXaml();
		[Benchmark]
		public void PortableXamlJson() => json.PortableXaml();
	}
}
