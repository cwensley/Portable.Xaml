using System;
using NUnit.Framework.Internal;
using System.Reflection;
using NUnit.Framework.Api;
using System.Collections.Generic;
using NUnit.Framework.Interfaces;
using NUnit.Framework;
using System.Diagnostics;

namespace Portable.Xaml_tests_core2
{
	public class TestListener : ITestListener
	{
		public Action<string> Log { get; set; }
		public void TestFinished(ITestResult result)
		{
			if (!result.HasChildren)
			{
				if (!string.IsNullOrEmpty(result.Output))
					Log(result.Output);
				if (result.FailCount > 0)
				{
					Log($"Failed: {result.Message}\n{result.StackTrace}");
				}
				if (result.InconclusiveCount > 0)
					Log($"Inconclusive: {result.Message}\n{result.StackTrace}");
			}
		}

		public void TestOutput(TestOutput output)
		{
		}

		public void TestStarted(ITest test)
		{
			if (!test.HasChildren)
				Log(test.FullName);
		}
	}

	class Program
    {
		static void Main(string[] args)
		{
			var builder = new DefaultTestAssemblyBuilder();
			var runner = new NUnitTestAssemblyRunner(builder);
			var settings = new Dictionary<string, object>();
			var assembly = Assembly.GetEntryAssembly();
			var messages = new List<string>();
			var listener = new TestListener
			{
				Log = message => messages.Add(message) // writing directly to console doesn't work for some reason
			};
			runner.Load(assembly, settings);
			var result = runner.Run(listener, TestFilter.Empty);

			foreach (var msg in messages)
			{
				Console.WriteLine(msg);
			}
			Console.WriteLine();
			Console.WriteLine(result.FailCount > 0 ? "FAILED" : "PASSED");
			Console.WriteLine($"Pass: {result.PassCount}, Fail: {result.FailCount}, Skipped: {result.SkipCount}, Inconclusive: {result.InconclusiveCount}");
			Console.WriteLine($"Duration: {result.Duration}");
			if (Debugger.IsAttached)
			{
				Console.Write("Press any key to continue...");
				Console.ReadKey(true);
			}
		}
    }
}
