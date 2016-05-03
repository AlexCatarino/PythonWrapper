using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// Gets the methods from QuantConnect.Algorithm.QCAlgorithm and wrap them into a python file
    /// </summary>
    class Program
    {
        private static Type _type;
        private static StringBuilder _pyFileContent;

        /// <summary>
        /// Main()
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var assemblyPath = "QuantConnect.Algorithm.dll";
            Console.WriteLine("Main(): Loading QCAlgorithm assembly: " + assemblyPath);
            _type = Assembly.LoadFile(Path.GetFullPath(assemblyPath)).GetType("QuantConnect.Algorithm.QCAlgorithm");

            _pyFileContent = new StringBuilder();

            AppendHeader();
            AppendProperties();
            AppendMethods();

            Console.WriteLine(_pyFileContent.ToString());
            File.WriteAllText("wrapper.py", _pyFileContent.ToString());
            Console.Write("wrapper.py was created.\r\nPress any key to exit.");
            Console.ReadKey();
        }

        /// <summary>
        /// Appends header information 
        /// </summary>
        private static void AppendHeader()
        {
            _pyFileContent.AppendLine("from datetime import datetime");
            _pyFileContent.AppendLine("from clr import AddReference");
            _pyFileContent.AppendLine("AddReference(\"System\")");
            _pyFileContent.AppendLine("AddReference(\"QuantConnect.Common\")");
            _pyFileContent.AppendLine("AddReference(\"QuantConnect.Indicators\")");
            _pyFileContent.AppendLine("from System import *");
            _pyFileContent.AppendLine("from QuantConnect import *");
            _pyFileContent.AppendLine("from QuantConnect.Indicators import *");
            _pyFileContent.AppendLine();
            _pyFileContent.AppendLine("class wrapper:");
            _pyFileContent.AppendLine("    reference = None");
            _pyFileContent.AppendLine();
        }

        /// <summary>
        /// Appends properties
        /// </summary>
        private static void AppendProperties()
        {
            var properties = _type.GetProperties().OrderBy(x => x.Name);

            foreach (var property in properties)
            {
                _pyFileContent.AppendLine("def " + GetPythonistaName(property.Name) + "():");
                _pyFileContent.Append("    return ");

                if (property.PropertyType.Namespace == "System")
                {
                    if (property.PropertyType == typeof(DateTime))
                    {
                        _pyFileContent.AppendLine("datetime(wrapper.reference." + property.Name + ")");
                    }
                    else if (property.PropertyType == typeof(string))
                    {
                        _pyFileContent.AppendLine("str(wrapper.reference." + property.Name + ")");
                    }
                    else if (property.PropertyType == typeof(bool))
                    {
                        _pyFileContent.AppendLine("bool(wrapper.reference." + property.Name + ")");
                    }
                    else
                    {
                        _pyFileContent.AppendLine("wrapper.reference." + property.Name);
                    }
                }
                else
                {
                    _pyFileContent.AppendLine("wrapper.reference." + property.Name);
                }

                _pyFileContent.AppendLine();
            }
        }

        /// <summary>
        /// Apends methods
        /// </summary>
        private static void AppendMethods()
        {
            var groupMethods = _type.GetMethods().OrderBy(x => x.Name).Where(x =>
            {
                if (x.Name.Contains("et_")) return false;
                if (x.Name == "Initialize") return false;
                if (x.Name == "PostInitialize") return false;
                if (x.Name.Substring(0, 2) == "On") return false;

                if (x.IsGenericMethod) return false;

                if (x.CustomAttributes.Count() == 0) return true;
                return !x.CustomAttributes.Any(a => a.AttributeType == typeof(ObsoleteAttribute));
            }).GroupBy(x => x.Name);

            foreach (var methods in groupMethods)
            {
                var voidReturn = "";
                var allParameters = new List<ParameterInfo>();

                // Gets most complex method
                var mostComplex = new List<ParameterInfo>();

                foreach (var method in methods)
                {
                    var parameters = method.GetParameters().ToList();

                    if (parameters.Count > mostComplex.Count || parameters.Count(x => x.HasDefaultValue) > mostComplex.Count(x => x.HasDefaultValue))
                    {
                        mostComplex = parameters;
                    }

                    allParameters.AddRange(parameters);
                }

                // Gets least complex method
                var leastComplex = mostComplex;

                foreach (var method in methods)
                {
                    var parameters = method.GetParameters().ToList();

                    if (parameters.Count < leastComplex.Count)
                    {
                        leastComplex = parameters;
                    }

                    voidReturn = method.ReturnType == typeof(void) ? "" : "return";
                }

                var arguments = "";
                
                mostComplex.ForEach(x =>
                {
                    arguments += GetPythonistaName(x.Name);

                    var y = leastComplex.FirstOrDefault(l => l.Name == x.Name);

                    if (x.HasDefaultValue)
                    {
                        var value = x.DefaultValue == null ? "None" : x.DefaultValue.ToString();
                        if (x.ParameterType.IsEnum)
                        {
                            value = x.ParameterType.Name + "." + value;
                        }
                        else if (x.Name == "market")
                        {
                            value = value == "fxcm" ? "Market.FXCM" : "Market.USA";
                        }
                        else if (x.ParameterType == typeof(string))
                        {
                            value = "\"" + value + "\"";
                        }
                        arguments += "=" + value;
                    }
                    else
                    {
                        if (y == null)
                        {
                            arguments += "=" + TypeDefaultString(x);
                        }
                        else if (y.HasDefaultValue)
                        {
                            var value = y.DefaultValue == null ? "None" : y.DefaultValue;
                            if (y.ParameterType.IsEnum) value = y.ParameterType.Name + "." + value;
                            if (y.ParameterType == typeof(string)) value = "\"" + value + "\"";
                            arguments += "=" + value;
                        }
                    }
                    arguments += ", ";
                });

                var startIndex = 0;
                if ((startIndex = arguments.LastIndexOf(", ")) > 1)
                {
                    arguments = arguments.Remove(startIndex);
                }

                _pyFileContent.AppendLine(String.Format("def {0}({1}):", GetPythonistaName(methods.Key), OrderOptionals(arguments)));
                
                allParameters.RemoveAll(x => x.ParameterType.Name != "Decimal");

                foreach (var item in allParameters)
                {
                    arguments = arguments.Replace(GetPythonistaName(item.Name), "Decimal(" + GetPythonistaName(item.Name) + ")");
                }

                _pyFileContent.AppendLine(String.Format("    {0} wrapper.reference.{1}({2})", voidReturn, methods.Key, RemoveDefaultValues(arguments)));
                _pyFileContent.AppendLine();
            }
        }

        /// <summary>
        /// Order methods arguments and puts mandatory arguments before optional ones 
        /// </summary>
        /// <param name="value">Arguments in their current order</param>
        /// <returns>Reodered arguments</returns>
        private static string OrderOptionals(string value)
        {
            if (value.Contains("="))
            {
                var array = value.Split(',');
                var nonoptionals = string.Join(",", array.Where(x => !x.Contains("="))).Trim();
                var optionals = string.Join(",", array.Where(x => x.Contains("="))).Trim();
                return (nonoptionals.Length > 0 ? nonoptionals + ", " : "") + optionals;
            }

            return value;
        }

        /// <summary>
        /// Removes default values
        /// </summary>
        /// <param name="value">Arguments with their default values</param>
        /// <returns>Arguments without default values</returns>
        private static string RemoveDefaultValues(string value)
        {
            while (value.Contains("="))
            {
                var startIndex = value.IndexOf("=");
                var length = value.IndexOf(",", startIndex) - startIndex;
                var waste = length > 0 ? value.Substring(startIndex, length) : value.Substring(startIndex);
                value = value.Replace(waste, "");
            }
            return value;
        }

        /// <summary>
        /// Defines default values when there are not.
        /// </summary>
        /// <param name="info">Paramenters informamation</param>
        /// <returns>Arguments with newly assigned default values</returns>
        private static string TypeDefaultString(ParameterInfo info)
        {
            var name = info.Name.ToLower();
            var type = info.ParameterType;

            switch (name)
            {
                case "market":
                    return "Market.USA";
                case "resolution":
                    return "Resolution.Minute";
                case "day":
                    return "1";
                case "month":
                    return "1";
                case "year":
                    return "1998";
                default:
                    break;
            }

            if (type.Namespace == "System")
            {
                if (type.Name == "String") return "\"\"";
                if (type.Name == "Func`2") return "None";
                if (type.Name == "DateTime") return "DateTime.Now";
                return "0";
            }

            if (type.Name == "SecurityType")
            {
                return "SecurityType.Equity";
            }

            if (type.Name == "Resolution")
            {
                return "Resolution.Minute";
            }

            return "None";
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