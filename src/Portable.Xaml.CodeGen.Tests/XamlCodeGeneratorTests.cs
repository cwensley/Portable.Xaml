using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Portable.Xaml.CodeGen.Tests.Types;

namespace Portable.Xaml.CodeGen.Tests
{
	[TestFixture]
	public class XamlCodeGeneratorTests
	{
		const string Namespaces = @"xmlns=""urn:codegen-test"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:Gen"" x:Class=""Gen.Root""";

		static string Xaml(string attributes, string content = null)
			=> content == null
				? $"<TestPanel {Namespaces} {attributes} />"
				: $"<TestPanel {Namespaces} {attributes}>{content}</TestPanel>";

		static TestPanel Load(string xaml, string code = CodeGenHelper.RootCode) => (TestPanel)CodeGenHelper.Compile(code, xaml).CreateGenerated();

		/// <summary>Loads XAML both ways and checks the results serialize the same.</summary>
		static TestPanel LoadAndCompare(string xaml, string code = CodeGenHelper.RootCode)
		{
			var compiled = CodeGenHelper.Compile(code, xaml);
			var generated = compiled.CreateGenerated();
			var runtime = compiled.CreateRuntime(xaml);
			Assert.AreEqual(XamlServices.Save(runtime), XamlServices.Save(generated), "Generated code should match the runtime loader");
			return (TestPanel)generated;
		}

		[Test]
		public void PrimitivePropertiesAreSet()
		{
			var panel = LoadAndCompare(Xaml(@"Title=""Hello"" Count=""42"" Ratio=""1.5"" Flag=""True"" Optional=""7"" Mode=""Second"" Flags=""A, C"" Delay=""00:00:05"""));
			Assert.AreEqual("Hello", panel.Title);
			Assert.AreEqual(42, panel.Count);
			Assert.AreEqual(1.5, panel.Ratio);
			Assert.IsTrue(panel.Flag);
			Assert.AreEqual(7, panel.Optional);
			Assert.AreEqual(TestMode.Second, panel.Mode);
			Assert.AreEqual(TestFlags.A | TestFlags.C, panel.Flags);
			Assert.AreEqual(TimeSpan.FromSeconds(5), panel.Delay);
		}

		[Test]
		public void NegativeAndInvariantNumbersParse()
		{
			var panel = Load(Xaml(@"Count=""-3"" Ratio=""-0.25"""));
			Assert.AreEqual(-3, panel.Count);
			Assert.AreEqual(-0.25, panel.Ratio);
		}

		[Test]
		public void InvalidNumberIsReportedAtItsLine()
		{
			var result = CodeGenHelper.Generate(CodeGenHelper.RootCode, Xaml("\n  Count=\"abc\""));
			Assert.IsFalse(result.Success);
			var error = result.Diagnostics.Single(r => r.Severity == DiagnosticSeverity.Error);
			StringAssert.Contains("abc", error.Message);
			Assert.AreEqual(2, error.LineInfo.Line);
		}

		[Test]
		public void TypePropertyAcceptsTypeNameAndXType()
		{
			Assert.AreEqual(typeof(TestItem), Load(Xaml(@"ItemType=""TestItem""")).ItemType);
			Assert.AreEqual(typeof(TestItem), Load(Xaml(@"ItemType=""{x:Type TestItem}""")).ItemType);
		}

		[Test]
		public void EnumTypeConverterWinsOverEnumParsing()
		{
			Assert.AreEqual(TestKeys.Control | TestKeys.O, Load(Xaml(@"Keys=""Control+O""")).Keys);
		}

		[Test]
		public void InternalTypeConverterIsUsedThroughTypeDescriptor()
		{
			var compiled = CodeGenHelper.Compile(CodeGenHelper.RootCode, Xaml(@"Color=""1,2,3"""));
			StringAssert.Contains("TypeDescriptor.GetConverter", compiled.Result.Source);
			var panel = (TestPanel)compiled.CreateGenerated();
			Assert.AreEqual(new TestColor { R = 1, G = 2, B = 3 }, panel.Color);
		}

		[Test]
		public void CodeGenParseMethodIsCalledInsteadOfConverter()
		{
			var compiled = CodeGenHelper.Compile(CodeGenHelper.RootCode, Xaml(@"Point=""10,20"""));
			StringAssert.Contains("TestPoint.Parse(\"10,20\")", compiled.Result.Source);
			var panel = (TestPanel)compiled.CreateGenerated();
			Assert.AreEqual(10, panel.Point.X);
			Assert.AreEqual(20, panel.Point.Y);
		}

		[Test]
		public void AssemblyCodeGenMethodIsCalledForOtherTypes()
		{
			var compiled = CodeGenHelper.Compile(CodeGenHelper.RootCode, Xaml(@"Size=""3,4"""));
			StringAssert.Contains("TestConverters.Size(\"3,4\")", compiled.Result.Source);
			StringAssert.DoesNotContain("TypeDescriptor", compiled.Result.Source);
			var panel = (TestPanel)compiled.CreateGenerated();
			Assert.AreEqual(3, panel.Size.Width);
			Assert.AreEqual(4, panel.Size.Height);
		}

		[Test]
		public void ContentAndNestedObjectsAreCreated()
		{
			var panel = LoadAndCompare(Xaml("", @"
				<TestItem Text=""one"" />
				<TestItem Text=""two""><TestItem.Other><TestItem Text=""inner"" /></TestItem.Other></TestItem>
				<x:String>three</x:String>"));
			Assert.AreEqual(3, panel.Children.Count);
			Assert.AreEqual("one", ((TestItem)panel.Children[0]).Text);
			Assert.AreEqual("inner", ((TestItem)panel.Children[1]).Other.Text);
			Assert.AreEqual("three", panel.Children[2]);
		}

		[Test]
		public void PropertyElementSetsObject()
		{
			var panel = LoadAndCompare(Xaml("", @"<TestPanel.Item><TestItem Text=""item"" /></TestPanel.Item>"));
			Assert.AreEqual("item", panel.Item.Text);
		}

		[Test]
		public void CollectionItemsAreConvertedWithTypeConverter()
		{
			var panel = LoadAndCompare(Xaml("", @"<TestPanel.Rows><TestItem Text=""a"" /><TestItem Text=""b"" /></TestPanel.Rows>"));
			Assert.AreEqual(2, panel.Rows.Count);
			Assert.AreEqual("b", panel.Rows[1].Item.Text);
		}

		[Test]
		public void DictionaryItemsUseKeys()
		{
			var panel = LoadAndCompare(Xaml("", @"<TestPanel.Resources><TestItem x:Key=""first"" Text=""1"" /><x:String x:Key=""second"">2</x:String></TestPanel.Resources>"));
			Assert.AreEqual("1", ((TestItem)panel.Resources["first"]).Text);
			Assert.AreEqual("2", panel.Resources["second"]);
		}

		[Test]
		public void NamesSetCodeBehindMembersAndAliasProperty()
		{
			const string code = @"
namespace Gen
{
	public partial class Root : Portable.Xaml.CodeGen.Tests.Types.TestPanel
	{
		public Portable.Xaml.CodeGen.Tests.Types.TestItem First { get; set; }
		protected Portable.Xaml.CodeGen.Tests.Types.TestItem second;
		public Portable.Xaml.CodeGen.Tests.Types.TestItem Second => second;
	}
}";
			var panel = Load(Xaml(@"x:Name=""rootName""", @"<TestItem x:Name=""First"" Text=""1"" /><TestItem Name=""second"" Text=""2"" />"), code);
			dynamic root = panel;
			Assert.AreEqual("1", (string)root.First.Text);
			Assert.AreEqual("First", (string)root.First.Name, "x:Name should set the RuntimeNameProperty");
			Assert.AreEqual("2", (string)root.Second.Text, "Setting the alias property also names the object");
			Assert.AreEqual("rootName", panel.Id);
		}

		[Test]
		public void FieldsAreGeneratedForUnknownNames()
		{
			var compiled = CodeGenHelper.Compile(CodeGenHelper.RootCode, Xaml("", @"<TestItem x:Name=""item"" /><TestItem x:Name=""publicItem"" x:FieldModifier=""public"" />"));
			var type = compiled.Assembly.GetType("Gen.Root");
			var field = type.GetField("item", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			Assert.IsNotNull(field);
			Assert.IsTrue(field.IsAssembly);
			Assert.IsTrue(type.GetField("publicItem").IsPublic);
			var panel = compiled.CreateGenerated();
			Assert.AreSame(((TestPanel)panel).Children[0], field.GetValue(panel));
		}

		[Test]
		public void EventsAreWiredToCodeBehindMethods()
		{
			const string code = @"
namespace Gen
{
	public partial class Root : Portable.Xaml.CodeGen.Tests.Types.TestPanel
	{
		public int Count2;
		void HandleClicked(object sender, System.EventArgs e) => Count2++;
	}
}";
			var compiled = CodeGenHelper.Compile(code, Xaml(@"Clicked=""HandleClicked"""));
			StringAssert.Contains("Clicked += this.HandleClicked", compiled.Result.Source);
			dynamic panel = compiled.CreateGenerated();
			panel.RaiseClicked();
			Assert.AreEqual(1, (int)panel.Count2);
		}

		[Test]
		public void MissingEventHandlerIsAnError()
		{
			var result = CodeGenHelper.Generate(CodeGenHelper.RootCode, Xaml(@"Clicked=""Nope"""));
			Assert.IsFalse(result.Success);
			StringAssert.Contains("'Nope' was not found", result.Diagnostics.Single().Message);
		}

		[Test]
		public void WrongHandlerSignatureMapsToXamlLine()
		{
			const string code = @"
namespace Gen
{
	public partial class Root : Portable.Xaml.CodeGen.Tests.Types.TestPanel
	{
		void HandleClicked(int value) { }
	}
}";
			var errors = CodeGenHelper.CompileErrors(code, Xaml("\n\n  Clicked=\"HandleClicked\""));
			Assert.IsNotEmpty(errors);
			var span = errors[0].Location.GetMappedLineSpan();
			Assert.AreEqual(CodeGenHelper.FilePath, span.Path);
			Assert.AreEqual(2, span.StartLinePosition.Line, "zero-based line of the Clicked attribute");
		}

		[Test]
		public void AttachedPropertiesCallSetter()
		{
			var panel = Load(Xaml(@"TestAttached.Tag=""root""", @"<TestItem TestAttached.Tag=""child"" />"));
			Assert.AreEqual("root", TestAttached.GetTag(panel));
			Assert.AreEqual("child", TestAttached.GetTag(panel.Children[0]));
		}

		[Test]
		public void BuiltInMarkupExtensions()
		{
			var panel = LoadAndCompare(Xaml(@"Title=""{x:Static TestStatics.Constant}"" Value=""{x:Null}""",
				@"<TestItem x:Name=""a"" Text=""a"" /><TestItem Text=""b"" Other=""{x:Reference a}"" />
				<TestPanel.Names><x:Array Type=""x:String""><x:String>one</x:String><x:String>two</x:String></x:Array></TestPanel.Names>"));
			Assert.AreEqual(TestStatics.Constant, panel.Title);
			Assert.IsNull(panel.Value);
			Assert.AreSame(panel.Children[0], ((TestItem)panel.Children[1]).Other);
			CollectionAssert.AreEqual(new[] { "one", "two" }, panel.Names);
		}

		[Test]
		public void StaticPropertyMarkupExtension()
		{
			Assert.AreEqual(TestStatics.Property, Load(Xaml(@"Title=""{x:Static TestStatics.Property}""")).Title);
		}

		[Test]
		public void CodeGenMarkupExtensionIsATypedCall()
		{
			var compiled = CodeGenHelper.Compile(CodeGenHelper.RootCode, Xaml(@"Count=""{TestOn 1, Wpf=2, Mac=3}"" Title=""{TestOn Default=a, Mac=b}"""));
			StringAssert.Contains("TestOnExtension.Provide<int>(", compiled.Result.Source);
			StringAssert.DoesNotContain("ProvideValue", compiled.Result.Source);
			var panel = (TestPanel)compiled.CreateGenerated();
			Assert.AreEqual(2, panel.Count);
			Assert.AreEqual("a", panel.Title);
		}

		[Test]
		public void OptionalParametersKnowWhenAValueIsSet()
		{
			var compiled = CodeGenHelper.Compile(CodeGenHelper.RootCode, Xaml(@"Flag=""{TestOptional True, Wpf=False}"" Mode=""{TestOptional Second}"""));
			StringAssert.Contains("TestOptionalExtension.Provide<bool>(", compiled.Result.Source);
			var panel = (TestPanel)compiled.CreateGenerated();
			Assert.IsFalse(panel.Flag, "Wpf=False is set, so it should win over the default");
			Assert.AreEqual(TestMode.Second, panel.Mode);
		}

		[Test]
		public void GeneratedCodeCompilesWithCSharp7()
		{
			var compilation = CodeGenHelper.CreateCompilation(CodeGenHelper.RootCode);
			compilation = compilation.WithOptions(compilation.Options).RemoveAllSyntaxTrees()
				.AddSyntaxTrees(Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(CodeGenHelper.RootCode, new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp7_3)));
			var result = new XamlCodeGenerator(compilation).Generate(Xaml(@"Title=""{Describe x}"" Count=""{TestOn 1}""", "<TestItem x:Name=\"item\" />"), new XamlCodeGenOptions { FilePath = CodeGenHelper.FilePath });
			Assert.IsTrue(result.Success, string.Join("\n", result.Diagnostics));
			StringAssert.DoesNotContain("#nullable", result.Source);
			var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(result.Source, new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp7_3));
			var errors = compilation.AddSyntaxTrees(tree).GetDiagnostics().Where(r => r.Severity == DiagnosticSeverity.Error).ToList();
			Assert.IsEmpty(errors, string.Join("\n", errors) + "\n" + result.Source);
		}

		[Test]
		public void OtherMarkupExtensionsUseProvideValue()
		{
			var compiled = CodeGenHelper.Compile(CodeGenHelper.RootCode, Xaml("", @"<TestItem Text=""{Describe pre}"" />"));
			StringAssert.Contains("ProvideValue", compiled.Result.Source);
			var panel = (TestPanel)compiled.CreateGenerated();
			Assert.AreEqual("pre:TestItem.Text:Root", ((TestItem)panel.Children[0]).Text);
		}

		[Test]
		public void EndInitIsCalledAfterStoringInTheOwner()
		{
			const string content = @"<TestInitItem Label=""outer""><TestInitItem.Child><TestInitItem Label=""child"" /></TestInitItem.Child><TestInitItem Label=""item""><TestInitItem Label=""nested"" /></TestInitItem></TestInitItem>";
			var compiled = CodeGenHelper.Compile(CodeGenHelper.RootCode, Xaml("", content));
			foreach (var (mode, panel) in new[] { ("compiled", (TestPanel)compiled.CreateGenerated()), ("runtime", (TestPanel)compiled.CreateRuntime(Xaml("", content))) })
			{
				var items = ((TestInitItem)panel.Children.Single()).Descendants().ToList();
				Assert.That(items.Select(r => r.Label), Is.EquivalentTo(new[] { "child", "item", "nested" }));
				foreach (var item in items)
					Assert.IsTrue(item.OwnedAtEndInit, $"{mode}: {item.Label} should be in its owner at EndInit");
			}
		}

		[Test]
		public void NullFromMarkupExtensionIsNotConverted()
		{
			const string xaml = @"<TestItem Row=""{Nothing}"" />";
			var compiled = CodeGenHelper.Compile(CodeGenHelper.RootCode, Xaml("", xaml));
			var panel = (TestPanel)compiled.CreateGenerated();
			Assert.IsNull(((TestItem)panel.Children[0]).Row);
			var runtime = (TestPanel)compiled.CreateRuntime(Xaml("", xaml));
			Assert.IsNull(((TestItem)runtime.Children[0]).Row);
		}

		[Test]
		public void PartialMethodDeclarationIsImplemented()
		{
			const string code = @"
namespace Gen
{
	public partial class Root : Portable.Xaml.CodeGen.Tests.Types.TestPanel
	{
		public Root() => InitializeComponent();
		public partial void InitializeComponent();
	}
}";
			var compiled = CodeGenHelper.Compile(code, Xaml(@"Title=""ctor"""));
			var panel = (TestPanel)Activator.CreateInstance(compiled.Assembly.GetType("Gen.Root"));
			Assert.AreEqual("ctor", panel.Title);
		}

		[Test]
		public void NonPartialClassIsAWarning()
		{
			const string code = @"
namespace Gen
{
	public class Root : Portable.Xaml.CodeGen.Tests.Types.TestPanel { }
}";
			var result = CodeGenHelper.Generate(code, Xaml(""));
			Assert.IsFalse(result.Success);
			var diagnostic = result.Diagnostics.Single();
			Assert.AreEqual(DiagnosticSeverity.Warning, diagnostic.Severity);
			StringAssert.Contains("must be partial", diagnostic.Message);
		}

		[Test]
		public void UnknownMemberIsAnError()
		{
			var result = CodeGenHelper.Generate(CodeGenHelper.RootCode, Xaml(@"Nope=""1"""));
			Assert.IsFalse(result.Success);
		}

		[Test]
		public void RootClassMustDeriveFromRootElement()
		{
			const string code = "namespace Gen { public partial class Root { } }";
			var result = CodeGenHelper.Generate(code, Xaml(""));
			StringAssert.Contains("must derive from", result.Diagnostics.Single().Message);
		}

		[Test]
		public void IgnoredNamespacesAreSkipped()
		{
			var xaml = $@"<TestPanel {Namespaces} xmlns:d=""urn:codegen-test"" Title=""real"" d:Count=""5"" />";
			var options = new XamlCodeGenOptions { FilePath = CodeGenHelper.FilePath };
			options.IgnoredNamespaces.Add("urn:codegen-test-design");
			var result = CodeGenHelper.Generate(CodeGenHelper.RootCode, xaml.Replace("xmlns:d=\"urn:codegen-test\"", "xmlns:d=\"urn:codegen-test-design\""), options);
			Assert.IsTrue(result.Success, string.Join("\n", result.Diagnostics));
			StringAssert.DoesNotContain("Count", result.Source);
		}
	}
}
