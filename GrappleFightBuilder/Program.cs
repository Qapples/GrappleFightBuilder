using System;
using System.IO;
using System.Linq;
using GrappleFightNET5;

namespace GrappleFightBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] fileContents = args[..^1].Select(File.ReadAllText).ToArray();
            string outPath = args.Last();

            ScriptAssemblyBuilder scriptBuilder = new(null, null, null, fileContents);

            Console.WriteLine($"Building the following scripts: {string.Join(' ', args[..^1])}");
            var results = scriptBuilder.CompileIntoAssembly(outPath);
            Console.WriteLine($"Output .dll to file path: {Path.GetFullPath(outPath)}");
            Console.WriteLine(
                $"Diagnostic results:\n{string.Join("\n\n", results.Select(e => e.GetMessage()).ToArray())}");
        }
    }
}