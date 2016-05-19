using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using QuantConnect.Algorithm;
using QuantConnect.Securities;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// Gets the methods from QuantConnect.Algorithm.QCAlgorithm and wrap them into a python file
    /// </summary>
    class Program
    {
        private static Type _type;
        private static StringBuilder _wrapperContent;
        private static StringBuilder _templateContent;
   
        /// <summary>
        /// Main()
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var assemblyPath = "QuantConnect.Algorithm.dll";
            Console.WriteLine("Main(): Loading QCAlgorithm assembly: " + assemblyPath);
            _type = Assembly.LoadFile(Path.GetFullPath(assemblyPath)).GetType("QuantConnect.Algorithm.QCAlgorithm");

            new CodeGenerator(_type).Run();

            Console.Write("Press any key to exit.");
            Console.ReadKey();
        }

        //private static void CreateSecurityPortfolioManager()
        //{
        //    var assemblyPath = "QuantConnect.Common.dll";
        //    Console.WriteLine("CreateSecurityPortfolioManager(): Loading QuantConnect assembly: " + assemblyPath);
        //    var type = Assembly.LoadFile(Path.GetFullPath(assemblyPath)).GetType("QuantConnect.Securities.SecurityPortfolioManager");
        //    var properties = type.GetProperties().Where(x =>
        //    {
        //        return x.DeclaringType == typeof(SecurityPortfolioManager);
        //    }).OrderBy(x => x.Name);
        //    var groupedMethods = type.GetMethods().OrderBy(x => x.Name).Where(x =>
        //    {
        //        if (x.DeclaringType != typeof(SecurityPortfolioManager)) return false;
        //        if (x.IsPrivate) return false;
        //        if (x.Name.Contains("et_")) return false;
        //        if (x.CustomAttributes.Count() == 0) return true;
        //        return !x.CustomAttributes.Any(a => a.AttributeType == typeof(ObsoleteAttribute));
        //    }).GroupBy(x => x.Name);

        //    var pyContent = new StringBuilder();
        //    pyContent.AppendLine("from clr import AddReference");
        //    pyContent.AppendLine("AddReference(\"System\")");
        //    pyContent.AppendLine("AddReference(\"QuantConnect.Common\")");
        //    pyContent.AppendLine("from System import *");
        //    pyContent.AppendLine("from QuantConnect import *");
        //    pyContent.AppendLine("from QuantConnect.Securities import *");
        //    pyContent.AppendLine();
        //    pyContent.AppendLine();
        //    pyContent.AppendLine("class PySecurityPortfolioManager(SecurityPortfolioManager):");
        //    pyContent.AppendLine("    def __init__(self, security_manager, transactions):");

        //    foreach (var property in properties)
        //    {
        //        pyContent.AppendLine(string.Format("        self.{0} = self.{1}", GetPythonistaName(property.Name), property.Name));
        //    }

        //    pyContent.AppendLine();

        //    foreach (var methods in groupedMethods)
        //    {
        //        IEnumerable<string> decimalParameters = null;
        //        var extremeMethods = GetExtremeMethod(methods, out decimalParameters);
        //        var line = extremeMethods.Replace(")", "").Split('(');

        //        if (line[0] == "SetCash")
        //        {
        //            line[1] = line[1].Replace("symbol=\"\"", "symbol=\"USD\"");
        //        }

        //        pyContent.Append("    def " + GetPythonistaName(line[0]) + "(self");
        //        pyContent.AppendLine((line[1].Length > 0 ? ", " : "") + OrderOptionals(line[1]) + "):");

        //        foreach (var decimalParameter in decimalParameters)
        //        {
        //            line[1] = line[1].Replace(decimalParameter, "Decimal(" + decimalParameter + ")");
        //        }

        //        pyContent.AppendLine("        self." + line[0] + "(" + RemoveDefaultValues(line[1]) + ")");
        //        pyContent.AppendLine();
        //    }

        //    File.WriteAllText("pysecurityportfoliomanager.py", pyContent.ToString());
        //}

        //private static void AppendTemplateMethods()
        //{
        //    var groupMethods = _type.GetMethods().OrderBy(x => x.Name).Where(x =>
        //    {
        //        if (x.DeclaringType != typeof(QCAlgorithm)) return false;

        //        if (x.Name == "Initialize" || x.Name.Substring(0, 2) == "On")
        //        {
        //            if (x.CustomAttributes.Count() == 0) return true;
        //            return !x.CustomAttributes.Any(a => a.AttributeType == typeof(ObsoleteAttribute));
        //        }
        //        else
        //        {
        //            return false;
        //        }

        //    }).GroupBy(x => x.Name);

        //    foreach (var methods in groupMethods)
        //    {
        //        var voidReturn = "";
        //        var allParameters = new List<ParameterInfo>();

        //        // Gets most complex method
        //        var mostComplex = new List<ParameterInfo>();

        //        foreach (var method in methods)
        //        {
        //            var parameters = method.GetParameters().ToList();

        //            if (parameters.Count > mostComplex.Count || parameters.Count(x => x.HasDefaultValue) > mostComplex.Count(x => x.HasDefaultValue))
        //            {
        //                mostComplex = parameters;
        //            }

        //            allParameters.AddRange(parameters);
        //        }

        //        // Gets least complex method
        //        var leastComplex = mostComplex;

        //        foreach (var method in methods)
        //        {
        //            var parameters = method.GetParameters().ToList();

        //            if (parameters.Count < leastComplex.Count)
        //            {
        //                leastComplex = parameters;
        //            }

        //            voidReturn = method.ReturnType == typeof(void) ? "" : "return";
        //        }

        //        var arguments = "";

        //        mostComplex.ForEach(x =>
        //        {
        //            arguments += GetPythonistaName(x.Name);

        //            var y = leastComplex.FirstOrDefault(l => l.Name == x.Name);

        //            if (x.HasDefaultValue)
        //            {
        //                var value = x.DefaultValue == null ? "None" : x.DefaultValue.ToString();
        //                if (x.ParameterType.IsEnum)
        //                {
        //                    value = x.ParameterType.Name + "." + value;
        //                }
        //                else if (x.Name == "market")
        //                {
        //                    value = value == "fxcm" ? "Market.FXCM" : "Market.USA";
        //                }
        //                else if (x.ParameterType == typeof(string))
        //                {
        //                    value = "\"" + value + "\"";
        //                }
        //                arguments += "=" + value;
        //            }
        //            else
        //            {
        //                if (y == null)
        //                {
        //                    arguments += "=" + TypeDefaultString(x);
        //                }
        //                else if (y.HasDefaultValue)
        //                {
        //                    var value = y.DefaultValue == null ? "None" : y.DefaultValue;
        //                    if (y.ParameterType.IsEnum) value = y.ParameterType.Name + "." + value;
        //                    if (y.ParameterType == typeof(string)) value = "\"" + value + "\"";
        //                    arguments += "=" + value;
        //                }
        //            }
        //            arguments += ", ";
        //        });

        //        var startIndex = 0;
        //        if ((startIndex = arguments.LastIndexOf(", ")) > 1)
        //        {
        //            arguments = arguments.Remove(startIndex);
        //        }

        //        arguments = arguments.Length == 0 ? "self" : "self, " + arguments;
                
        //        _templateContent.AppendLine(String.Format("    def {0}({1}):", methods.Key, arguments));
        //        _templateContent.AppendLine(String.Format("        if \"{0}\" in self.authorized:", GetPythonistaName(methods.Key), OrderOptionals(arguments)));
        //        _templateContent.AppendLine(String.Format("            algorithm.{0}({1})", GetPythonistaName(methods.Key), RemoveDefaultValues(arguments)));
        //        _templateContent.AppendLine();
        //    }
        //}

        /// <summary>
        /// Order methods arguments and puts mandatory arguments before optional ones 
        /// </summary>
        /// <param name="value">Arguments in their current order</param>
        /// <returns>Reodered arguments</returns>
        //private static string OrderOptionals(string value)
        //{
        //    if (value.Contains("="))
        //    {
        //        var array = value.Split(',');
        //        var nonoptionals = string.Join(",", array.Where(x => !x.Contains("="))).Trim();
        //        var optionals = string.Join(",", array.Where(x => x.Contains("="))).Trim();
        //        return (nonoptionals.Length > 0 ? nonoptionals + ", " : "") + optionals;
        //    }

        //    return value;
        //}

        /// <summary>
        /// Removes default values
        /// </summary>
        /// <param name="value">Arguments with their default values</param>
        /// <returns>Arguments without default values</returns>
        //private static string RemoveDefaultValues(string value)
        //{
        //    while (value.Contains("="))
        //    {
        //        var startIndex = value.IndexOf("=");
        //        var length = value.IndexOf(",", startIndex) - startIndex;
        //        var waste = length > 0 ? value.Substring(startIndex, length) : value.Substring(startIndex);
        //        value = value.Replace(waste, "");
        //    }
        //    return value;
        //}

        /// <summary>
        /// Defines default values when there are not.
        /// </summary>
        /// <param name="info">Paramenters informamation</param>
        /// <returns>Arguments with newly assigned default values</returns>
        //private static string TypeDefaultString(ParameterInfo info)
        //{
        //    var name = info.Name.ToLower();
        //    var type = info.ParameterType;

        //    switch (name)
        //    {
        //        case "market":
        //            return "Market.USA";
        //        case "resolution":
        //            return "Resolution.Minute";
        //        case "day":
        //            return "1";
        //        case "month":
        //            return "1";
        //        case "year":
        //            return "1998";
        //        default:
        //            break;
        //    }

        //    if (type.Namespace == "System")
        //    {
        //        if (type.Name == "String") return "\"\"";
        //        if (type.Name == "Func`2") return "None";
        //        if (type.Name == "DateTime") return "DateTime.Now";
        //        return "0";
        //    }

        //    if (type.Name == "SecurityType")
        //    {
        //        return "SecurityType.Equity";
        //    }

        //    if (type.Name == "Resolution")
        //    {
        //        return "Resolution.Minute";
        //    }

        //    return "None";
        //}

        
    }
}