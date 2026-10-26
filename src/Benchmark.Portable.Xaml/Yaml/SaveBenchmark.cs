using System;
using BenchmarkDotNet.Attributes;
using System.IO;
using Portable.Xaml;
using Portable.Xaml.Yaml;
using YamlDotNet.Serialization;

namespace Benchmark.Portable.Xaml.Yaml
{
	public abstract class SaveBenchmark : IXamlBenchmark
	{
		public abstract object Instance { get; }

		XamlSchemaContext pxc;
		[Benchmark(Baseline = true)]
		public void PortableXaml()
		{
			pxc = pxc ?? (pxc = new XamlSchemaContext());
			using (var writer = new StringWriter())
			using (var yamlWriter = new XamlYamlWriter(writer, pxc))
			{
				XamlServices.Save(yamlWriter, Instance);
				var result = writer.ToString();
			}
		}

		ISerializer serializer;
		[Benchmark]
		public void YamlDotNet()
		{
			serializer = serializer ?? (serializer = new SerializerBuilder().Build());
			var result = serializer.Serialize(Instance);
		}
	}
}
