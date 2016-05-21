using QuantConnect.Algorithm;
using System;
using System.IO;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// Gets the methods from QuantConnect.Algorithm.QCAlgorithm and wrap them into a python file
    /// </summary>
    class Program
    {
        /// <summary>
        /// Main()
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var x = new CodeGenerator(typeof(QCAlgorithm));

            var code = "namespace QuantConnect.PyAlgorithm" + Environment.NewLine +
                "{" + Environment.NewLine + x.ToString() + Environment.NewLine + "}";

            File.WriteAllText("Python.cs", code);
            
            Console.Write("Press any key to exit.");
            Console.ReadKey();
        }        
    }
}