using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Validators;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Environments;

namespace Benchmark.Portable.Xaml
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			/**  Uncomment to test using performance profiler */
			if (args?.FirstOrDefault() == "profile")
			{
				//var benchmark = new Json.JsonLoadComplexBenchmark();
				var benchmark = new Json.JsonSaveComplexBenchmark();
				//var benchmark = new Xml.XmlLoadSimpleBenchmark();
				//var benchmark = new Xml.XmlLoadComplexBenchmark();
				//var benchmark = new Xml.XmlSaveSimpleBenchmark();
				//var benchmark = new Xml.XmlSaveComplexBenchmark();
				for (int i = 0; i < 1000; i++)
				{
					benchmark.PortableXaml();
					//benchmark.PortableXamlNoCache();
					//benchmark.SystemXaml();
					//benchmark.SystemXamlNoCache();
				}
				return;
			}
			/**/

			// BenchmarkSwitcher doesn't automatically exclude abstract benchmark classes
			var types = typeof(MainClass)
				.Assembly
				.GetExportedTypes()
				.Where(r => typeof(IXamlBenchmark).IsAssignableFrom(r) && !r.IsAbstract)
				.OrderBy(r => r.Name);

			var job = Job.ShortRun;
			//var job = Job.Default;

			var config = new ManualConfig();

			config.AddLogger(DefaultConfig.Instance.GetLoggers().ToArray());
        	config.AddExporter(DefaultConfig.Instance.GetExporters().ToArray());
        	config.AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray());

			config.AddValidator(JitOptimizationsValidator.DontFailOnError);

			//config.AddJob(Job.Default);
			config.AddJob(job.WithRuntime(CoreRuntime.Core31));
			config.AddJob(job.WithRuntime(MonoRuntime.Default));
			config.AddJob(job.WithRuntime(ClrRuntime.Net48));

			config.AddDiagnoser(MemoryDiagnoser.Default);

			config.AddColumn(StatisticColumn.OperationsPerSecond);
			config.AddColumn(RankColumn.Arabic);

			var switcher = new BenchmarkSwitcher(types.ToArray());
			switcher.Run(args, config);
		}
	}
}
