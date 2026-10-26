using System.Collections.Generic;
using System.Linq;

namespace Portable.Xaml.Yaml
{
	[EnhancedXaml]
	public class XamlYamlReaderSettings : XamlReaderSettings
	{
		internal static XamlYamlReaderSettings Instance = new XamlYamlReaderSettings();

		Dictionary<string, string> _defaultNamespaces;

		/// <summary>
		/// Gets or sets the type of the root object when the YAML doesn't give a <c>$type</c>, e.g. the instance it is loaded into.
		/// </summary>
		public XamlType RootType { get; set; }

		/// <summary>
		/// Adds a namespace so the YAML doesn't need to declare it with <c>$ns</c>.
		/// </summary>
		/// <param name="prefix">Prefix of namespace, or null for the default namespace</param>
		/// <param name="xmlNamespace">Uri or clr-namespace to set as default</param>
		public void AddNamespace(string prefix, string xmlNamespace)
		{
			if (_defaultNamespaces == null)
				_defaultNamespaces = new Dictionary<string, string>();
			_defaultNamespaces[prefix ?? string.Empty] = xmlNamespace;
		}

		public IEnumerable<KeyValuePair<string, string>> DefaultNamespaces => _defaultNamespaces ?? Enumerable.Empty<KeyValuePair<string, string>>();
	}
}
