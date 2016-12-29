using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Xaml
{
	static class MemberExpressionExtensions
	{
		static ParameterExpression s_InstanceExpression = Expression.Parameter(typeof(object), "instance");
		static ParameterExpression s_ValueExpression = Expression.Parameter(typeof(object), "value");
		static ParameterExpression[] s_ParameterExpressions = { s_InstanceExpression, s_ValueExpression };
		static Type s_TargetExceptionType = typeof(Assembly).GetTypeInfo().Assembly.GetType("System.Reflection.TargetException");

		public static Exception TargetException(Type targetType, Type instanceType)
		{
			return s_TargetExceptionType != null
				? (Exception)Activator.CreateInstance(s_TargetExceptionType)
				: new InvalidOperationException($"Instance of type {instanceType} is not assignable to target type {targetType}");
		}

		public static Func<object, object> BuildGetExpression(this MethodInfo getter)
		{
			var declaringType = getter.DeclaringType;

			var instanceCast = !declaringType.GetTypeInfo().IsValueType
				? Expression.TypeAs(s_InstanceExpression, declaringType)
				: Expression.Convert(s_InstanceExpression, declaringType);


			var typeAs = Expression.TypeAs(Expression.Call(instanceCast, getter), typeof(object));

			// check the type of instance and throw TargetException if it isn't compatible
			var checkCall = Expression.Call(s_checkInstanceMethod, Expression.Constant(declaringType), s_InstanceExpression);
			//var block = Expression.TryCatch(typeAs, Expression.Catch(typeof(Exception), Expression.Block(checkCall, Expression.Rethrow())));
			var block = Expression.Block(typeof(object), checkCall, typeAs);

			return Expression.Lambda<Func<object, object>>(block, s_InstanceExpression).Compile();
		}

		static MethodInfo s_checkInstanceMethod = GetMethodInfo(() => CheckInstance(null, null));
		static MethodInfo s_checkInstanceAndValueMethod = GetMethodInfo(() => CheckInstanceAndValue(null, null, null, null));

		static MethodInfo GetMethodInfo(Expression<Action> expression)
		{
			var member = expression.Body as MethodCallExpression;

			if (member != null)
				return member.Method;

			throw new ArgumentException("Expression is not a method", "expression");
		}

		static void CheckInstance(Type declaringType, object instance)
		{
			if (!declaringType.GetTypeInfo().IsAssignableFrom(instance.GetType().GetTypeInfo()))
				throw TargetException(declaringType, instance.GetType());
		}

		static void CheckInstanceAndValue(Type declaringType, Type propertyType, object instance, object value)
		{
			if (!declaringType.GetTypeInfo().IsAssignableFrom(instance.GetType().GetTypeInfo()))
				throw TargetException(declaringType, instance.GetType());
			if (value != null && !propertyType.GetTypeInfo().IsAssignableFrom(value.GetType().GetTypeInfo()))
				throw new ArgumentException($"Value of type {value.GetType()} cannot be assigned to member with type {propertyType}", nameof(value));
		}

		public static Action<object, object> BuildSetExpression(this MethodInfo setter)
		{
			var parameters = setter.GetParameters();
			bool isAttachable = parameters.Length > 1;
			if (parameters.Length == 0 || parameters.Length > 2)
				throw new ArgumentOutOfRangeException(nameof(setter), "Method must only have exactly one or two parameters");
			if (isAttachable && !setter.IsStatic)
				throw new ArgumentOutOfRangeException(nameof(setter), "Method must be static if it has two parameters, the first being the instance and second being the value");
			var declaringType = isAttachable ? parameters[0].ParameterType : setter.DeclaringType;

			// value as T is slightly faster than (T)value, so if it's not a value type, use that
			var instanceCast = !declaringType.GetTypeInfo().IsValueType
				? Expression.TypeAs(s_InstanceExpression, declaringType)
				: Expression.Convert(s_InstanceExpression, declaringType);

			var propertyType = parameters[isAttachable ? 1 : 0].ParameterType;
			var valueCast = !propertyType.GetTypeInfo().IsValueType
				? Expression.TypeAs(s_ValueExpression, propertyType)
				: Expression.Convert(s_ValueExpression, propertyType);

			Expression call;
			if (isAttachable)
				call = Expression.Call(setter, instanceCast, valueCast);
			else
				call = Expression.Call(instanceCast, setter, valueCast);

			// check the type of instance and value and throw TargetException if it isn't compatible
			var checkCall = Expression.Call(s_checkInstanceAndValueMethod, Expression.Constant(declaringType), Expression.Constant(propertyType), s_InstanceExpression, s_ValueExpression);
			var returnTarget = Expression.Label();
			/**
			var block = Expression.Block(
				Expression.TryCatch(
					Expression.Return(returnTarget, call),
					Expression.Catch(typeof(Exception), Expression.Block(checkCall, Expression.Rethrow()))
				),
				Expression.Label(returnTarget));
			//block = Expression.Block(checkCall, block);
			/**/
			var block = Expression.Block(checkCall, call);
			/**/

			return Expression.Lambda<Action<object, object>>(block, s_ParameterExpressions).Compile();
		}
	}
}
