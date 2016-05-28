using QuantConnect.Algorithm;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// Constructor class: 
    /// Generate code for constructor object
    /// </summary>
    public class Constructor
    {
        private Type _type;
        private IEnumerable<Field> _fields;
        private IEnumerable<Property> _properties;

        /// <summary>
        /// Constructor Class Constructor:
        /// Generates code for the constructor of the required type
        /// </summary>
        /// <param name="type">Type to generate constructor code from</param>
        /// <param name="fields">Enumerable of fields property which are initialized in constructor body</param>
        /// <param name="properties">Enumerable of properties property which are initialized in constructor body</param>
        public Constructor(Type type, IEnumerable<Field> fields, IEnumerable<Property> properties)
        {
            _type = type;
            _fields = fields;
            _properties = properties.Where(x => x.Type.FullName.StartsWith("QuantConnect"));
        }

        public override string ToString()
        {
            // QCAlgorithm type is a special case, do not need obj parameter
            if (_type == typeof(QCAlgorithm))
            {
                return string.Format(
                    "public PyQCAlgorithm(){0}"+
                    "{{ {0}"+
                    "{1}{2}{0}" +
                    "}} {0}",
                    Environment.NewLine,
                    string.Join(Environment.NewLine + "\t", _fields.Select(x => x.ToConstructorBodyString())),
                    string.Join(Environment.NewLine + "\t", _properties.Select(x => x.ToConstructorBodyString())));
            }
            else
            {
                return string.Format(
                    "private {1} _obj;{0}{0}" +
                    "public Py{2}({1} obj){0}" +
                    "{{ {0}" +
                    "\t_obj = obj;{0} " +
                    "{3}{0}{4}" +
                    "}} {0}",
                    Environment.NewLine,
                    _type.ToTypeString(false),
                    _type.ToTypeString(false).Substring(_type.IsInterface ? 1 : 0).Split('.').Last(),
                    string.Join(Environment.NewLine + "\t", _fields.Select(x => x.ToConstructorBodyString())),
                    string.Join(Environment.NewLine + "\t", _properties.Select(x => x.ToConstructorBodyString())));
            }
        }
    }
}