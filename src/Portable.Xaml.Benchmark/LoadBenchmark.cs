using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes.Columns;

namespace Portable.Xaml.Benchmark
{
	public abstract class LoadBenchmark : XamlBenchmark
	{
		public abstract string TestName { get; }

		protected Stream GetStream() => typeof(XamlBenchmark).Assembly.GetManifestResourceStream("Portable.Xaml.Benchmark." + TestName);

		Portable.Xaml.XamlSchemaContext pxc;
		[Benchmark(Baseline = true)]
		public void PortableXaml()
		{
			pxc = pxc ?? (pxc = new XamlSchemaContext());
			using (var stream = GetStream())
				Portable.Xaml.XamlServices.Load(new Portable.Xaml.XamlXmlReader(stream, pxc));
		}

		System.Xaml.XamlSchemaContext sxc;
		[Benchmark]
		public void SystemXaml()
		{
			sxc = sxc ?? (sxc = new System.Xaml.XamlSchemaContext());
			using (var stream = GetStream())
				System.Xaml.XamlServices.Load(new System.Xaml.XamlXmlReader(stream, sxc));
		}
	}
}
