using System;
using System.IO;
using System.Reflection;

namespace Tests.Portable.Xaml
{
    static class Compat
    {

#if PORTABLE_XAML

		public const string Namespace = "Portable.Xaml";
		public const string Prefix = "px";
		public static bool IsPortableXaml = true;
#else
        public const string Namespace = "System.Xaml";
        public const string Prefix = "sx";
        public static bool IsPortableXaml = false;
#endif

        public const string TestAssemblyName = "Tests." + Namespace;

        public const string TestAssemblyNamespace = "clr-namespace:Tests.Portable.Xaml;assembly=" + TestAssemblyName;

#if NETSTANDARD
#if HAS_ISUPPORT_INITIALIZE
		public const bool HasISupportInitializeInterface = true;
#else
		public const bool HasISupportInitializeInterface = false;
#endif
#else
        public const bool HasISupportInitializeInterface = true;
#endif


        public static string Fixup(this string str)
        {
#if PORTABLE_XAML
			return str;
#else
            return str
                .Replace("Portable.Xaml.Markup", "System.Windows.Markup")
                .Replace("Portable.Xaml", Namespace);
#endif
        }

#if !NETSTANDARD
        public static Type GetTypeInfo(this Type type) => type;
#endif

        public static Assembly GetAssembly(this Type type) => type.GetTypeInfo().Assembly;
        public static bool GetIsGenericType(this Type type) => type.GetTypeInfo().IsGenericType;

        public static string UpdateXml(this string str)
        {
            return str
                .Replace("assembly=Tests.Portable.Xaml", $"assembly={TestAssemblyName}")
                .Replace("clr-namespace:Portable.Xaml;assembly=Portable.Xaml", $"clr-namespace:{Compat.Namespace};assembly={Compat.Namespace}")
                .Replace($" px:", $" {Compat.Prefix}:")
                .Replace($"xmlns:px", $"xmlns:{Compat.Prefix}")
                .Replace("\r", "")
                .Replace("\n", Environment.NewLine);
        }

        public static string UpdateJson(this string str)
        {
            return str
                .Replace("clr-namespace:Portable.Xaml;assembly=Portable.Xaml", $"clr-namespace:{Compat.Namespace};assembly={Compat.Namespace}")
                .Replace($" px:", $" {Compat.Prefix}:")
                .Replace($"$ns:px", $"$ns:{Compat.Prefix}")
                .Replace("\r", "")
                .Replace("\n", Environment.NewLine);
        }

        public static bool IsMono => Type.GetType("Mono.Runtime", false) != null;

#if NETCOREAPP
		public static bool IsNetCore => true;
#else
		public static bool IsNetCore => false;
#endif

        public static Stream GetTestFile(string fileName)
        {
            var resourceName = Compat.TestAssemblyName + ".XmlFiles." + fileName;
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException($"Could not find embedded resource {resourceName}");
            return stream;
        }
        public static string GetTestFileText(string fileName)
        {
            var stream = GetTestFile(fileName);
            return new StreamReader(stream).ReadToEnd();
        }
    }
}