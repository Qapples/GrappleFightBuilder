using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            bool searchSubDir = false;

            //Console.WriteLine(args.Length);
            foreach (string arg in args)
            {
                //Console.WriteLine(arg);
                string argVal = arg[(arg.IndexOf('=') + 1)..];

                if (arg.StartsWith("--script_directory=")) scriptDirectory = argVal;
                if (arg.StartsWith("--scene_directory=")) sceneDirectory = argVal;

                if (arg.StartsWith("--script_output=")) scriptOutput = argVal;
                if (arg.StartsWith("--scene_output=")) sceneOutput = argVal;

                if (arg.StartsWith("--search_subdirectories"))
                {
                    bool parse = bool.TryParse(argVal, out searchSubDir);
                    searchSubDir = parse && searchSubDir;
                }
            }

            if (!Directory.Exists(scriptDirectory))
            {
                Console.WriteLine($"Cannot find script directory ({scriptDirectory})!"); 
                return;
            }

            var (scriptContents, scriptLocations) = GetScriptsInSubdirectories(scriptDirectory, searchSubDir);
            string[] scenePaths = Directory.GetFiles(sceneDirectory); //SceneBuilder accepts paths and not contents!

            Console.WriteLine("\nBUILDING SCRIPTS (THEN SCENES)\n");
            
            //Build scripts
            ScriptAssemblyBuilder scriptBuilder = new(null, null, null, scriptContents);

            Console.WriteLine(
                $"Building the following scripts: {string.Join('\n', scriptLocations)}");
            var scriptResults = scriptBuilder.CompileIntoAssembly(scriptOutput);

            Console.WriteLine(scriptResults.Any(e => e.Severity == DiagnosticSeverity.Error)
                ? "Building scripts failed!"
                : $"Output .dll to file path: {Path.GetFullPath(scriptOutput)}");
            Console.WriteLine(
                $"Diagnostic results:\n{string.Join("\n", scriptResults.Select(e => e.ToString()).ToArray())}\n");

            //Build scenes
            
            Console.WriteLine("FINISHED BUILDING SCRIPTS. BUILDING SCENES NOW.\n");
            return;
            SceneAssemblyBuilder sceneBuilder = new(null, scenePaths);
            
            Console.WriteLine($"Building the following scenes: {string.Join('\n', scenePaths)}");
            var sceneResults = sceneBuilder.CompileIntoAssembly(sceneOutput);
            
            Console.WriteLine(sceneResults.Any(e => e.Severity == DiagnosticSeverity.Error)
                ? "Building scenes failed!"
                : $"Output .dll to file path: {Path.GetFullPath(sceneOutput)}");
            Console.WriteLine(
                $"Diagnostic results:\n{string.Join("\n", sceneResults.Select(e => e.GetMessage()).ToArray())}");
        }

        static (string[] scriptContents, string[] scriptLocations) GetScriptsInSubdirectories(string directory,
            bool searchSubDir = false)
        {
            List<string> outputContents = new();
            List<string> outputLocations = new();

            foreach (string dir in Directory.GetFiles(directory).Where(e => Path.GetExtension(e) == ".cs"))
            {
                outputContents.Add(File.ReadAllText(dir));
                outputLocations.Add(dir);
            }

            if (searchSubDir)
            {
                foreach (string dir in Directory.GetDirectories(directory))
                {
                    var (contents, locations) = GetScriptsInSubdirectories(dir, searchSubDir);
                    outputContents.AddRange(contents);
                    outputLocations.AddRange(locations);
                }
            }

            return (outputContents.ToArray(), outputLocations.ToArray());
        }
    }
}