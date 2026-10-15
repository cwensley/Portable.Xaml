namespace Portable.Xaml.Markup
{
	/// <summary>
	/// A parameter value that may not have been set, for <see cref="XamlCodeGenAttribute"/> methods.
	/// </summary>
	/// <remarks>
	/// Lets a codegen method tell an unset property apart from one set to the default value, e.g. <c>Wpf=False</c>.
	/// </remarks>
	public struct XamlOptional<T>
	{
		public XamlOptional(T value)
		{
			Value = value;
			HasValue = true;
		}

		public bool HasValue { get; }

		public T Value { get; }

		public static implicit operator XamlOptional<T>(T value) => new XamlOptional<T>(value);

		public override string ToString() => HasValue ? Value?.ToString() : string.Empty;
	}
}
