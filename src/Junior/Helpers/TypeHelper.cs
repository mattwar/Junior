using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Junior.Helpers
{
    internal static class TypeHelper
    {
        /// <summary>
        /// If the type specified is a nullable type, returns the underlying type.
        /// Otherwise returns the type.
        /// </summary>
        public static Type GetNonNullableType(Type type)
        {
            if (type.IsGenericType 
                && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        /// <summary>
        /// Returns the element type of any type that implements <see cref="IEnumerable"/>
        /// </summary>
        public static Type? GetElementType(Type type)
        {
            TryGetElementType(type, out var elementType);
            return elementType;
        }

        /// <summary>
        /// Returns true if the type implements <see cref="IEnumerable"/>,
        /// and outputs the element type.
        /// </summary>
        public static bool TryGetElementType(Type type, [NotNullWhen(true)] out Type? elementType)
        {
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return elementType != null;
            }
            else if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                if (type.IsGenericType 
                    && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    elementType = type.GetGenericArguments()[0];
                    return true;
                }
                else
                {
                    elementType = type.GetInterfaces()
                        .Where(i => i.IsGenericType 
                            && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                        .Select(i => i.GetGenericArguments()[0])
                        .FirstOrDefault()
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

        /// <summary>
        /// Returns if the type implements <see cref="IDictionary"/> or <see cref="IReadOnlyDictionary"/>
        /// and outputs the key and value types.
        /// </summary>
        public static bool TryGetDictionaryTypes(
            Type type,
            [NotNullWhen(true)]
            out Type? keyType,
            [NotNullWhen(true)]
            out Type? valueType)
        {
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType
                    && i.GetGenericTypeDefinition() is Type gtd
                    && (gtd == typeof(IReadOnlyDictionary<,>)
                        || gtd == typeof(IDictionary<,>)))
                {
                    var args = i.GetGenericArguments();
                    keyType = args[0];
                    valueType = args[1];
                    return true;
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
        /// Returns the type of a type member,
        /// either the property type, field type or method return type.
        /// </summary>
        public static Type? GetMemberType(MemberInfo member)
        {
            switch (member)
            {
                case PropertyInfo p:
                    return p.PropertyType;
                case FieldInfo f:
                    return f.FieldType;
                case MethodInfo m:
                    return m.ReturnType;
                default:
                    return null;
            }
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
        public static Delegate CreateObjectArrayConstructorDelegate(ConstructorInfo constructor)
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
        /// Creates a delegate that will set a field value.
        /// </summary>
        public static Delegate CreateFieldSetterDelegate(Type type, FieldInfo field)
        {
            var instance = Expression.Parameter(type, "instance");
            var value = Expression.Parameter(field.FieldType, "value");
            var delegateType = typeof(Action<,>).MakeGenericType(type, field.FieldType);
            var lambda = Expression.Lambda(
                delegateType,
                Expression.Assign(Expression.Field(instance, field), value),
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
        /// parameters correspond to public properties or fields.
        /// </summary>
        public static ConstructorInfo? GetPublicPropertyConstructor(Type type)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.MemberType == MemberTypes.Property
                    || m.MemberType == MemberTypes.Field)
                .ToList();

            // if the member has the same name as the parameter, regardless of case,
            // consider them related.
            bool HasCorrespondingPropertyOrField(ParameterInfo param) =>
                param.Name != null
                    && members.Any(m =>
                        string.Compare(param.Name, m.Name, StringComparison.OrdinalIgnoreCase) == 0
                        && GetMemberType(m) == param.ParameterType);

            // constructors with parameters that correspond to properties
            var propConstructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Select(c => (Constructor: c, Parameters: c.GetParameters()))
                .Where(x => x.Parameters.All(pm => HasCorrespondingPropertyOrField(pm)))
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