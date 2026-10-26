using System;
using BenchmarkDotNet.Attributes;
using System.IO;
using Portable.Xaml;
using Portable.Xaml.Yaml;
using YamlDotNet.Serialization;

namespace Benchmark.Portable.Xaml.Yaml
{
	public abstract class LoadBenchmark : IXamlBenchmark
	{
		public abstract string TestName { get; }

		protected Stream GetStream() => typeof(IXamlBenchmark).Assembly.GetManifestResourceStream("Benchmark.Portable.Xaml.Yaml." + TestName);

		XamlSchemaContext pxc;
		[Benchmark(Baseline = true)]
		public void PortableXaml()
		{
			pxc = pxc ?? (pxc = new XamlSchemaContext());
			using (var stream = GetStream())
				XamlServices.Load(new XamlYamlReader(stream, pxc));
		}

		// YamlDotNet can't fill a read only collection, so it loads the same YAML without types like Newtonsoft.Json does in the JSON benchmark
		IDeserializer deserializer;
		[Benchmark]
		public void YamlDotNet()
		{
			deserializer = deserializer ?? (deserializer = new DeserializerBuilder().Build());
			using (var stream = GetStream())
			using (var reader = new StreamReader(stream))
				deserializer.Deserialize<object>(reader);
		}
	}
}
