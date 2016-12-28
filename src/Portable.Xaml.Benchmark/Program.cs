using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Portable.Xaml.Benchmark
{
	class MainClass
	{
		public static void Main (string [] args)
		{
			/**  Uncomment to test using performance profiler *
			
			var benchmark = new LoadSimpleBenchmark();
			//var benchmark = new LoadComplexBenchmark();
			//var benchmark = new SaveSimpleBenchmark();
			//var benchmark = new SaveComplexBenchmark();
			for (int i = 0; i < 10000; i++)
			{
				benchmark.PortableXaml();
				//b.SystemXaml();
			}
			return;
			/**/
			
			// BenchmarkSwitcher doesn't automatically exclude abstract benchmark classes
			var types = typeof (MainClass)
				.Assembly
				.GetExportedTypes ()
				.Where (r => typeof (XamlBenchmark).IsAssignableFrom (r) && !r.IsAbstract);
			
			var switcher = new BenchmarkDotNet.Running.BenchmarkSwitcher (types.ToArray());
			switcher.Run (args);
		}
	}
}
