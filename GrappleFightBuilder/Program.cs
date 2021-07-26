using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GrappleFightBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            var (scriptDirectory, scriptOutput) = (@"Scripts/", "GrappleFightScripts.dll");
            var (systemDirectory, systemOutput) = (@"Systems/", "GrappleFightSystems.dll");

            foreach (string arg in args)
            {
                if (arg.StartsWith("--script_directory=")) scriptDirectory = arg[(arg.IndexOf('=') + 1)..];
                if (arg.StartsWith("--system_directory=")) systemDirectory = arg[(arg.IndexOf('=') + 1)..];
                
                if (arg.StartsWith("--script_output=")) scriptOutput = arg[(arg.IndexOf('=') + 1)..];
                if (arg.StartsWith("--system_output=")) systemOutput = arg[(arg.IndexOf('=') + 1)..];
            }

            string[] scriptContents = Directory.GetFiles(scriptDirectory).Select(File.ReadAllText).ToArray();
            string[] systemContents = Directory.GetFiles(systemDirectory).Select(File.ReadAllText).ToArray();

            Console.WriteLine("First building scripts, then building systems.");

            //Build scripts
            ScriptAssemblyBuilder scriptBuilder = new(null, null, null, scriptContents);

            Console.WriteLine(
                $"Building the following scripts: {string.Join('\n', Directory.GetFiles(scriptDirectory))}");
            var scriptResults = scriptBuilder.CompileIntoAssembly(scriptOutput, "GrappleFightScripts");
            
            Console.WriteLine(scriptResults.Any(e => e.Severity == DiagnosticSeverity.Error)
                ? "Building scripts failed!"
                : $"Output .dll to file path: {Path.GetFullPath(scriptOutput)}");
            Console.WriteLine(
                $"Diagnostic results:\n{string.Join("\n", scriptResults.Select(e => e.GetMessage()).ToArray())}\n");

            //Build systems
            SystemAssemblyBuilder systemBuilder = new(null, null, null, systemContents);
            
            Console.WriteLine(
                $"Building the following scripts: {string.Join('\n', Directory.GetFiles(systemDirectory))}");
            var systemResults = systemBuilder.CompileIntoAssembly(systemOutput, "GrappleFightSystems");
            
            Console.WriteLine(systemResults.Any(e => e.Severity == DiagnosticSeverity.Error)
                ? "Building scripts failed!"
                : $"Output .dll to file path: {Path.GetFullPath(systemOutput)}");
            Console.WriteLine(
                $"Diagnostic results:\n{string.Join("\n", systemResults.Select(e => e.GetMessage()).ToArray())}\n");
        }
    }
}