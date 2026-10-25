using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Portable.Xaml.CodeGen.Tests.Types;

namespace Portable.Xaml.CodeGen.Tests
{
	public class CompiledXaml
	{
		public XamlCodeGenResult Result { get; set; }
		public Assembly Assembly { get; set; }
		public string RootClassName { get; set; }

		public object CreateGenerated()
		{
			var type = Assembly.GetType(RootClassName, true);
			var instance = Activator.CreateInstance(type);
			type.GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(instance, null);
			return instance;
		}

		/// <summary>Loads the same XAML with the runtime object writer, for comparison.</summary>
		public object CreateRuntime(string xaml)
		{
			var type = Assembly.GetType(RootClassName, true);
			var instance = Activator.CreateInstance(type);
			var context = new XamlSchemaContext(new[] { Assembly, typeof(TestPanel).Assembly, typeof(XamlLanguage).Assembly, typeof(object).Assembly });
			var reader = new XamlXmlReader(new StringReader(xaml), context, new XamlXmlReaderSettings { LocalAssembly = Assembly });
			var writer = new XamlObjectWriter(context, new XamlObjectWriterSettings { RootObjectInstance = instance });
			XamlServices.Transform(reader, writer);
			return instance;
		}
	}

	public static class CodeGenHelper
	{
		static List<MetadataReference> s_references;
		static int s_count;

		public const string FilePath = @"C:\test\Test.xaml";

		static List<MetadataReference> References
		{
			get
			{
				if (s_references != null)
					return s_references;
				var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).ToList();
				paths.Add(typeof(XamlLanguage).Assembly.Location);
				paths.Add(typeof(TestPanel).Assembly.Location);
				s_references = paths.Distinct().Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
				return s_references;
			}
		}

		public static CSharpCompilation CreateCompilation(string code, string assemblyName = null)
		{
			assemblyName = assemblyName ?? "CodeGenTest" + (++s_count);
			return CSharpCompilation.Create(assemblyName,
				new[] { CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest)) },
				References,
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		}

		/// <summary>Generates code for the XAML, without compiling it.</summary>
		public static XamlCodeGenResult Generate(string code, string xaml, XamlCodeGenOptions options = null)
		{
			var compilation = CreateCompilation(code);
			return new XamlCodeGenerator(compilation).Generate(xaml, options ?? new XamlCodeGenOptions { FilePath = FilePath });
		}

		/// <summary>Generates code for the XAML, compiles it with the code-behind and loads the assembly.</summary>
		public static CompiledXaml Compile(string code, string xaml, string rootClassName = "Gen.Root", XamlCodeGenOptions options = null, Func<Compilation, XamlCodeGenerator> createGenerator = null)
		{
			var compilation = CreateCompilation(code);
			var generator = createGenerator?.Invoke(compilation) ?? new XamlCodeGenerator(compilation);
			var result = generator.Generate(xaml, options ?? new XamlCodeGenOptions { FilePath = FilePath });
			var errors = result.Diagnostics.Where(r => r.Severity == DiagnosticSeverity.Error).ToList();
			if (!result.Success || errors.Count > 0)
				Assert.Fail("Generation failed:\n" + string.Join("\n", result.Diagnostics));

			compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(result.Source, new CSharpParseOptions(LanguageVersion.Latest), "Generated.cs"));
			using (var stream = new MemoryStream())
			{
				var emit = compilation.Emit(stream);
				if (!emit.Success)
				{
					Assert.Fail("Compile failed:\n" + string.Join("\n", emit.Diagnostics.Where(r => r.Severity == DiagnosticSeverity.Error)) + "\n\n" + result.Source);
				}
				return new CompiledXaml
				{
					Result = result,
					Assembly = Assembly.Load(stream.ToArray()),
					RootClassName = rootClassName
				};
			}
		}

		/// <summary>Compiles generated code and returns the compiler's errors.</summary>
		public static IList<Diagnostic> CompileErrors(string code, string xaml)
		{
			var compilation = CreateCompilation(code);
			var result = new XamlCodeGenerator(compilation).Generate(xaml, new XamlCodeGenOptions { FilePath = FilePath });
			Assert.IsTrue(result.Success, string.Join("\n", result.Diagnostics));
			compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(result.Source, new CSharpParseOptions(LanguageVersion.Latest), "Generated.cs"));
			return compilation.GetDiagnostics().Where(r => r.Severity == DiagnosticSeverity.Error).ToList();
		}

		public const string RootCode = @"
namespace Gen
{
	public partial class Root : Portable.Xaml.CodeGen.Tests.Types.TestPanel
	{
	}
}";
	}
}
