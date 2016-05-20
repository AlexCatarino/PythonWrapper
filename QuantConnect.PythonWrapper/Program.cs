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
            File.WriteAllText("Python.cs", x.ToString());
            
            Console.Write("Press any key to exit.");
            Console.ReadKey();
        }        
    }
}