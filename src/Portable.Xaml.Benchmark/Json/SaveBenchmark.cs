using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Portable.Xaml.Json;

namespace Portable.Xaml.Benchmark.Json
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
			using (var jsonWriter = new XamlJsonWriter(writer, pxc))
			{
				XamlServices.Save(jsonWriter, Instance);
				var result = writer.ToString();
			}
		}

		[Benchmark]
		public void NewtonsoftJson()
		{
			var result = Newtonsoft.Json.JsonConvert.SerializeObject(Instance);
		}

		/*
		[Benchmark]
		public void PortableXamlNoCache()
		{
			using (var stream = new MemoryStream())
				XamlJsonServices.Save(stream, Instance);
		}
		*/
	}
}
