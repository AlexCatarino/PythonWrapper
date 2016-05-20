using QuantConnect.Securities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections;
using QuantConnect.Algorithm;

namespace QuantConnect.PythonWrapper
{
    public class CodeGenerator
    {
        public string Prefix;
        private IEnumerable<Method> Methods;
        private IEnumerable<Property> Properties;
        private IEnumerable<CodeGenerator> Children;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        public CodeGenerator(Type type)
        {
            Prefix = type.Name;

            var setMethods = type.GetMethods()
                .Where(x => x.Name.StartsWith("Set"))
                .Select(x => x.Name.Substring(3)).Distinct();

            Methods = type.GetMethods().OrderBy(x => x.ReturnType.Name)
                .Where(x => x.DeclaringType == type && !x.Name.Contains("et_"))
                .Select(x => new Method(x));

            Properties = type.GetProperties().OrderBy(x => x.PropertyType.Name)
                .Where(x => x.DeclaringType == type && !x.PropertyType.IsEnum && !setMethods.Contains(x.Name))
                .Select(x => new Property(x));

            Children = Properties
                .Where(x => x.Type.Namespace.StartsWith("QuantConnect"))
                .Distinct().Select(x => new CodeGenerator(x.Type));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="methods"></param>
        /// <param name="properties"></param>
        public CodeGenerator(string prefix, IEnumerable<Method> methods, IEnumerable<Property> properties)
        {
            Prefix = prefix;
            Methods = methods;
            Properties = properties;
            Children = Enumerable.Empty<CodeGenerator>();
        }

        public override string ToString()
        {
            var children = Children
                .Map(p => true, (CodeGenerator n) => { return n.Children; })
                .Select(x => new CodeGenerator(x.Prefix, x.Methods, x.Properties))
                .DistinctBy(x => x.Prefix).OrderBy(x => x.Prefix);

            return string.Format("public class {0}", Prefix) + Environment.NewLine +
                "{" + Environment.NewLine +
                    string.Join(Environment.NewLine, Properties) + Environment.NewLine +
                    string.Join(Environment.NewLine, Methods) + Environment.NewLine +
                "}" + Environment.NewLine +
                string.Join(Environment.NewLine, children);
        }      
    }

    public class Method
    {
        private Type _type;
        private string _name;
        private bool _isGeneric;
        private bool _isSpecialCase;
        private IEnumerable<Parameter> _parameters;
        private IEnumerable<Type> _genericArguments;

        public Method(MethodInfo methodInfo)
        {
            _name = methodInfo.Name;
            _type = methodInfo.ReturnType;
            _isGeneric = methodInfo.IsGenericMethod;
            _isSpecialCase = methodInfo.DeclaringType == typeof(QCAlgorithm) && (_name == "Initialize" || _name.StartsWith("On"));
            _parameters = methodInfo.GetParameters().Select(x => new Parameter(x));
            _genericArguments = methodInfo.GetGenericArguments();
        }
        
        public override string ToString()
        {
            var pyName = _name.ToPythonista();

            // event is a C# reserved work, we cannot use it.
            if (pyName == "event")
            {
                return string.Empty;
            }

            var typeString = _type.ToTypeString(false);
            var genericTag = _isGeneric ? "<T>({0})" : "({0})";

            var events = "";
            var header = string.Format(genericTag, string.Join(", ", _parameters));
            var body = (_type == typeof(void) ? "{0}" : "return {0}") + 
                string.Format(genericTag, string.Join(", ", _parameters.Select(x => x.ToShortString())));

            if (_isSpecialCase)
            {
                events = "public virtual " + typeString + " " + pyName + header + "\r\n{\r\n}\r\n";
                header = "public override " + typeString + " " + _name + header;
                body = string.Format(body, pyName);
            }
            else
            {
                header = "public " + typeString + " " + pyName + header;
                body = string.Format(body, _name);
            }

            return events + header + GetGenericArgumentString() + "\r\n{\r\n\t" + body + ";\r\n}";
        }

        private string GetGenericArgumentString()
        {
            var genericArgumentsString = "";

            if (_isGeneric)
            {
                foreach (var genericArgument in _genericArguments)
                {
                    if (genericArgument.BaseType != typeof(Object))
                    {
                        genericArgumentsString += genericArgument.BaseType.Name + ", ";
                    }
                }

                if (!string.IsNullOrWhiteSpace(genericArgumentsString))
                {
                    genericArgumentsString = string.Format(" where T : {0} new()", genericArgumentsString);
                }
            }

            return genericArgumentsString;
        }
    }

    public class Parameter
    {
        private bool _isOut;
        private bool _hasDefaultValue;
        private string _defaultValue;

        public Type Type;
        public string Name;
        
        public Parameter(ParameterInfo parameterInfo)
        {
            Name = parameterInfo.Name;
            Type = parameterInfo.ParameterType;

            _isOut = parameterInfo.IsOut;
            _hasDefaultValue = parameterInfo.HasDefaultValue;
            _defaultValue = GetDefaultValueString(parameterInfo.DefaultValue);
        }

        public override string ToString()
        {            
            return Type.ToTypeString(_isOut) + " " + Name.ToPythonista() + _defaultValue;
        }

