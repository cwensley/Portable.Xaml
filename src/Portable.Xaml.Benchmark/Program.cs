using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Running;

namespace Portable.Xaml.Benchmark
{
	class Config : ManualConfig
	{
		public Config()
		{
			/* doesn't work yet, still runs twice as of BenchmarkDotNet 0.10.1
			Add(Job.Dry
				.With(RunStrategy.ColdStart)
				.WithLaunchCount(4)
				.WithId("ColdStart")
				);
			*/
			
			Add(Job.Default);
		}
	}

	class MainClass
	{
		public static void Main(string[] args)
		{
			/**  Uncomment to test using performance profiler *
			
			//var benchmark = new LoadSimpleBenchmark();
			var benchmark = new LoadComplexBenchmark();
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
			var types = typeof(MainClass)
				.Assembly
				.GetExportedTypes()
				.Where(r => typeof(IXamlBenchmark).IsAssignableFrom(r) && !r.IsAbstract);

			var switcher = new BenchmarkSwitcher(types.ToArray());
			switcher.Run(args);
		}
	}
}
