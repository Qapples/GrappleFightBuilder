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
            var (scriptDirectory, scriptOutput) = (@"Scripts", "GrappleFightScripts.dll");
            var (sceneDirectory, sceneOutput) = (@"Scenes", "GrappleFightScenes.dll");

            foreach (string arg in args)
            {
                if (arg.StartsWith("--script_directory=")) scriptDirectory = arg[(arg.IndexOf('=') + 1)..];
                if (arg.StartsWith("--scene_directory=")) sceneDirectory = arg[(arg.IndexOf('=') + 1)..];
                
                if (arg.StartsWith("--script_output=")) scriptOutput = arg[(arg.IndexOf('=') + 1)..];
                if (arg.StartsWith("--scene_output=")) sceneOutput = arg[(arg.IndexOf('=') + 1)..];
            }

            if (!Directory.Exists(scriptDirectory))
            {
                Console.WriteLine("Cannot find script directory! Creating new empty script directory");
                Directory.CreateDirectory(sceneDirectory);
            }

            string[] scriptContents = Directory.GetFiles(scriptDirectory).Select(File.ReadAllText).ToArray();
            string[] scenePaths = Directory.GetFiles(sceneDirectory); //SceneBuilder accepts paths and not contents!

            Console.WriteLine("First building scripts, then building scenes.");

            //Build scripts
            ScriptAssemblyBuilder scriptBuilder = new(null, null, null, scriptContents);

            Console.WriteLine(
                $"Building the following scripts: {string.Join('\n', Directory.GetFiles(scriptDirectory))}");
            var scriptResults = scriptBuilder.CompileIntoAssembly(scriptOutput);
            
            Console.WriteLine(scriptResults.Any(e => e.Severity == DiagnosticSeverity.Error)
                ? "Building scripts failed!"
                : $"Output .dll to file path: {Path.GetFullPath(scriptOutput)}");
            Console.WriteLine(
                $"Diagnostic results:\n{string.Join("\n", scriptResults.Select(e => e.GetMessage()).ToArray())}\n");
            
            //Build scenes
            SceneAssemblyBuilder sceneBuilder = new(null, scenePaths);
            
            Console.WriteLine($"Building the following scenes: {string.Join('\n', scenePaths)}");
            var sceneResults = sceneBuilder.CompileIntoAssembly(sceneOutput);
            
            Console.WriteLine(sceneResults.Any(e => e.Severity == DiagnosticSeverity.Error)
                ? "Building scenes failed!"
                : $"Output .dll to file path: {Path.GetFullPath(sceneOutput)}");
            Console.WriteLine(
                $"Diagnostic results:\n{string.Join("\n", sceneResults.Select(e => e.GetMessage()).ToArray())}");
        }
    }
}