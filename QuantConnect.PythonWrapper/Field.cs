using QuantConnect.Algorithm;
using System;
using System.Reflection;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// Field class: 
    /// Generate code for FieldInfo object
    /// </summary>
    public class Field
    {
        private Type _type;
        private string _name;
        private string _refString;

        /// <summary>
        /// Field Class Constructor:
        /// Generates code for a field of the required type
        /// </summary>
        /// <param name="fieldInfo">Information about a field of a required type</param>
        public Field(FieldInfo fieldInfo)
        {
            var declaringType = fieldInfo.DeclaringType;

            _name = fieldInfo.Name;
            _type = fieldInfo.FieldType;
            _refString = declaringType == typeof(QCAlgorithm) ? string.Empty
                : fieldInfo.IsStatic ? declaringType.ToTypeString(false) + "." 
                : "_obj.";
        }

        public override string ToString()
        {
            return string.Format("public {0} {1};", _type.ToTypeString(false), _name.ToPythonista());
        }

        /// <summary>
        /// Generated code line for constructor body
        /// </summary>
        /// <returns>String with code line for constructor body</returns>
        public string ToConstructorBodyString()
        {
            return string.Format("{1} = {0}{2};", _refString, _name.ToPythonista(), _name);
        }
    }
}
