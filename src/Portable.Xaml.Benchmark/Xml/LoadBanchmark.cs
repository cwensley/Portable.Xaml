using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace Portable.Xaml.Benchmark.Xml
{
	public abstract class LoadBenchmark : IXamlBenchmark
	{
		public abstract string TestName { get; }

		protected Stream GetStream() => typeof(IXamlBenchmark).Assembly.GetManifestResourceStream("Portable.Xaml.Benchmark.Xml." + TestName);

		Portable.Xaml.XamlSchemaContext pxc;
		[Benchmark(Baseline = true)]
		public void PortableXaml()
		{
			pxc = pxc ?? new XamlSchemaContext();
			using (var stream = GetStream())
				Portable.Xaml.XamlServices.Load(new Portable.Xaml.XamlXmlReader(stream, pxc));
		}

		[Benchmark]
		public void PortableXamlNoCache()
		{
			using (var stream = GetStream())
				Portable.Xaml.XamlServices.Load(stream);
		}

#if NETFRAMEWORK
		System.Xaml.XamlSchemaContext sxc;
#endif

		[Benchmark]
		public void SystemXaml()
		{
#if NETFRAMEWORK
			sxc = sxc ?? new System.Xaml.XamlSchemaContext();
			using (var stream = GetStream())
				System.Xaml.XamlServices.Load(new System.Xaml.XamlXmlReader(stream, sxc));
#else
			throw new NotImplementedException();
#endif
		}

		[Benchmark]
		public void SystemXamlNoCache()
		{
#if NETFRAMEWORK
			using (var stream = GetStream())
				System.Xaml.XamlServices.Load(stream);
#else
			throw new NotImplementedException();
#endif
		}

	}
}
