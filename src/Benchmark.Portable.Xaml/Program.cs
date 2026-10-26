using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Validators;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
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
			// on Windows the .NET 10 build targets net10.0-windows so it can compare against System.Xaml
			var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
			var coreJob = job.WithRuntime(CoreRuntime.Core10_0);
			if (isWindows)
				coreJob = coreJob.WithToolchain(CsProjCoreToolchain.From(new NetCoreAppSettings("net10.0-windows", null, ".NET 10.0")));
			// a net48 host may run emulated (x64 on Arm64), so don't force its architecture onto the .NET build
			config.AddJob(coreJob.WithPlatform(Platform.AnyCpu));
			// .NET Framework can only run on Windows
			if (isWindows)
				config.AddJob(job.WithRuntime(ClrRuntime.Net48));

			config.AddDiagnoser(MemoryDiagnoser.Default);

			config.AddColumn(StatisticColumn.OperationsPerSecond);
			config.AddColumn(RankColumn.Arabic);

			var switcher = new BenchmarkSwitcher(types.ToArray());
			switcher.Run(args, config);
		}
	}
}
