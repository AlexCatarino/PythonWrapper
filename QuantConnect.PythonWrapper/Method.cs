using QuantConnect.Algorithm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// Method class:
    /// Generate code for MethodInfo object
    /// </summary>
    public class Method
    {
        private Type _type;
        private bool _isGeneric;
        private bool _isSpecialCase;
        private bool _fromInterface;
        private bool _fromQCAlgorithm;
        private string _name;
        private string _refString;
        private string _genericArgumentsString;
        private IEnumerable<Parameter> _parameters;
        
        /// <summary>
        /// Method Class Constructor:
        /// Generates code for a method of the required type
        /// </summary>
        /// <param name="methodInfo">Information about a method of a required type</param>
        public Method(MethodInfo methodInfo)
        {
            _name = methodInfo.Name;
            _type = methodInfo.ReturnType;
            _isGeneric = methodInfo.IsGenericMethod;
            GetGenericArgumentString(methodInfo.GetGenericArguments());

            var declaringType = methodInfo.DeclaringType;
            _fromInterface = declaringType.IsInterface;
            _fromQCAlgorithm = declaringType == typeof(QCAlgorithm);
            _isSpecialCase = _fromInterface || (_fromQCAlgorithm && (_name == "Initialize" || _name.StartsWith("On")));
            _parameters = methodInfo.GetParameters().Select(x => new Parameter(x));            
            _refString = _fromQCAlgorithm || _fromInterface ? string.Empty
                : methodInfo.IsStatic ? declaringType.ToTypeString(false) + "."
                : "_obj.";
        }

        public override string ToString()
        {
            // event is a C# reserved work, we cannot use it.            
            if (_name == "Event") return string.Empty;

            return string.Format(
                "public {4}{6} {2}{1}({9}){7}{0}" +
                "{{ {0}" +
                "\t{5} {8}{3}{1}({10});{0}" +
                "}}{0}{11}",
                Environment.NewLine,
                _isGeneric ? "<T>" : string.Empty,
                _isSpecialCase ? _name : _name.ToPythonista(),
                _isSpecialCase ? _name.ToPythonista() : _name,
                _isSpecialCase && !_fromInterface ? "override " : string.Empty,
                _type == typeof(void) ? string.Empty : "return",
                _type.ToTypeString(false),
                _genericArgumentsString,
                _refString,
                string.Join(", ", _parameters),
                string.Join(", ", _parameters.Select(x => x.ToShortString())),
                GetSpecialMethod());
        }

        /// <summary>
        /// In special cases, virtual methods need to be created
        /// </summary>
        /// <returns>String with virtual method</returns>
        private string GetSpecialMethod()
        {
            if (!_isSpecialCase) return string.Empty;

            return string.Format("public virtual {0} {1}{2}({3}){4}{{ {5} }}" + Environment.NewLine,
                _type.ToTypeString(false),
                _name.ToPythonista(),
                _isGeneric ? "<T>" : string.Empty,
                string.Join(", ", _parameters),
                _genericArgumentsString,
                _fromQCAlgorithm ? string.Empty : "throw new NotImplementedException();");
        }

        /// <summary>
        /// Transforms Array of Type into String
        /// </summary>
        /// <param name="types">Array of Type to be converted into String</param>
        private void GetGenericArgumentString(Type[] types)
        {
            _genericArgumentsString = string.Empty;

            if (_isGeneric)
            {
                foreach (var type in types)
                {
                    if (type.BaseType != typeof(Object))
                    {
                        _genericArgumentsString += type.BaseType.Name + ", ";
                    }
                }

                if (!string.IsNullOrWhiteSpace(_genericArgumentsString))
                {
                    _genericArgumentsString = string.Format(" where T : {0} new()", _genericArgumentsString);
                }
            }
        }
    }
}