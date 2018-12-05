using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Portable.Xaml.Benchmark
{
	public abstract class SaveBenchmark : IXamlBenchmark
	{
		public abstract object Instance { get; }

		Portable.Xaml.XamlSchemaContext pxc;
		[Benchmark(Baseline = true)]
		public void PortableXaml()
		{
			pxc = pxc ?? (pxc = new XamlSchemaContext());
			using (var stream = new MemoryStream())
				Portable.Xaml.XamlServices.Save(new Portable.Xaml.XamlXmlWriter(stream, pxc), Instance);
		}

		System.Xaml.XamlSchemaContext sxc;
		[Benchmark]
		public void SystemXaml()
		{
			sxc = sxc ?? (sxc = new System.Xaml.XamlSchemaContext());
			using (var stream = new MemoryStream())
				System.Xaml.XamlServices.Save(new System.Xaml.XamlXmlWriter(stream, sxc), Instance);
		}

		[Benchmark]
		public void PortableXamlNoCache()
		{
			using (var stream = new MemoryStream())
				Portable.Xaml.XamlServices.Save(stream, Instance);
		}

		[Benchmark]
		public void SystemXamlNoCache()
		{
			using (var stream = new MemoryStream())
				System.Xaml.XamlServices.Save(stream, Instance);
		}
	}
}
