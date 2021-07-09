using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DefaultEcs;
using GrappleFightNET5.Components.Script;
using GrappleFightNET5.Scenes.Info;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Xna.Framework;

namespace GrappleFightBuilder
{
    /// <summary>
    /// Class that is used to construct an <see cref="Assembly"/> from script files.
    /// </summary>
    public class ScriptAssemblyBuilder
    {
        private const string DefaultNamespace = "Scripts";

        /// <summary>
        /// The references that will be used when no references are specified in the constructor.
        /// </summary>
        private static readonly MetadataReference[] DefaultReferences = new[]
        {
            Assembly.GetAssembly(typeof(Entity)).Location, Assembly.GetAssembly(typeof(GameTime)).Location,
            AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "netstandard").Location,
            Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location), "System.Runtime.dll"),
            Assembly.GetAssembly(typeof(System.Console)).Location, Assembly.GetAssembly(typeof(System.Object)).Location,
            Assembly.GetAssembly(typeof(System.Runtime.GCSettings)).Location
        }.Select(e => MetadataReference.CreateFromFile(e)).ToArray();

        private static readonly string[] DefaultImports =
        {
            "using System;", "using System.Diagnostics;", "using DefaultEcs;", "using Microsoft.Xna.Framework;"
        };
        
        /// <summary>
        /// The name of the namespace when creating the script.
        /// </summary>
        public string Namespace { get; private set; }

        /// <summary>
        /// A list of each import that the script will use.
        /// </summary>
        public List<string> Imports { get; private set; }

        /// <summary>
        /// A list of <see cref="MetadataReference"/> that define the references that are necessary to compile the
        /// script.
        /// </summary>
        public List<MetadataReference> References { get; private set; }

        /// <summary>
        /// Represents the body of the script. Use <see cref="AddScript"/> to add additional code to the body. 
        /// </summary>
        public StringBuilder Body { get; private set; }

        /// <summary>
        /// Constructs an instance of <see cref="ScriptAssemblyBuilder"/>.
        /// </summary>
        /// <param name="imports">Defines the imports the script will use. If null, a default set of imports will be
        /// used. Default imports:<br/>
        /// using System;<br/>
        /// using System.Diagnostics;<br/>
        /// using DefaultEcs;<br/>
        /// using Microsoft.Xna.Framework;</param>
        /// <param name="nspace">Defines the namespace that will encompass all the classes and methods the script will
        /// use. If null, then a default namespace (<see cref="DefaultNamespace"/>) will be used.</param>
        /// <param name="references"><see cref="MetadataReference"/> instances that define the references that are
        /// necessary to compile the script.</param>
        /// <param name="scriptContents">The scripts (as strings, not file paths) to combine together so that an
        /// <see cref="Assembly"/> can be created later on.</param>
        public ScriptAssemblyBuilder(string[]? imports = null, string? nspace = null,
            MetadataReference[]? references = null, params string[]? scriptContents)
        {
            (Imports, References, Namespace) = (imports?.ToList() ?? DefaultImports.ToList(),
                references?.ToList() ?? DefaultReferences.ToList(), nspace ?? DefaultNamespace);
            Body = new StringBuilder();

            if (scriptContents is not null)
            {
                foreach (string script in scriptContents) AddScript(script);
            }
            else
            {
                Body = new StringBuilder();
            }
        }

        /// <summary>
        /// Constructs an instance of <see cref="ScriptAssemblyBuilder"/> without any script contents.
        /// </summary>
        /// <param name="imports">Defines the imports the script will use. If null, a default set of imports will be
        /// used. Default imports:<br/>
        /// using System;<br/>
        /// using System.Diagnostics;<br/>
        /// using DefaultEcs;<br/>
        /// using Microsoft.Xna.Framework;</param>
        /// <param name="nspace">Defines the namespace that will encompass all the classes and methods the script will
        /// use. If null, then a default namespace (<see cref="DefaultNamespace"/>) will be used.</param>
        /// <param name="references"><see cref="MetadataReference"/> instances that define the references that are
        /// necessary to compile the script.</param>
        public ScriptAssemblyBuilder(string[]? imports = null, string? nspace = null,
            MetadataReference[]? references = null) : this(imports, nspace, references, null)
        {
        }

        private const string _initBase = @"
            public static void Init(in Entity entity, World world)
            {";

        private const string _updateBase = @"
            public static void Update(GameTime gameTime, in Entity entity, World world)
            {";

        private const string _classBase = @"
            public static class ";
        
        /// <summary>
        /// Constructs an instance of <see cref="ScriptAssemblyBuilder"/> with <see cref="ScriptInfo"/> instances.
        /// in a <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="scriptInfos"><see cref="ScriptInfo"/> instances with it's <see cref="ScriptInfo.Init"/> and
        /// <see cref="ScriptInfo.Update"/> methods as the body, with each pair of methods being under a static class
        /// of name _<see cref="ScriptInfo.Name"/>. If <see cref="ScriptInfo.Name"/> is null, then the class name will
        /// be _{i}, with i representing the index of the <see cref="ScriptInfo"/> instance in <see cref="scriptInfos"/>
        /// </param>
        /// <param name="imports">Defines the imports the script will use. If null, a default set of imports will be
        /// used. Default imports:<br/>
        /// using System;<br/>
        /// using System.Diagnostics;<br/>
        /// using DefaultEcs;<br/>
        /// using Microsoft.Xna.Framework;</param>
        /// <param name="nspace">Defines the namespace that will encompass all the classes and methods the script will
        /// use. If null, then a default namespace (<see cref="DefaultNamespace"/>) will be used.</param>
        /// <param name="references"><see cref="MetadataReference"/> instances that define the references that are
        /// necessary to compile the script.</param>
        public ScriptAssemblyBuilder(in ReadOnlySpan<ScriptInfo> scriptInfos, string[]? imports = null, string? nspace = null,
            MetadataReference[]? references = null) : this(imports, nspace, references)
        {
            for (int i = 0; i < scriptInfos.Length; i++)
            {
                ref readonly var scriptInfo = ref scriptInfos[i];

                Body.Append($"{_classBase}_{scriptInfo.Name ?? i.ToString()}\n{{");

                if (scriptInfo.Init is not null)
                {
                    Body.Append(
                        $"{_initBase}{(scriptInfo.IsFilePath ? File.ReadAllText(scriptInfo.Init) : scriptInfo.Init)}" +
                        "}\n");
                }

                if (scriptInfo.Update is not null)
                {
                    Body.Append(
                        $"{_updateBase}{(scriptInfo.IsFilePath ? File.ReadAllText(scriptInfo.Update) : scriptInfo.Update)}" +
                        "}\n");
                }

                Body.Append($"\n}}");
            }
        }

        /// <summary>
        /// Adds code to <see cref="Body"/> and any new imports to <see cref="Imports"/> from script data.
        /// </summary>
        /// <param name="scriptContents">The contents of the script to add. (Not the file name).</param>
        public void AddScript(string scriptContents)
        {
            string[] imports = GetImports(scriptContents);
            Imports.AddRange(imports.Where(e => !Imports.Contains(e))); //don't add already existing references

            //substring past the header.
            string body = scriptContents[((imports.Length > 0 ? scriptContents.IndexOf(imports.Last()) : -1) + 1)..];
            Body.Append(body);
        }

        /// <summary>
        /// Compiles the <see cref="Body"/> and <see cref="Imports"/> together into a single string script. <br/>
        /// Note: the outgoing string value may not be formatted properly. If <see cref="Imports"/> is null then
        /// default imports are used.
        /// </summary>
        /// <returns>A new script that has the imports from <see cref="Imports"/> and the body from <see cref="Body"/>.
        /// </returns>
        public string GenerateFinalizedSource()
        {
            StringBuilder final = new();

            final.Append(GetHeader((IEnumerable<string>?) Imports ?? DefaultImports));
            final.Append(Body);
            final.Append('}'); //this is for the open bracket from the namespace declaration.

            return final.ToString();
        }

        /// <summary>
        /// Compiles the script from <see cref="Body"/> with the imports from <see cref="Imports"/> into an
        /// <see cref="Assembly"/> that can be referenced and invoked.
        /// </summary>
        /// <param name="filePath">The file path describing where to write the compiled assembly to. </param>
        /// <param name="compilationOptions">Additional <see cref="CSharpCompilationOptions"/> that change how the
        /// script is compiled. By default, a new <see cref="CSharpCompilationOptions"/> instance with an
        /// <see cref="OutputKind"/> of <see cref="OutputKind.DynamicallyLinkedLibrary"/>.</param>
        /// <returns>An <see cref="ImmutableArray{T}"/> with any warnings or errors from the compilation.</returns>
        public ImmutableArray<Diagnostic> CompileIntoAssembly(string filePath,
            CSharpCompilationOptions? compilationOptions = null)
        {
            string finalCode = GenerateFinalizedSource();

            CSharpCompilation comp = CSharpCompilation.Create(
                assemblyName: "GrappleFightScripts",
                syntaxTrees: new[] {CSharpSyntaxTree.ParseText(finalCode)},
                references: References,
                options: compilationOptions ?? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using MemoryStream ms = new();
            
            EmitResult result = comp.Emit(ms);

            if (!result.Success)
            {
                Debug.WriteLine("CompileIntoAssembly() has failed. Diagnostics have been returned.");
                return result.Diagnostics;
            }

            ms.Seek(0, SeekOrigin.Begin);

            using (FileStream fs = new(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                ms.WriteTo(fs);
            }

            return result.Diagnostics;
        }

        /// <summary>
        /// Gets <see cref="Script"/> instances from an <see cref="Assembly"/>.
        /// </summary>
        /// <param name="assembly"><see cref="Assembly"/> to get the <see cref="Script"/> instances from.</param>
        /// <returns>An array of <see cref="Script"/> with <see cref="Script.Init"/> and <see cref="Script.Update"/>
        /// delegates from the provided <see cref="Assembly"/>.</returns>
        public static Script[] GetScriptsFromAssembly(Assembly assembly)
        {
            List<Script> outList = new();

            foreach (Type type in assembly.GetTypes())
            {
                Script script = new();
                var (initMethod, updateMethod) = (type.GetMethod("Init"), type.GetMethod("Update"));

                if (initMethod is not null) script.Init = initMethod.CreateDelegate<ScriptHelper.InitDelegate>();
                if (updateMethod is not null)
                {
                    //this line is too long for a one liner rip
                    script.Update = updateMethod.CreateDelegate<ScriptHelper.UpdateDelegate>();
                }
                
                if (initMethod is not null || updateMethod is not null) outList.Add(script);
            }

            return outList.ToArray();
        }

        /// <summary>
        /// Gets a <see cref="Script"/> instance from an <see cref="Assembly"/> from a specified class name.
        /// </summary>
        /// <param name="assembly"><see cref="Assembly"/> to get the <see cref="Script"/> instances from.</param>
        /// <returns>An array of <see cref="Script"/> with <see cref="Script.Init"/> and <see cref="Script.Update"/>
        /// delegates from the provided <see cref="Assembly"/>.</returns>
        /// <param name="className">The class name to get the <see cref="Script.Init"/> and <see cref="Script.Update"/>
        /// methods from. </param>
        /// <param name="outScript">The <see cref="Script"/> with the Init/Update methods loaded in from the assembly.
        /// </param>
        /// <returns>If true is returned, then the script was obtained successfully with either
        /// <see cref="Script.Init"/> or <see cref="Script.Update"/> having values. If false is returned, then neither
        /// <see cref="Script.Init"/> or <see cref="Script.Update"/> were obtained succesfully.</returns>
        public static bool TryGetScriptFromAssembly(Assembly assembly, string className, out Script outScript)
        {
            Script script = new();
            Type? classType = assembly.GetTypes().First(e => e.Name == className);

            var (initMethod, updateMethod) = (classType?.GetMethod("Init"),
                classType?.GetMethod("Update"));
            
            if (initMethod is not null) script.Init = initMethod.CreateDelegate<ScriptHelper.InitDelegate>();
            if (updateMethod is not null)
            {
                //this line is too long for a one liner rip
                script.Update = updateMethod.CreateDelegate<ScriptHelper.UpdateDelegate>();
            }

            if (initMethod is not null || updateMethod is not null)
            {
                outScript = script;
                return true;
            }

            Debug.WriteLine(
                $"TryGetScriptFromAssembly failed. Returning null. Class name: {className}. Assembly: {assembly}.");
            outScript = script;
            return false;
        }

        private static string[] GetImports(in string value) =>
            Regex.Matches(value, "using.+").Select(e => e.Value).ToArray();

        private string GetHeader(IEnumerable<string> imports)
        {
            StringBuilder builder = new();

            foreach (string import in imports) builder.Append(import + '\n');
            builder.Append('\n');
            builder.Append($"namespace {Namespace}\n{{");

            return builder.ToString();
        }
    }
}