        public string ToShortString()
        {
            return (_isOut ? "out " : "") + Name.ToPythonista();
        }

        private string GetDefaultValueString(object defaultValue)
        {
            var defaultString = string.Empty;

            if (_hasDefaultValue)
            {
                defaultString =
                    defaultValue == null ? "null" :
                    defaultValue.GetType() == typeof(string) ? "\"" + defaultValue.ToString() + "\"" :
                    defaultValue.ToString();

                if (Type.IsEnum)
                {
                    defaultString = Type.Name + "." + defaultValue;
                }
                if (Type == typeof(decimal))
                {
                    defaultString += "m";
                }
                if (Type == typeof(bool))
                {
                    defaultString = defaultString.ToLower();
                }
                defaultString = " = " + defaultString;
            }

            return defaultString;
        }
    }

    public class Property
    {
        public Type Type;
        private string _name;
        
        public Property(PropertyInfo propertyInfo)
        {
            _name = propertyInfo.Name;
            Type = propertyInfo.PropertyType;
        }

        public override string ToString()
        {
            return "public " + Type.ToTypeString(false) + " " + _name.ToPythonista() + "{ get { return " + _name + "; } }";
        }
    }

    public static class Util
    {
        /// <summary>
        /// Converts name from C# style to Pythonista
        /// </summary>
        /// <param name="name">Name in C# style</param>
        /// <returns>string with the converted name</returns>
        public static string ToPythonista(this string name)
        {
            // When all characted are in lower case, does nothing
            if (name.All(c => char.IsLower(c)))
            {
                return name;
            }

            var outname = new StringBuilder();
            var lowered = name.ToLower();

            // Indicator API are upper case. In this case, simply returns the lower case name
            if (name.All(c => char.IsUpper(c)))
            {
                return lowered;
            }

            for (var i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0 && name[i - 1] != '_')
                {
                    outname.Append("_");
                }
                outname.Append(lowered[i]);
            }

            return outname.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="isOut"></param>
        /// <returns></returns>
        public static string ToTypeString(this Type type, bool isOut)
        {
            var genericArguments = type.GetGenericArguments();

            if (genericArguments.Count() > 0)
            {
                var hasBrackets = type.Name.Contains("[]");
                var excess = type.Name.Substring(type.Name.IndexOf('`'));
                var collectionName = type.Name.Replace(excess, "<");
                var typeStringBuilder = new StringBuilder(collectionName);

                foreach (var genericArgument in genericArguments)
                {
                    typeStringBuilder.Append(genericArgument.ToTypeString(false) + ", ");
                }

                typeStringBuilder.Append(">");

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    typeStringBuilder.Replace("Nullable<", "").Replace(", >", "?");
                }

                typeStringBuilder.Replace(", >", hasBrackets ? ">[]" : ">");

                return typeStringBuilder.ToString();
            }

            var systemObjects = new Dictionary<Type, string>
            {
                { typeof(void), "void" },       { typeof(void).MakeByRefType(), "void" },
                { typeof(short), "short" },     { typeof(short).MakeByRefType(), "short" },
                { typeof(int), "int" },         { typeof(int).MakeByRefType(), "int" },
                { typeof(long), "long" },       { typeof(long).MakeByRefType(), "long" },
                { typeof(double), "double" },   { typeof(double).MakeByRefType(), "double" },
                { typeof(decimal), "decimal" }, { typeof(decimal).MakeByRefType(), "decimal" },
                { typeof(string), "string" },   { typeof(string).MakeByRefType(), "string" },
                { typeof(bool), "bool" },       { typeof(bool).MakeByRefType(), "bool" },
            };

            var name = systemObjects.ContainsKey(type) ? systemObjects[type] : type.Name;
            return (isOut ? "out " : "") + name.Replace("&", " ");
        }

        /// <summary>
        /// Traverses an object hierarchy and return a flattened list of elements based on a predicate.
        /// </summary>
        /// <typeparam name="TSource">The type of object in your collection.</typeparam>
        /// <param name="source">The collection of your topmost TSource objects.</param>
        /// <param name="selectorFunction">A predicate for choosing the objects you want.</param>
        /// <param name="getChildrenFunction">A function that fetches the child collection from an object.</param>
        /// <returns>A flattened list of objects which meet the criteria in selectorFunction.</returns>
        public static IEnumerable<TSource> Map<TSource>(
          this IEnumerable<TSource> source,
          Func<TSource, bool> selectorFunction,
          Func<TSource, IEnumerable<TSource>> getChildrenFunction)
        {
            // Add what we have to the stack
            var flattenedList = source.Where(selectorFunction);

            // Go through the input enumerable looking for children,
            // and add those if we have them
            foreach (TSource element in source)
            {
                flattenedList = flattenedList
                    .Concat(getChildrenFunction(element)
                    .Map(selectorFunction, getChildrenFunction)
                );
            }
            return flattenedList;
        }

        /// <summary>
        /// DistinctBy:  
        /// </summary>
        /// <typeparam name="TSource">The type of object in your collection.</typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="source">The collection of your topmost TSource objects.</param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
            this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
    }
}
