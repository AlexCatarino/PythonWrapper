using QuantConnect.Algorithm;
using System;
using System.IO;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// Generated code with pythonista convention for QuantConnect.Algorithm.QCAlgorithm
    /// </summary>
    class Program
    {
        /// <summary>
        /// Main(): Program entry point
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var x = new CodeGenerator(typeof(QCAlgorithm));

            File.WriteAllText("Python.cs", 
                string.Format("namespace QuantConnect.Algorithm{0} {{ {0}{1}{0} }}",
                    Environment.NewLine,
                    x.ToString()));
            
            Console.Write("Press any key to exit.");
            Console.ReadKey();
        }        
    }
}