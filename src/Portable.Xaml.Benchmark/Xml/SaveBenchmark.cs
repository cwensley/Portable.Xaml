using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Portable.Xaml.Benchmark.Xml
{
	public abstract class SaveBenchmark : IXamlBenchmark
	{
		public abstract object Instance { get; }

		Portable.Xaml.XamlSchemaContext pxc;
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
				Portable.Xaml.XamlServices.Save(writer, Instance);
		}

#if NETFRAMEWORK
		System.Xaml.XamlSchemaContext sxc;
#endif
		[Benchmark]
		public void SystemXaml()
		{
#if NETFRAMEWORK
			sxc = sxc ?? (sxc = new System.Xaml.XamlSchemaContext());
			using (var writer = new StringWriter())
				System.Xaml.XamlServices.Save(new System.Xaml.XamlXmlWriter(writer, sxc), Instance);
#else
			throw new NotImplementedException();
#endif
		}

		[Benchmark]
		public void SystemXamlNoCache()
		{
#if NETFRAMEWORK
			using (var writer = new StringWriter())
				System.Xaml.XamlServices.Save(writer, Instance);
#else
			throw new NotImplementedException();
#endif
		}
	}
}
