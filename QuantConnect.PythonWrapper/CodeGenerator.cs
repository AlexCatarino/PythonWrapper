using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.PythonWrapper
{
    public class CodeGenerator
    {
        private Type _type;
        private string _filename;
        private StringBuilder _wrapperContent;

        public CodeGenerator(Type type)
        {
            _type = type;
            _filename = "Py" + type.Name + ".cs";
            _wrapperContent = new StringBuilder();
        }

        public void Run()
        {
            ProcessProperties();
            ProcessMethods();
            
            File.WriteAllText(_filename, _wrapperContent.ToString());
            Console.WriteLine(_filename + " was created.");
        }

        private void ProcessProperties()
        {
            var properties = _type.GetProperties().Where(x => x.DeclaringType == _type);

            foreach (var property in properties)
            {
                if (property.PropertyType.Namespace.StartsWith("System"))
                {
                    var systemStringBuider = new StringBuilder("public ");
                    systemStringBuider.Append(GetTypeString(property.PropertyType) + " ");
                    systemStringBuider.Append(GetPythonistaName(property.Name));
                    systemStringBuider.Append("{ get { return ");
                    systemStringBuider.Append(property.Name);
                    systemStringBuider.Append("; } }");
                    _wrapperContent.AppendLine(systemStringBuider.ToString());
                }
                else
                {
                    //var type = property.PropertyType;
                    //new CodeGenerator(type).Run();
                }
            }
        }

        private void ProcessMethods()
        {
            var methods = _type.GetMethods().Where(x => x.DeclaringType == _type && !x.Name.Contains("et_"));

            foreach (var method in methods)
            {
                var bodyParameters = new StringBuilder();
                var headerParameters = new StringBuilder();

                var parameters = method.GetParameters();

                foreach (var parameter in parameters)
                {
                    var type = parameter.ParameterType;

                    headerParameters.Append(GetTypeString(type) + " ");
                    headerParameters.Append(GetPythonistaName(parameter.Name));
                    bodyParameters.Append(GetPythonistaName(parameter.Name));

                    if (parameter.HasDefaultValue)
                    {
                        var defaultValue =
                            parameter.DefaultValue == null ? "null" :
                            parameter.DefaultValue.GetType() == typeof(string) ? "\"" + parameter.DefaultValue.ToString() + "\"" :
                            parameter.DefaultValue.ToString();

                        if (parameter.ParameterType.IsEnum)
                        {
                            defaultValue = parameter.ParameterType.Name + "." + defaultValue;
                        }
                        if (parameter.ParameterType == typeof(decimal))
                        {
                            defaultValue += "m";
                        }
                        if (parameter.ParameterType == typeof(bool))
                        {
                            defaultValue = defaultValue.ToLower();
                        }
                        headerParameters.Append(" = " + defaultValue);
                    }

                    bodyParameters.Append(", ");
                    headerParameters.Append(", ");
                }

                if (parameters.Count() > 0)
                {
                    bodyParameters.Remove(bodyParameters.Length - 2, 2);
                    headerParameters.Remove(headerParameters.Length - 2, 2);
                }

                string events = null;
                var header = "public " + GetTypeString(method.ReturnType) + " ";
                var body = method.ReturnType == typeof(void) ? "\t" : "\treturn ";

                var pythonLine = GetPythonistaName(method.Name) + (method.IsGenericMethod ? "<T>({0})" : "({0})");
                var csharpLine = method.Name + (method.IsGenericMethod ? "<T>({0})" : "({0})");

                if (method.Name == "Initialize" || method.Name.StartsWith("On"))
                {
                    header = header.Replace("public", "public override");
                    events = header.Replace("override", "virtual") + GetPythonistaName(method.Name) + "(" + headerParameters.ToString() + ")\r\n{\r\n}";
                    header += csharpLine;
                    body += pythonLine;
                }
                else
                {
                    header += pythonLine;
                    body += csharpLine;
                }

                if (method.IsGenericMethod)
                {
                    var genericArgumentsString = "";
                    var genericArguments = method.GetGenericArguments();
                    foreach (var genericArgument in genericArguments)
                    {
                        if (genericArgument.BaseType != typeof(Object))
                        {
                            genericArgumentsString += genericArgument.BaseType.Name + ", ";
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(genericArgumentsString))
                    {
                        header += string.Format(" where T : {0} new()", genericArgumentsString);
                    }
                }

                header = string.Format(header, headerParameters.ToString());
                body = string.Format(body + ";", bodyParameters.ToString());

                if (!string.IsNullOrEmpty(events))
                {
                    _wrapperContent.AppendLine(events);
                }

                _wrapperContent.AppendLine(header);
                _wrapperContent.AppendLine("{");
                _wrapperContent.AppendLine(body);
                _wrapperContent.AppendLine("}");
            }
        }

        private string GetTypeString(Type type)
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
                    typeStringBuilder.Append(GetTypeString(genericArgument) + ", ");
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
                { typeof(void), "void" },
                { typeof(short), "short" },
                { typeof(int), "int" },
                { typeof(long), "long" },
                { typeof(double), "double" },
                { typeof(decimal), "decimal" },
                { typeof(string), "string" },
                { typeof(bool), "bool" },
            };

            if (systemObjects.ContainsKey(type))
            {
                return systemObjects[type];
            }
            else
            {
                return type.Name;
            }
        }

        /// <summary>
        /// Converts name from C# style to Pythonista
        /// </summary>
        /// <param name="name">Name in C# style</param>
        /// <returns>string with the converted name</returns>
        private static string GetPythonistaName(string name)
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
                if (char.IsUpper(name[i]) && i > 0)
                {
                    outname.Append("_");
                }
                outname.Append(lowered[i]);
            }

            return outname.ToString();
        }
    }
}
