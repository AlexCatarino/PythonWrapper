using QuantConnect.Algorithm;
using System;
using System.Linq;
using System.Reflection;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// Property class: 
    /// Generate code for PropertyInfo object
    /// </summary>
    public class Property
    {
        public Type Type;
        private string _name;
        private string _refString;
        private bool _fromInterface;
        private Parameter _indexer;

        /// <summary>
        /// Property Class Constructor:
        /// Generates code for a property of the required type
        /// </summary>
        /// <param name="propertyInfo">Information about a property of a required type</param>
        public Property(PropertyInfo propertyInfo)
        {
            Type = propertyInfo.PropertyType;
            _name = propertyInfo.Name;
            _fromInterface = propertyInfo.DeclaringType.IsInterface;
            _refString = propertyInfo.DeclaringType == typeof(QCAlgorithm) ? string.Empty : "_obj.";
            
            // Is indexer?
            var indexParameters = propertyInfo.GetIndexParameters();
            if (indexParameters.Count() > 0)
            {
                _indexer = new Parameter(indexParameters[0]);
            }
        }
        
        public override string ToString()
        {
            if (_fromInterface)
            {
                return string.Format(
                    "public {1} {2} {{ get {{ throw new NotImplementedException(); }} }} {0}" +
                    "public {1} {3} {{ get {{ return {2}; }} }}",
                    Environment.NewLine,
                    Type.ToTypeString(false),
                    _name.ToPythonista(),
                    _name);

            }
            else if (_indexer != null)
            {
                return string.Format("public Py{0} this[{1}] {{ get {{ return new Py{0}(_obj[{2}]); }} }}",
                     Type.ToTypeString(false).Split('.').Last(),
                    _indexer,
                    _indexer.Name);
            }
            else if (Type.FullName.StartsWith("QuantConnect"))
            {
                return string.Format("public Py{0} {1};",
                    Type.ToTypeString(false).Substring(Type.IsInterface ? 1 : 0).Split('.').Last(),
                    _name.ToPythonista());
            }
            else
            {
                return string.Format("public {0} {2} {{ get {{ return {1}{3}; }} }}",
                    Type.ToTypeString(false),
                    _refString,
                    _name.ToPythonista(),
                    _name);
            }

        }

        /// <summary>
        /// Generated code line for constructor body
        /// </summary>
        /// <returns>String with code line for constructor body</returns>
        public string ToConstructorBodyString()
        {
            if (Type.FullName.StartsWith("QuantConnect") && _indexer == null)
            {
                return string.Format("{2} = new Py{0}({1}{3});",
                    Type.ToTypeString(false).Substring(Type.IsInterface ? 1 : 0).Split('.').Last(),
                    _refString,
                    _name.ToPythonista(),
                    _name);
            }

            return string.Empty;
        }
    }
}