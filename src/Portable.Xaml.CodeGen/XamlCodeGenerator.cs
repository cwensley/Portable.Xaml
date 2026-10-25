using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Portable.Xaml.CodeGen.Schema;

namespace Portable.Xaml.CodeGen
{
	/// <summary>
	/// Turns XAML into C# for a compilation. Create one per compilation and call <see cref="Generate"/> for each file.
	/// </summary>
	/// <remarks>
	/// Subclass to change type lookup (<see cref="CreateSchemaContext"/>), reading (<see cref="CreateReader"/>, <see cref="CreateReaderSettings"/>)
	/// or the generated code (<see cref="CreateEmitter"/>).
	/// </remarks>
	public class XamlCodeGenerator
	{
		SymbolXamlSchemaContext _schemaContext;

		public XamlCodeGenerator(Compilation compilation)
		{
			Compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
		}

		public Compilation Compilation { get; }

		public SymbolXamlSchemaContext SchemaContext => _schemaContext ?? (_schemaContext = CreateSchemaContext());

		protected virtual SymbolXamlSchemaContext CreateSchemaContext() => new SymbolXamlSchemaContext(Compilation);

		protected virtual XamlCodeEmitter CreateEmitter(XamlCodeGenOptions options, INamedTypeSymbol rootClass)
			=> new XamlCodeEmitter(SchemaContext, options, rootClass);

		protected virtual XamlXmlReaderSettings CreateReaderSettings(XamlCodeGenOptions options)
		{
			var settings = new XamlXmlReaderSettings { ProvideLineInfo = true };
			foreach (var ns in options.Namespaces)
				settings.AddNamespace(ns.Key, ns.Value);
			return settings;
		}

		/// <summary>Called before a file is read, e.g. to find the code-behind class or add namespaces.</summary>
		protected virtual void PrepareOptions(XamlCodeGenOptions options)
		{
		}

		/// <summary>Creates the reader for a file's text, e.g. a <see cref="Json.XamlJsonReader"/> for JSON.</summary>
		protected virtual XamlReader CreateReader(string text, XamlCodeGenOptions options)
			=> new XamlXmlReader(new StringReader(text), SchemaContext, CreateReaderSettings(options));

		/// <summary>Reads XAML into a tree of objects without generating code.</summary>
		public XamlObjectNode Read(string xaml, XamlCodeGenOptions options)
		{
			var reader = CreateReader(xaml, options);
			return XamlObjectTree.Read(reader, options.Namespaces);
		}

		public XamlCodeGenResult Generate(string xaml, XamlCodeGenOptions options)
		{
			var result = new XamlCodeGenResult();
			try
			{
				PrepareOptions(options);
				var root = Read(xaml, options);
				if (root == null)
					throw new XamlCodeGenException("The document has no root object", default);

				var rootClassName = options.RootClassName ?? root.GetText(XamlLanguage.Class);
				if (string.IsNullOrEmpty(rootClassName))
					throw new XamlCodeGenException("No class to generate into. Set x:Class on the root element.", root.LineInfo);
				var rootClass = Compilation.GetTypeByMetadataName(rootClassName);
				if (rootClass == null)
					throw new XamlCodeGenException($"Class '{rootClassName}' was not found", root.LineInfo);
				result.RootClass = rootClass;

				var notPartial = GetNonPartialType(rootClass);
				if (notPartial != null)
				{
					result.Diagnostics.Add(new XamlCodeGenDiagnostic(DiagnosticSeverity.Warning, $"'{notPartial.ToDisplayString()}' must be partial for its XAML to be compiled", root.LineInfo));
					return result;
				}

				var emitter = CreateEmitter(options, rootClass);
				try
				{
					result.Source = emitter.Generate(root);
				}
				finally
				{
					result.Diagnostics.AddRange(emitter.Diagnostics);
				}
			}
			catch (XamlCodeGenException ex)
			{
				result.Diagnostics.Add(new XamlCodeGenDiagnostic(DiagnosticSeverity.Error, ex.Message, ex.LineInfo));
			}
			catch (XamlException ex)
			{
				result.Diagnostics.Add(new XamlCodeGenDiagnostic(DiagnosticSeverity.Error, ex.Message, new XamlLineInfo(ex.LineNumber, ex.LinePosition)));
			}
			catch (XmlException ex)
			{
				result.Diagnostics.Add(new XamlCodeGenDiagnostic(DiagnosticSeverity.Error, ex.Message, new XamlLineInfo(ex.LineNumber, ex.LinePosition)));
			}
			return result;
		}

		static INamedTypeSymbol GetNonPartialType(INamedTypeSymbol type)
		{
			for (var t = type; t != null; t = t.ContainingType)
			{
				var refs = t.DeclaringSyntaxReferences;
				if (refs.Length == 0)
					return t;
				var isPartial = refs.Any(r => r.GetSyntax() is TypeDeclarationSyntax syntax && syntax.Modifiers.Any(SyntaxKind.PartialKeyword));
				if (!isPartial)
					return t;
			}
			return null;
		}
	}
}
