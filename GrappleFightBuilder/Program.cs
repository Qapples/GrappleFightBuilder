using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace GrappleFightBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            var (globalScriptDirectory, userInterfaceScriptDirectory, scenesDirectory, scriptOutput) =
                (@"Scripts", @"UI/Scripts", @"Scenes", "GrappleFightScripts.dll");

            //Console.WriteLine(args.Length);
            foreach (string arg in args)
            {
                //Console.WriteLine(arg);
                string argVal = arg[(arg.IndexOf('=') + 1)..];

                if (arg.StartsWith("--global_script_directory=")) globalScriptDirectory = argVal;
                if (arg.StartsWith("--ui_script_directory")) userInterfaceScriptDirectory = argVal;
                if (arg.StartsWith("--scenes_directory=")) scenesDirectory = argVal;
                if (arg.StartsWith("--script_output=")) scriptOutput = argVal;
            }

            if (!Directory.Exists(globalScriptDirectory))
            {
                Console.WriteLine($"Cannot find global script directory ({globalScriptDirectory})!");
                return;
            }

            var (globalScriptContents, globalScriptLocations) = GetScriptsInSubdirectories(globalScriptDirectory, true);
            var (localScriptContents, localScriptLocations) = GetScriptsInSubdirectories(scenesDirectory, true);
            var (userInterfaceScriptContents, userInterfaceScriptLocations) =
                GetScriptsInSubdirectories(userInterfaceScriptDirectory, true);

            string globalNamespace = ScriptAssemblyBuilder.DefaultNamespace;
            string userInterfaceNamespace = "UserInterfaceScripts";

            var scriptsAndNamespaces = new (string nspace, string contents)[globalScriptContents.Length +
                                                                            localScriptLocations.Length +
                                                                            userInterfaceScriptLocations.Length];
            int i;
            int prevI;

            //load global scripts first
            for (i = 0; i < globalScriptContents.Length; i++)
            {
                scriptsAndNamespaces[i] = (globalNamespace, globalScriptContents[i]);
            }

            //then local scripts
            prevI = i;
            for (; i < prevI + localScriptContents.Length; i++)
            {
                string sceneName =
                    FindSceneNameFromDirectory(Directory.GetParent(localScriptLocations[i - prevI])!.FullName);

                scriptsAndNamespaces[i].nspace = $"{globalNamespace}.{sceneName}";
                scriptsAndNamespaces[i].contents = localScriptContents[i - prevI];
            }
            
            //then ui scripts
            prevI = i;
            for (; i < scriptsAndNamespaces.Length; i++)
            {
                scriptsAndNamespaces[i] = (userInterfaceNamespace, userInterfaceScriptContents[i - prevI]);
            }
            

            Console.WriteLine("\n============= SCRIPTS TO BUILD =============");
            Console.WriteLine($"{string.Join('\n', globalScriptLocations.Concat(localScriptLocations))}\n" +
                              $"{string.Join('\n', userInterfaceScriptLocations)}");
            
            ScriptAssemblyBuilder scriptBuilder = new(null, globalNamespace, null, scriptsAndNamespaces);
            var scriptResults = scriptBuilder.CompileIntoAssembly(scriptOutput);
            bool compileError = scriptResults.Any(e => e.Severity == DiagnosticSeverity.Error);

            //Diagnostic results
            Console.WriteLine($@"=========================================================
Output .dll to file path: {Path.GetFullPath(scriptOutput)}
=========================================================");
            Console.WriteLine(
                $"================ DIAGNOSTIC RESULTS ({(compileError ? "ERROR" : "OK")}) ================");
            Console.WriteLine($"{string.Join("\n", scriptResults.Select(e => e.ToString()).ToArray())}\n");

            Console.WriteLine("FINISH");
        }
        
        private static (string[] scriptContents, string[] scriptLocations) GetScriptsInSubdirectories(string directory,
            bool searchSubDir = false)
        {
            List<string> outputContents = new();
            List<string> outputLocations = new();

            foreach (string dir in Directory.GetFiles(directory).Where(e => Path.GetExtension(e) == ".cs"))
            {
                //normalize line endings to be Environment.NewLine
                outputContents.Add(Regex.Replace(File.ReadAllText(dir), @"\r\n|\n\r|\n|\r", Environment.NewLine));
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

        private static string FindSceneNameFromDirectory(string directory)
        {
            while (Directory.GetParent(directory) is not null)
            {
                string? sceneWorldFile = (from filePath in Directory.GetFiles(directory)
                    where Path.GetExtension(filePath) == ".world"
                    select Path.GetFileName(filePath)).FirstOrDefault();

                string? sceneName = Path.GetFileNameWithoutExtension(sceneWorldFile);
                if (sceneName is not null) return sceneName;
                
                directory = Path.Combine(directory, "..");
            }

            return directory;
        }
    }
}