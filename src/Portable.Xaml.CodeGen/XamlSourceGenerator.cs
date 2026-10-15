using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Portable.Xaml.CodeGen
{
	/// <summary>A XAML file picked up by a <see cref="XamlSourceGenerator"/>.</summary>
	public sealed class XamlSourceFile
	{
		public XamlSourceFile(AdditionalText file, string text, XamlCodeGenOptions options)
		{
			File = file;
			Text = text;
			Options = options;
		}

		public AdditionalText File { get; }

		public string Text { get; }

		public XamlCodeGenOptions Options { get; }

		/// <summary>The result, once generated.</summary>
		public XamlCodeGenResult Result { get; internal set; }
	}

	/// <summary>
	/// Base for a source generator that compiles XAML additional files.
	/// </summary>
	/// <remarks>
	/// By default a file is compiled when its AdditionalFiles item has <c>XamlCodeGen="true"</c>
	/// (exposed with <c>CompilerVisibleItemMetadata</c>). Subclass and add <c>[Generator]</c> to use it.
	/// </remarks>
	public abstract class XamlSourceGenerator : IIncrementalGenerator
	{
		/// <summary>Prefix for diagnostic IDs, e.g. "PXAML".</summary>
		protected virtual string DiagnosticPrefix => "PXAML";

		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			var files = context.AdditionalTextsProvider
				.Combine(context.AnalyzerConfigOptionsProvider)
				.Where(pair => ShouldGenerate(pair.Left, pair.Right.GetOptions(pair.Left), pair.Right.GlobalOptions))
				.Select((pair, token) =>
				{
					var options = new XamlCodeGenOptions { FilePath = pair.Left.Path };
					ConfigureOptions(options, pair.Left, pair.Right.GetOptions(pair.Left), pair.Right.GlobalOptions);
					return new XamlSourceFile(pair.Left, pair.Left.GetText(token)?.ToString(), options);
				});

			context.RegisterSourceOutput(files.Collect().Combine(context.CompilationProvider), (spc, data) => Execute(spc, data.Right, data.Left));
		}

		protected virtual bool ShouldGenerate(AdditionalText file, AnalyzerConfigOptions fileOptions, AnalyzerConfigOptions globalOptions)
		{
			return fileOptions.TryGetValue("build_metadata.AdditionalFiles.XamlCodeGen", out var value) && string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Sets per-file options, e.g. default namespaces or the code-behind class.</summary>
		protected virtual void ConfigureOptions(XamlCodeGenOptions options, AdditionalText file, AnalyzerConfigOptions fileOptions, AnalyzerConfigOptions globalOptions)
		{
			if (fileOptions.TryGetValue("build_metadata.AdditionalFiles.XamlClass", out var className) && !string.IsNullOrEmpty(className))
				options.RootClassName = className;
		}

		protected virtual XamlCodeGenerator CreateCodeGenerator(Compilation compilation) => new XamlCodeGenerator(compilation);

		/// <summary>Called after all files are generated, e.g. to add registration code.</summary>
		protected virtual void AfterGenerate(SourceProductionContext context, Compilation compilation, IReadOnlyList<XamlSourceFile> files)
		{
		}

		void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<XamlSourceFile> files)
		{
			if (files.IsDefaultOrEmpty)
				return;
			var generator = CreateCodeGenerator(compilation);
			var hintNames = new HashSet<string>();
			foreach (var file in files)
			{
				context.CancellationToken.ThrowIfCancellationRequested();
				if (file.Text == null)
					continue;
				var result = file.Result = generator.Generate(file.Text, file.Options);
				foreach (var diagnostic in result.Diagnostics)
					context.ReportDiagnostic(CreateDiagnostic(file, diagnostic));
				if (result.Success)
				{
					var baseName = result.RootClass.ToDisplayString().Replace('<', '[').Replace('>', ']');
					var hintName = baseName;
					for (int i = 1; !hintNames.Add(hintName); i++)
						hintName = baseName + i;
					context.AddSource(hintName + ".xaml.g.cs", SourceText.From(result.Source, Encoding.UTF8));
				}
			}
			AfterGenerate(context, compilation, files);
		}

		Diagnostic CreateDiagnostic(XamlSourceFile file, XamlCodeGenDiagnostic diagnostic)
		{
			var isError = diagnostic.Severity == DiagnosticSeverity.Error;
			var descriptor = new DiagnosticDescriptor(
				DiagnosticPrefix + (isError ? "001" : "002"),
				isError ? "XAML could not be compiled" : "XAML compile warning",
				"{0}",
				"XamlCodeGen",
				diagnostic.Severity,
				isEnabledByDefault: true);

			var location = Location.None;
			if (diagnostic.LineInfo.HasLineInfo)
			{
				var position = new LinePosition(diagnostic.LineInfo.Line - 1, System.Math.Max(0, diagnostic.LineInfo.Column - 1));
				location = Location.Create(file.File.Path, new TextSpan(0, 0), new LinePositionSpan(position, position));
			}
			else
				location = Location.Create(file.File.Path, new TextSpan(0, 0), new LinePositionSpan(default, default));
			return Diagnostic.Create(descriptor, location, diagnostic.Message);
		}
	}
}
