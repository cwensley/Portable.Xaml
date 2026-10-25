using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace Portable.Xaml.CodeGen.Tests
{
	[Generator]
	public class TestXamlSourceGenerator : XamlSourceGenerator
	{
	}

	[TestFixture]
	public class XamlSourceGeneratorTests
	{
		class InMemoryText : AdditionalText
		{
			readonly string _text;

			public InMemoryText(string path, string text)
			{
				Path = path;
				_text = text;
			}

			public override string Path { get; }

			public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(_text);
		}

		class Options : AnalyzerConfigOptions
		{
			readonly Dictionary<string, string> _values;

			public Options(Dictionary<string, string> values) => _values = values;

			public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value);
		}

		class OptionsProvider : AnalyzerConfigOptionsProvider
		{
			readonly Dictionary<string, Dictionary<string, string>> _files;

			public OptionsProvider(Dictionary<string, Dictionary<string, string>> files) => _files = files;

			public override AnalyzerConfigOptions GlobalOptions => new Options(new Dictionary<string, string>());

			public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

			public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
				=> new Options(_files.TryGetValue(textFile.Path, out var values) ? values : new Dictionary<string, string>());
		}

		const string Xaml = @"<TestPanel xmlns=""urn:codegen-test"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" x:Class=""Gen.Root"" Title=""generated"" />";

		static GeneratorDriverRunResult Run(string code, params (string Path, string Text, bool Enabled)[] files)
		{
			var compilation = CodeGenHelper.CreateCompilation(code);
			var options = files.ToDictionary(f => f.Path, f => f.Enabled
				? new Dictionary<string, string> { ["build_metadata.AdditionalFiles.XamlCodeGen"] = "true" }
				: new Dictionary<string, string>());
			GeneratorDriver driver = CSharpGeneratorDriver.Create(
				new[] { new TestXamlSourceGenerator().AsSourceGenerator() },
				files.Select(f => (AdditionalText)new InMemoryText(f.Path, f.Text)),
				(CSharpParseOptions)compilation.SyntaxTrees.First().Options,
				new OptionsProvider(options));
			driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
			var errors = output.GetDiagnostics().Where(r => r.Severity == DiagnosticSeverity.Error).ToList();
			Assert.IsEmpty(errors, string.Join("\n", errors));
			return driver.GetRunResult();
		}

		[Test]
		public void EnabledFilesAreGenerated()
		{
			var result = Run(CodeGenHelper.RootCode, (@"C:\test\Root.xaml", Xaml, true));
			var source = result.GeneratedTrees.Single();
			StringAssert.EndsWith("Gen.Root.xaml.g.cs", source.FilePath);
			StringAssert.Contains("this.Title = \"generated\";", source.ToString());
		}

		[Test]
		public void FilesWithoutMetadataAreSkipped()
		{
			var result = Run(CodeGenHelper.RootCode, (@"C:\test\Root.xaml", Xaml, false));
			Assert.IsEmpty(result.GeneratedTrees);
		}

		[Test]
		public void ErrorsAreReportedAtTheXamlLocation()
		{
			var compilation = CodeGenHelper.CreateCompilation(CodeGenHelper.RootCode);
			var file = new InMemoryText(@"C:\test\Root.xaml", Xaml.Replace("Title=\"generated\"", "\n Count=\"x\""));
			var options = new OptionsProvider(new Dictionary<string, Dictionary<string, string>>
			{
				[file.Path] = new Dictionary<string, string> { ["build_metadata.AdditionalFiles.XamlCodeGen"] = "true" }
			});
			var driver = CSharpGeneratorDriver.Create(new[] { new TestXamlSourceGenerator().AsSourceGenerator() }, new[] { file }, null, options);
			var diagnostic = driver.RunGenerators(compilation).GetRunResult().Diagnostics.Single();
			Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
			Assert.AreEqual("PXAML001", diagnostic.Id);
			var span = diagnostic.Location.GetLineSpan();
			Assert.AreEqual(file.Path, span.Path);
			Assert.AreEqual(1, span.StartLinePosition.Line);
		}
	}
}
