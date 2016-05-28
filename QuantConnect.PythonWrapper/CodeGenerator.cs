using QuantConnect.Algorithm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// CodeGenerator Class:
    /// Generates code from provided type and, recursively, from types that are found in its properties.
    /// </summary>
    public class CodeGenerator
    {
        private Type _type;
        private Constructor Constructor;
        private IEnumerable<Field> Fields;
        private IEnumerable<Method> Methods;
        private IEnumerable<Property> Properties;
        private IEnumerable<CodeGenerator> Children;

        /// <summary>
        /// <see cref="CodeGenerator(Type)"/> Class Constructor:
        /// Generates code from provided type and, recursively, from types that are found in its properties.
        /// </summary>
        /// <param name="type">Type to generate code from</param>
        public CodeGenerator(Type type)
        {
            _type = type;
            
            var allowedTypes = new List<Type> { type };
            var fields = type.GetFields().ToList();
            var properties = type.GetProperties().ToList();
            var methods = type.GetMethods().ToList();

            if (type.IsInterface)
            {
                foreach (var iinterface in type.GetInterfaces())
                {
                    allowedTypes.Add(iinterface);
                    fields.AddRange(iinterface.GetFields());
                    properties.AddRange(iinterface.GetProperties());
                    methods.AddRange(iinterface.GetMethods());
                }
            }

            var setMethods = methods
                .Where(x => x.Name.StartsWith("Set"))
                .Select(x => x.Name.Substring(3)).Distinct();
          
            Fields = fields
                .Where(x => allowedTypes.Contains(x.DeclaringType) && x.IsPublic)
                .Select(x => new Field(x));

            Properties = properties.OrderBy(x => x.PropertyType.Name)
                .Where(x => allowedTypes.Contains(x.DeclaringType) && !x.PropertyType.IsEnum && !setMethods.Contains(x.Name))
                .Select(x => new Property(x));

            Methods = methods.OrderBy(x => x.ReturnType.Name)
                .Where(x => allowedTypes.Contains(x.DeclaringType) && !x.Name.Contains("et_") && !x.Name.Contains("op_") && !x.Name.Contains("CollectionChanged"))
                .Select(x => new Method(x));

            Constructor = new Constructor(type, Fields, Properties);

            Children = Properties
                    .Where(x => x.Type.Namespace.StartsWith("QuantConnect"))
                    .Select(x => new CodeGenerator(x.Type))
                    // Flattens the nodes
                    .Map(p => true, (CodeGenerator n) => { return n.Children; })
                    // Does not consider the grandchildren
                    .Select(x => new CodeGenerator(x._type, x.Constructor, x.Fields, x.Methods, x.Properties))
                    // Does not pass repeated children
                    .DistinctBy(x => x._type).OrderBy(x => x._type.Name);
        }

        /// <summary>
        /// <see cref="CodeGenerator(Type, Constructor, IEnumerable{Field}, IEnumerable{Method}, IEnumerable{Properties})"/> Class Constructor:
        /// Generates code from provided type 
        /// </summary>
        /// <param name="type">Type to generate code from</param>
        /// <param name="constructor">Constructor property of previously calculated CodeGenerator</param>
        /// <param name="fields">IEnumerable of fields property of previously calculated CodeGenerator</param>
        /// <param name="methods">IEnumerable of methods property of previously calculated CodeGenerator</param>
        /// <param name="properties">IEnumerable of properties property of previously calculated CodeGenerator</param>
        public CodeGenerator(Type type, Constructor constructor, IEnumerable<Field> fields, IEnumerable<Method> methods, IEnumerable<Property> properties)
        {
            _type = type;
            Constructor = constructor;
            Fields = fields;
            Methods = methods;
            Properties = properties;
            Children = Enumerable.Empty<CodeGenerator>();
        }

        public override string ToString()
        {
            var header = string.Format("public {0} Py{1}",
                _type.IsValueType ? "struct" : "class",
                _type.IsInterface ? _type.Name.Substring(1) : _type.Name);

            if (_type == typeof(QCAlgorithm) || _type.IsInterface)
            {
                header += " : " + _type.Name;
            }

            return header + Environment.NewLine +
                "{" + Environment.NewLine +
                    string.Join(Environment.NewLine, Fields) +
                    (Fields.Count() > 0 ? Environment.NewLine + Environment.NewLine : "") +
                    string.Join(Environment.NewLine, Properties) +
                    (Properties.Count() > 0 ? Environment.NewLine + Environment.NewLine : "") +
                    Constructor + Environment.NewLine + 
                    string.Join(Environment.NewLine, Methods) +
                    (Methods.Count() > 0 ? Environment.NewLine : "") +
                "}" + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, Children);
        }
    }
}