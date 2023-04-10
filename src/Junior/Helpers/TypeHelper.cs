using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Junior.Helpers
{
    internal static class TypeHelper
    {
        public static Type GetNonNullableType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        public static Type? GetElementType(Type type)
        {
            TryGetElementType(type, out var elementType);
            return elementType;
        }

        public static bool TryGetElementType(Type type, [NotNullWhen(true)] out Type? elementType)
        {
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return elementType != null;
            }
            else if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    elementType = type.GetGenericArguments()[0];
                    return true;
                }
                else
                {
                    elementType = type.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                        .Select(i => i.GetGenericArguments()[0]).FirstOrDefault()
                        ?? typeof(object);
                    return true;
                }
            }
            else
            {
                elementType = null;
                return false;
            }
        }

        public static bool TryGetDictionaryTypes(
            Type type,
            [NotNullWhen(true)]
            out Type? keyType,
            [NotNullWhen(true)]
            out Type? valueType)
        {
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType)
                {
                    var gtd = i.GetGenericTypeDefinition();
                    if (gtd == typeof(IReadOnlyDictionary<,>) || gtd == typeof(IDictionary<,>))
                    {
                        var args = i.GetGenericArguments();
                        keyType = args[0];
                        valueType = args[1];
                        return true;
                    }
                }
            }

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                keyType = typeof(object);
                valueType = typeof(object);
                return true;
            }

            keyType = null;
            valueType = null;
            return false;
        }


        /// <summary>
        /// Creates a delgate that will invoke a constructor.
        /// </summary>
        public static Delegate CreateConstructorDelegate(ConstructorInfo constructor, Type[] delegateArgTypes)
        {
            var constructorParameters = constructor.GetParameters();
            var lambdaParameters = constructorParameters.Select((p, i) => Expression.Parameter(delegateArgTypes[i], p.Name)).ToArray();
            var args = lambdaParameters.Select((p, i) => Expression.Convert(p, constructorParameters[i].ParameterType)).ToArray();
            var lambda = Expression.Lambda(Expression.New(constructor, args), lambdaParameters);
            return lambda.Compile();
        }

        /// <summary>
        /// Creates a delgate that will invoke a constructor with array of object for parameters
        /// </summary>
        public static Delegate CreateObjectArrayDelegate(ConstructorInfo constructor)
        {
            var constructorParameters = constructor.GetParameters();
            var arrayParam = Expression.Parameter(typeof(object[]), "paramArray");
            var args = constructorParameters.Select((p, i) =>
                Expression.Convert(
                    Expression.ArrayIndex(arrayParam, Expression.Constant(i)),
                    p.ParameterType)).ToArray();
            var lambda = Expression.Lambda(Expression.New(constructor, args), arrayParam);
            return lambda.Compile();
        }

        /// <summary>
        /// Creates a delegate that will set a property value.
        /// </summary>
        public static Delegate CreatePropertySetterDelegate(Type type, PropertyInfo property)
        {
            var instance = Expression.Parameter(type, "instance");
            var value = Expression.Parameter(property.PropertyType, "value");
            var delegateType = typeof(Action<,>).MakeGenericType(type, property.PropertyType);
            var lambda = Expression.Lambda(
                delegateType,
                Expression.Assign(Expression.Property(instance, property), value),
                new[] { instance, value });
            return lambda.Compile();
        }

        /// <summary>
        /// Returns if the type has a default constructor.
        /// </summary>
        public static bool HasPublicDefaultConstructor(Type type)
        {
            return type.IsValueType
                || type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                       .FirstOrDefault(c => c.GetParameters().Length == 0)
                       != null;
        }

        /// <summary>
        /// Returns the public constructor with most parameters where all the 
        /// arguments correspond to public properties.
        /// </summary>
        public static ConstructorInfo? GetPublicPropertyConstructor(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            bool HasMatchingProperty(ParameterInfo param) =>
                properties?.FirstOrDefault(p =>
                    string.Compare(p.Name, param.Name, StringComparison.OrdinalIgnoreCase) == 0
                    && param.ParameterType == p.PropertyType) != null;

            // constructors with parameters that correspond to properties
            var propConstructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Select(c => (Constructor: c, Parameters: c.GetParameters()))
                .Where(x => x.Parameters.All(pm => HasMatchingProperty(pm)))
                .ToList();

            var best = propConstructors
                .OrderByDescending(x => x.Parameters.Length)
                .FirstOrDefault();

            return best.Constructor;
        }

        /// <summary>
        /// Returns the <see cref="ConstructorInfo"/> for a type 
        /// with one parameter that can be assigned a list.
        /// </summary>
        public static ConstructorInfo? GetListCompatibleConstructor(Type type)
        {
            return
                type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(c => c.GetParameters() is var ps
                && ps.Length == 1
                && typeof(IEnumerable).IsAssignableFrom(ps[0].ParameterType));
        }

        /// <summary>
        /// Returns the <see cref="ConstructorInfo"/> for a type
        /// with one parameter that can be assigned a list.
        /// </summary>
        public static ConstructorInfo? GetDictionaryCompatibleConstructor(Type type)
        {
            return
                type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(c =>
                    c.GetParameters() is var ps
                    && ps.Length == 1
                    && typeof(IDictionary).IsAssignableFrom(ps[0].ParameterType));
        }

        /// <summary>
        /// Returns the <see cref="ConstructorInfo"/> for a type
        /// with one parameter that can be assigned a string.
        /// </summary>
        public static ConstructorInfo? GetStringCompatibleConstructor(Type type)
        {
            return GetMatchingConstructor(type, _stringArg);
        }

        private static readonly Type[] _stringArg = new[] { typeof(string) };

        /// <summary>
        /// Returns the constructor that matches the arg types.
        /// </summary>
        public static ConstructorInfo? GetMatchingConstructor(Type type, params Type[] argTypes)
        {
            return type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(c => IsMatchingConstructor(c, argTypes));
        }

        /// <summary>
        /// Returns true if the constructor has parameters that are assignable from the argument types.
        /// </summary>
        public static bool IsMatchingConstructor(ConstructorInfo constructor, params Type[] argTypes)
        {
            var parameters = constructor.GetParameters();

            if (parameters.Length != argTypes.Length)
            {
                return false;
            }

            for (int i = 0; i < argTypes.Length; i++)
            {
                if (!parameters[i].ParameterType.IsAssignableFrom(argTypes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the methdo that matches the name and arg types.
        /// </summary>
        public static MethodInfo? GetMatchingMethod(Type type, string name, params Type[] argTypes)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => IsMatchingMethod(m, name, argTypes));
        }

        /// <summary>
        /// Returns true if the method has the specified name and parameters that are 
        /// assignable from the argument types.
        /// </summary>
        public static bool IsMatchingMethod(MethodInfo method, string name, params Type[] argTypes)
        {
            if (method.Name != name)
            {
                return false;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != argTypes.Length)
            {
                return false;
            }

            for (int i = 0; i < argTypes.Length; i++)
            {
                if (!parameters[i].ParameterType.IsAssignableFrom(argTypes[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}