using System;

namespace Portable.Xaml.Markup
{
	/// <summary>
	/// Names a static method that compiled XAML calls instead of using the runtime behaviour of a type.
	/// </summary>
	/// <remarks>
	/// On a markup extension, the method's parameters are matched to the extension's properties by name,
	/// e.g. <c>{On Wpf=1, Mac=2}</c> becomes <c>OnExtension.Provide(wpf: 1, mac: 2)</c>.
	/// A generic method gets the target property type as its type argument, and an <see cref="IServiceProvider"/>
	/// parameter receives the target object, property and root object.
	///
	/// On any other type, the method takes the attribute string and returns the value,
	/// e.g. <c>Color="#fff"</c> becomes <c>Color.Parse("#fff")</c>.
	///
	/// On an assembly, it does the same for a type defined elsewhere, so a XAML library can provide
	/// compiled conversions without the type's assembly referencing Portable.Xaml:
	/// <c>[assembly: XamlCodeGen(typeof(Color), typeof(MyConverters), nameof(MyConverters.Color))]</c>.
	/// An attribute on the type itself takes precedence.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
	public sealed class XamlCodeGenAttribute : Attribute
	{
		/// <param name="methodName">Static method on the attributed type to call.</param>
		public XamlCodeGenAttribute(string methodName)
		{
			MethodName = methodName;
		}

		/// <param name="declaringType">Type that declares the static method.</param>
		/// <param name="methodName">Static method to call.</param>
		public XamlCodeGenAttribute(Type declaringType, string methodName)
		{
			DeclaringType = declaringType;
			MethodName = methodName;
		}

		/// <param name="targetType">Type the method provides values for, when applied to an assembly.</param>
		/// <param name="declaringType">Type that declares the static method.</param>
		/// <param name="methodName">Static method to call.</param>
		public XamlCodeGenAttribute(Type targetType, Type declaringType, string methodName)
		{
			TargetType = targetType;
			DeclaringType = declaringType;
			MethodName = methodName;
		}

		public Type TargetType { get; private set; }

		public string MethodName { get; private set; }

		public Type DeclaringType { get; private set; }
	}
}
