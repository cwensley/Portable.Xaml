using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Portable.Xaml;

namespace Benchmark.Portable.Xaml.Xml
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
				XamlServices.Save(new XamlXmlWriter(writer, pxc), Instance);
		}

		[Benchmark]
		public void PortableXamlNoCache()
		{
			using (var writer = new StringWriter())
				XamlServices.Save(writer, Instance);
		}

#if NETFRAMEWORK
		System.Xaml.XamlSchemaContext sxc;
		[Benchmark]
		public void SystemXaml()
		{
			sxc = sxc ?? (sxc = new System.Xaml.XamlSchemaContext());
			using (var writer = new StringWriter())
				System.Xaml.XamlServices.Save(new System.Xaml.XamlXmlWriter(writer, sxc), Instance);
		}

		[Benchmark]
		public void SystemXamlNoCache()
		{
			using (var writer = new StringWriter())
				System.Xaml.XamlServices.Save(writer, Instance);

		}
#else
		[Benchmark]
		public void SystemXaml() => throw new NotImplementedException();
		
		[Benchmark]
		public void SystemXamlNoCache() => throw new NotImplementedException();
#endif
	}
}
