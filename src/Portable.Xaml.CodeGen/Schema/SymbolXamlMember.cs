using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Portable.Xaml.Schema;

namespace Portable.Xaml.CodeGen.Schema
{
	/// <summary>
	/// A XAML member backed by a property, event, or attachable Get/Set/Add methods.
	/// </summary>
	public class SymbolXamlMember : XamlMember
	{
		public SymbolXamlMember(ISymbol symbol, SymbolXamlType declaringType)
			: base(symbol.Name, declaringType, false)
		{
			Symbol = symbol;
			Context = declaringType.Context;
		}

		public SymbolXamlMember(string name, SymbolXamlType declaringType, IMethodSymbol getter, IMethodSymbol setter, IMethodSymbol adder)
			: base(name, declaringType, true)
		{
			Context = declaringType.Context;
			Getter = getter;
			Setter = setter;
			Adder = adder;
			Symbol = (ISymbol)setter ?? (ISymbol)adder ?? getter;
		}

		public SymbolXamlSchemaContext Context { get; }

		/// <summary>The property or event, or for attachable members the setter, adder or getter method.</summary>
		public ISymbol Symbol { get; }

		public IPropertySymbol Property => Symbol as IPropertySymbol;

		public IEventSymbol Event => Symbol as IEventSymbol;

		public IMethodSymbol Getter { get; }

		public IMethodSymbol Setter { get; }

		public IMethodSymbol Adder { get; }

		/// <summary>The type of the value assigned to this member.</summary>
		public ITypeSymbol ValueType
		{
			get
			{
				if (Property != null)
					return Property.Type;
				if (Event != null)
					return Event.Type;
				if (Setter != null)
					return Setter.Parameters[1].Type;
				if (Adder != null)
					return Adder.Parameters[1].Type;
				return Getter?.ReturnType;
			}
		}

		/// <summary>Constructor parameter this property is set from, from [ConstructorArgument].</summary>
		public string ConstructorArgumentName
			=> Property?.FindAttribute(c => Context.IsXamlAttribute(c, "ConstructorArgumentAttribute")).GetStringArgument();

		public override string ToString() => Symbol != null ? $"{Symbol.ContainingType.FullName()}.{Name}" : Name;

		public override int GetHashCode() => Name.GetHashCode() ^ (DeclaringType?.Name.GetHashCode() ?? 0);

		protected override bool LookupIsUnknown() => false;

		protected override XamlType LookupType() => ValueType != null ? Context.GetXamlType(ValueType) : XamlLanguage.Object;

		protected override XamlType LookupTargetType()
		{
			var target = (Setter ?? Adder ?? Getter)?.Parameters.FirstOrDefault()?.Type;
			return target != null ? Context.GetXamlType(target) : DeclaringType;
		}

		protected override bool LookupIsEvent() => Event != null || Adder != null;

		protected override bool LookupIsReadOnly()
		{
			if (Property != null)
				return Property.SetMethod == null || Property.SetMethod.DeclaredAccessibility != Accessibility.Public;
			if (IsAttachable)
				return Setter == null && Adder == null;
			return false;
		}

		protected override bool LookupIsReadPublic()
		{
			if (Property != null)
				return Property.GetMethod?.DeclaredAccessibility == Accessibility.Public;
			return Getter != null;
		}

		protected override bool LookupIsWritePublic()
		{
			if (Property != null)
				return Property.SetMethod?.DeclaredAccessibility == Accessibility.Public;
			return true;
		}

		protected override bool LookupIsWriteOnly() => Property != null ? Property.GetMethod == null : IsAttachable && Getter == null;

		protected override bool LookupIsAmbient()
			=> Property?.FindAttribute(c => Context.IsXamlAttribute(c, "AmbientAttribute")) != null;

		protected override IList<XamlMember> LookupDependsOn() => null;

		protected override XamlValueConverter<XamlDeferringLoader> LookupDeferringLoader() => null;

		protected override XamlValueConverter<System.ComponentModel.TypeConverter> LookupTypeConverter() => null;

		protected override XamlValueConverter<Markup.ValueSerializer> LookupValueSerializer() => null;

		protected override System.Reflection.MemberInfo LookupUnderlyingMember() => null;

		protected override System.Reflection.MethodInfo LookupUnderlyingGetter() => null;

		protected override System.Reflection.MethodInfo LookupUnderlyingSetter() => null;
	}
}
