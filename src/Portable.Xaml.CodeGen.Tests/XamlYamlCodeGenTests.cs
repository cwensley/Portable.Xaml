using System.IO;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Portable.Xaml.CodeGen.Tests.Types;
using Portable.Xaml.Yaml;

namespace Portable.Xaml.CodeGen.Tests
{
	/// <summary>Reads YAML instead of XML, as a generator for YAML files would.</summary>
	class YamlCodeGenerator : XamlCodeGenerator
	{
		public YamlCodeGenerator(Compilation compilation)
			: base(compilation)
		{
		}

		protected override XamlReader CreateReader(string text, XamlCodeGenOptions options)
		{
			var settings = new XamlYamlReaderSettings { ProvideLineInfo = true };
			settings.AddNamespace(null, "urn:codegen-test");
			settings.RootType = SchemaContext.GetXamlType(Compilation.GetTypeByMetadataName(options.RootClassName));
			return new XamlYamlReader(new StringReader(text), SchemaContext, settings);
		}
	}

	[TestFixture]
	public class XamlYamlCodeGenTests
	{
		const string Yaml = @"Title: Hello
Count: 42
Flag: true
Children:
  - $type: TestItem
    $name: item
    Text: '{Describe pre}'
  - null
";

		static CompiledXaml Compile() => CodeGenHelper.Compile(CodeGenHelper.RootCode, Yaml,
			options: new XamlCodeGenOptions { FilePath = @"C:\test\Test.yaml", RootClassName = "Gen.Root" },
			createGenerator: c => new YamlCodeGenerator(c));

		static TestPanel LoadRuntime(CompiledXaml compiled)
		{
			var type = compiled.Assembly.GetType(compiled.RootClassName, true);
			var instance = System.Activator.CreateInstance(type);
			var context = new XamlSchemaContext(new[] { compiled.Assembly, typeof(TestPanel).Assembly, typeof(XamlLanguage).Assembly, typeof(object).Assembly });
			var settings = new XamlYamlReaderSettings { LocalAssembly = compiled.Assembly, RootType = context.GetXamlType(type) };
			settings.AddNamespace(null, "urn:codegen-test");
			var writer = new XamlObjectWriter(context, new XamlObjectWriterSettings { RootObjectInstance = instance });
			XamlServices.Transform(new XamlYamlReader(new StringReader(Yaml), context, settings), writer);
			return (TestPanel)instance;
		}

		[Test]
		public void YamlShouldCompileLikeTheRuntimeLoader()
		{
			var compiled = Compile();
			var generated = (TestPanel)compiled.CreateGenerated();
			Assert.AreEqual("Hello", generated.Title);
			Assert.AreEqual(42, generated.Count);
			Assert.IsTrue(generated.Flag);
			Assert.AreEqual("pre:TestItem.Text:Root", ((TestItem)generated.Children[0]).Text);
			Assert.IsNull(generated.Children[1]);

			var runtime = LoadRuntime(compiled);
			Assert.AreEqual(XamlServices.Save(runtime), XamlServices.Save(generated), "Generated code should match the runtime loader");
		}

		[Test]
		public void YamlLinesShouldBeUsedForDebugging()
		{
			StringAssert.Contains(@"#line 7 ""C:\test\Test.yaml""", Compile().Result.Source);
		}
	}
}
