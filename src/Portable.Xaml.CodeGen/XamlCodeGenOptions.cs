using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Portable.Xaml.CodeGen
{
	public class XamlCodeGenOptions
	{
		/// <summary>Path of the XAML file, used for #line directives and diagnostics.</summary>
		public string FilePath { get; set; }

		/// <summary>Full name of the code-behind class. When null, x:Class is used.</summary>
		public string RootClassName { get; set; }

		/// <summary>Name of the generated method that builds the object tree.</summary>
		public string MethodName { get; set; } = "InitializeComponent";

		/// <summary>Modifiers of the generated method, unless the class already declares it as a partial method.</summary>
		public string MethodModifiers { get; set; } = "private";

		/// <summary>Generate fields for x:Name'd objects that have no matching member in the code-behind class.</summary>
		public bool GenerateFields { get; set; } = true;

		/// <summary>Field modifier when x:FieldModifier isn't set.</summary>
		public string DefaultFieldModifier { get; set; } = "internal";

		/// <summary>Prefix to XAML namespace mappings available without declaring them, e.g. a default xmlns.</summary>
		public IDictionary<string, string> Namespaces { get; } = new Dictionary<string, string>();

		/// <summary>Members in these XAML namespaces are skipped, e.g. design-time only properties.</summary>
		public ISet<string> IgnoredNamespaces { get; } = new HashSet<string>();
	}

	public class XamlCodeGenResult
	{
		/// <summary>Generated C#, or null when generation failed.</summary>
		public string Source { get; internal set; }

		/// <summary>The code-behind class the source extends.</summary>
		public INamedTypeSymbol RootClass { get; internal set; }

		public List<XamlCodeGenDiagnostic> Diagnostics { get; } = new List<XamlCodeGenDiagnostic>();

		public bool Success => Source != null;
	}

	public class XamlCodeGenDiagnostic
	{
		public XamlCodeGenDiagnostic(DiagnosticSeverity severity, string message, XamlLineInfo lineInfo)
		{
			Severity = severity;
			Message = message;
			LineInfo = lineInfo;
		}

		public DiagnosticSeverity Severity { get; }

		public string Message { get; }

		public XamlLineInfo LineInfo { get; }

		public override string ToString() => LineInfo.HasLineInfo ? $"({LineInfo.Line},{LineInfo.Column}): {Severity}: {Message}" : $"{Severity}: {Message}";
	}
}
