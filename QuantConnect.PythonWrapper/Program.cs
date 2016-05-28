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
            var codeGenerator = new CodeGenerator(typeof(QCAlgorithm));

            File.WriteAllText("PyQCAlgorithm.cs",
                string.Format("{2}{0}{0}namespace QuantConnect.Algorithm{0} {{ {0}{1}{0} }}",
                    Environment.NewLine,
                    codeGenerator.ToString(),
                    File.ReadAllText("UsingDeclarations.txt")));
            
            Console.Write("Press any key to exit.");
            Console.ReadKey();
        }        
    }
}