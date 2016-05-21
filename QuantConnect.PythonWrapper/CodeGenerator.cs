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
        private Type _type;
        private IEnumerable<Field> Fields;
        private IEnumerable<Method> Methods;
        private IEnumerable<Property> Properties;
        private IEnumerable<CodeGenerator> Children;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        public CodeGenerator(Type type)
        {
            _type = type;

            var setMethods = type.GetMethods()
                .Where(x => x.Name.StartsWith("Set"))
                .Select(x => x.Name.Substring(3)).Distinct();

            Methods = type.GetMethods().OrderBy(x => x.ReturnType.Name)
                .Where(x => x.DeclaringType == type && !x.Name.Contains("et_"))
                .Select(x => new Method(x));

            Fields = type.GetFields()
                .Where(x => x.DeclaringType == type && x.IsPublic)
                .Select(x => new Field(x));

            Properties = type.GetProperties().OrderBy(x => x.PropertyType.Name)
                .Where(x => x.DeclaringType == type && !x.PropertyType.IsEnum && !setMethods.Contains(x.Name))
                .Select(x => new Property(x));

            Children = Properties
                .Where(x => x.Type.Namespace.StartsWith("QuantConnect"))
                .Distinct().Select(x => new CodeGenerator(x.Type))
                // Flattens the tree
                .Map(p => true, (CodeGenerator n) => { return n.Children; })
                // Does not consider the grandchildren
                .Select(x => new CodeGenerator(x._type, x.Fields, x.Methods, x.Properties))
                // Does not pass repeated children
                .DistinctBy(x => x._type).OrderBy(x => x._type.Name);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="methods"></param>
        /// <param name="properties"></param>
        public CodeGenerator(Type type, IEnumerable<Field> fields, IEnumerable<Method> methods, IEnumerable<Property> properties)
        {
            _type = type;
            Fields = fields;
            Methods = methods;
            Properties = properties;
            Children = Enumerable.Empty<CodeGenerator>();
        }

        public override string ToString()
        {
            var kind = _type.IsClass ? "class" : _type.IsEnum ? "enum" : _type.IsValueType ? "struct" : "interface";

            return string.Format("public {0} {1}", kind, _type.Name) + Environment.NewLine +
                "{" + Environment.NewLine +
                    string.Join(Environment.NewLine, Fields) +
                    (Fields.Count() > 0 ? Environment.NewLine + Environment.NewLine : "") +
                    string.Join(Environment.NewLine, Properties) +
                    (Properties.Count() > 0 ? Environment.NewLine + Environment.NewLine : "") +
                    string.Join(Environment.NewLine, Methods) +
                    (Methods.Count() > 0 ? Environment.NewLine : "") +
                "}" + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, Children);
        }
    }

    public class Field
    {
        private Type _type;
        private string _name;

        public Field(FieldInfo fieldInfo)
        {
            _name = fieldInfo.Name;
            _type = fieldInfo.FieldType;
        }

        public override string ToString()
        {
            return string.Format("public {0} {1} = {2};", _type.ToTypeString(false), _name.ToPythonista(), _name);
        }
    }

    public class Method
    {
        private Type _type;
        private string _name;
        private bool _isGeneric;
        private bool _isSpecialCase;
        private bool _fromInterface;
        private IEnumerable<Parameter> _parameters;
        private IEnumerable<Type> _genericArguments;

        public Method(MethodInfo methodInfo)
        {
            _name = methodInfo.Name;
            _type = methodInfo.ReturnType;
            _isGeneric = methodInfo.IsGenericMethod;
            _isSpecialCase = methodInfo.DeclaringType == typeof(QCAlgorithm) && (_name == "Initialize" || _name.StartsWith("On"));
            _fromInterface = methodInfo.DeclaringType.IsInterface;
            _parameters = methodInfo.GetParameters().Select(x => new Parameter(x));
            _genericArguments = methodInfo.GetGenericArguments();
        }

        public override string ToString()
        {
            // event is a C# reserved work, we cannot use it.            
            return _name == "Event" ? string.Empty : GetSpecialMethod() + GetHeader() + GetBody();
        }

        private string GetHeader()
        {
            return string.Format("{0}{1}{2} {3}{4}({5}){6}{7}",
                _fromInterface ? string.Empty : "public ",
                _isSpecialCase ? "override " : string.Empty,
                _type.ToTypeString(false),
                _isSpecialCase ? _name : _name.ToPythonista(),
                _isGeneric ? "<T>" : string.Empty,
                string.Join(", ", _parameters),
                GetGenericArgumentString(),
                _fromInterface ? ";" : Environment.NewLine);
        }

        private string GetBody()
        {
            if (_fromInterface) return string.Empty;

            return string.Format("{0}{1} {2}{3}({4});{5}",
                "{" + Environment.NewLine,
                _type == typeof(void) ? string.Empty : "return",
                _isSpecialCase ? _name.ToPythonista() : _name,
                _isGeneric ? "<T>" : string.Empty,
                string.Join(", ", _parameters.Select(x => x.ToShortString())),
                Environment.NewLine + "}");
        }

        private string GetSpecialMethod()
        {
            if (!_isSpecialCase) return string.Empty;

            return string.Format("public virtual {0} {1}{2}({3}){4}{5}",
                _type.ToTypeString(false),
                _name.ToPythonista(),
                _isGeneric ? "<T>" : string.Empty,
                string.Join(", ", _parameters),
                GetGenericArgumentString(),
                "{ }" + Environment.NewLine);
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
        private bool _fromInterface;

        public Property(PropertyInfo propertyInfo)
        {
            _name = propertyInfo.Name;
            _fromInterface = propertyInfo.DeclaringType.IsInterface;
            Type = propertyInfo.PropertyType;
        }

        public override string ToString()
        {
            return string.Format("{0}{2} {3} {1}",
                _fromInterface ? string.Empty : "public ",
                _fromInterface ? "{ get; }" : "{ get { return " + _name + "; } }",
                Type.ToTypeString(false),
                _name.ToPythonista());
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
