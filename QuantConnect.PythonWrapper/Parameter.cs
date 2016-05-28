using System;
using System.Reflection;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// Parameter class: 
    /// Generate code for ParameterInfo object
    /// </summary>
    public class Parameter
    {
        private bool _isOut;
        private string _defaultValue;
        public Type Type;
        public string Name;

        /// <summary>
        /// Parameter Class Constructor:
        /// Generates code for a parameter of the required method/indexer
        /// </summary>
        /// <param name="parameterInfo">Information about a parameter of a required method/indexer</param>
        public Parameter(ParameterInfo parameterInfo)
        {
            _isOut = parameterInfo.IsOut;
            Name = parameterInfo.Name;
            Type = parameterInfo.ParameterType;
            GetDefaultValueString(parameterInfo.DefaultValue, parameterInfo.HasDefaultValue);
        }

        public override string ToString()
        {
            return Type.ToTypeString(_isOut) + " " + Name.ToPythonista() + _defaultValue;
        }

        /// <summary>
        /// Converts to string the parameter name in pythonista convention
        /// </summary>
        /// <returns>String of parameter name in pythonista convention</returns>
        public string ToShortString()
        {
            return (_isOut ? "out " : "") + Name.ToPythonista();
        }

        /// <summary>
        /// Transforms a default value from a parameter into a string
        /// </summary>
        /// <param name="defaultValue">Default value object</param>
        /// <param name="hasDefaultValue">Test to continue conversion</param>
        private void GetDefaultValueString(object defaultValue, bool hasDefaultValue)
        {
            _defaultValue = string.Empty;

            if (hasDefaultValue)
            {
                _defaultValue =
                    defaultValue == null ? "null" :
                    defaultValue.GetType() == typeof(string) ? "\"" + defaultValue.ToString() + "\"" :
                    defaultValue.ToString();

                if (Type.IsEnum)
                {
                    _defaultValue = Type.Name + "." + defaultValue;
                }
                if (Type == typeof(decimal))
                {
                    _defaultValue += "m";
                }
                if (Type == typeof(bool))
                {
                    _defaultValue = _defaultValue.ToLower();
                }
                _defaultValue = " = " + _defaultValue;
            }
        }
    }
}