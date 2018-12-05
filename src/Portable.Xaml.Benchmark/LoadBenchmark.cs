using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace Portable.Xaml.Benchmark
{
	public abstract class LoadBenchmark : IXamlBenchmark
	{
		public abstract string TestName { get; }

		protected Stream GetStream() => typeof(IXamlBenchmark).Assembly.GetManifestResourceStream("Portable.Xaml.Benchmark." + TestName);

		Portable.Xaml.XamlSchemaContext pxc;
		[Benchmark(Baseline = true)]
		public void PortableXaml()
		{
			pxc = pxc ?? new XamlSchemaContext();
			using (var stream = GetStream())
				Portable.Xaml.XamlServices.Load(new Portable.Xaml.XamlXmlReader(stream, pxc));
		}

		System.Xaml.XamlSchemaContext sxc;
		[Benchmark]
		public void SystemXaml()
		{
			sxc = sxc ?? new System.Xaml.XamlSchemaContext();
			using (var stream = GetStream())
				System.Xaml.XamlServices.Load(new System.Xaml.XamlXmlReader(stream, sxc));
		}

		[Benchmark]
		public void PortableXamlNoCache()
		{
			using (var stream = GetStream())
				Portable.Xaml.XamlServices.Load(stream);
		}

		[Benchmark]
		public void SystemXamlNoCache()
		{
			using (var stream = GetStream())
				System.Xaml.XamlServices.Load(stream);
		}
	}
}
