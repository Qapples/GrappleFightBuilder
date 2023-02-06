using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AppleScene.Helpers;
using AppleScene.Rendering;
using DefaultEcs;
using GrappleFight.Collision;
using GrappleFight.Components;
using GrappleFight.Input;
using GrappleFight.Network;
using GrappleFight.Resource;
using GrappleFight.Utils;
using MessagePack;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Xna.Framework;
using SharpGLTF.Schema2;

namespace GrappleFightBuilder
{
    /// <summary>
    /// Class that is used to construct an <see cref="Assembly"/> from script files.
    /// </summary>
    public class ScriptAssemblyBuilder
    {
        public const string DefaultNamespace = "ScriptData";

        /// <summary>
        /// The references that will be used when no references are specified in the constructor.
        /// </summary>
        private static readonly MetadataReference[] DefaultReferences = new[]
        {
            Assembly.GetAssembly(typeof(Entity)).Location, Assembly.GetAssembly(typeof(GameTime)).Location,
            AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "netstandard").Location,
            Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location),
                "System.Runtime.dll"),
            Assembly.GetAssembly(typeof(System.Console)).Location, Assembly.GetAssembly(typeof(object)).Location,
            Assembly.GetAssembly(typeof(IScript)).Location, Assembly.GetAssembly(typeof(Input)).Location,
            Assembly.GetAssembly(typeof(ICollisionHull)).Location, Assembly.GetAssembly(typeof(GameClient)).Location,
            Assembly.GetAssembly(typeof(Animation)).Location, Assembly.GetAssembly(typeof(MeshData)).Location,
            Assembly.GetAssembly(typeof(Matrix4x4)).Location, Assembly.GetAssembly(typeof(NumericsExtensions)).Location,
            Path.Combine(Assembly.GetAssembly(typeof(object)).Location, "..", "System.Numerics.Vectors.dll"),
            Path.Combine(Assembly.GetAssembly(typeof(object)).Location, "..", "System.Collections.dll"),
            Assembly.GetAssembly(typeof(IEnumerable<>)).Location,
            Assembly.GetAssembly(typeof(System.Linq.Enumerable)).Location,
            Assembly.GetAssembly(typeof(MonogameExtensions)).Location,
            Assembly.GetAssembly(typeof(JsonSerializer)).Location,
            Assembly.GetAssembly(typeof(MessagePackSerializer)).Location,
            Assembly.GetAssembly(typeof(IgnoreMemberAttribute)).Location,
            Assembly.GetAssembly(typeof(ContentPath)).Location
        }.Select(e => MetadataReference.CreateFromFile(e)).ToArray();

        private static readonly string[] DefaultImports =
        {
            "using System;", "using System.Diagnostics;", "using GrappleFight.Components;", 
            "using DefaultEcs;", "using Microsoft.Xna.Framework;", "using MessagePack;"
        };
        
        /// <summary>
        /// The name of the global namespace that all scripts reside in.
        /// </summary>
        public string GlobalNamespace { get; private set; }

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
        /// There can be multiple namespaces in the output assembly, and this dictionary represents the body of each
        /// of the namespaces. 
        /// </summary>
        public Dictionary<string, List<string>> NamespaceScripts;
            
        /// <summary>
        /// Constructs an instance of <see cref="ScriptAssemblyBuilder"/>.
        /// </summary>
        /// <param name="imports">Defines the imports the script will use. If null, a default set of imports will be
        /// used. Default imports:<br/>
        /// using System;<br/>
        /// using System.Diagnostics;<br/>
        /// using DefaultEcs;<br/>
        /// using Microsoft.Xna.Framework;</param>
        /// <param name="globalNamespace">Defines the namespace that will encompass all the classes and methods the script will
        /// use. If null, then a default namespace (<see cref="DefaultNamespace"/>) will be used.</param>
        /// <param name="references"><see cref="MetadataReference"/> instances that define the references that are
        /// necessary to compile the script.</param>
        /// <param name="scripts">The scripts (as strings, not file paths) to combine together so that an
        /// <see cref="Assembly"/> can be created later on.</param>
        // we have to use null here because we can't set it to any variable non-const value
        public ScriptAssemblyBuilder(string[]? imports = null, string? globalNamespace = null,
            MetadataReference[]? references = null, params (string nspace, string contents)[]? scripts)
        {
            (Imports, References, GlobalNamespace) = (imports?.ToList() ?? DefaultImports.ToList(),
                references?.ToList() ?? DefaultReferences.ToList(), globalNamespace ?? DefaultNamespace);

            string exampleScript = $"namespace {globalNamespace}\n{{\n{ExampleClass}\n}}";
            NamespaceScripts = new Dictionary<string, List<string>>
                { { GlobalNamespace, new List<string> {exampleScript} } };

            if (scripts is not null)
            {
                foreach (var (nspace, contents) in scripts)
                {
                    AddScript(string.IsNullOrEmpty(nspace) ? GlobalNamespace : nspace, contents);
                }
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
        /// <param name="globalNamespace">Defines the namespace that will encompass all the classes and methods the script will
        /// use. If null, then a default namespace (<see cref="DefaultNamespace"/>) will be used.</param>
        /// <param name="references"><see cref="MetadataReference"/> instances that define the references that are
        /// necessary to compile the script.</param>
        public ScriptAssemblyBuilder(string[]? imports = null, string? globalNamespace = null,
            MetadataReference[]? references = null) : this(imports, globalNamespace, references, null)
        {
        }

        private const string ExampleClass = "public static class Example{}";

        /// <summary>
        /// Adds code to a script body in <see cref="NamespaceScripts"/> and any new imports to <see cref="Imports"/>
        /// from script data.
        /// </summary>
        /// <param name="scriptNamespace">The namespace the script resides in.</param>
        /// <param name="scriptContents">The contents of the script to add. (Not the file name).</param>
        public void AddScript(string scriptNamespace, string scriptContents)
        {
            string[] imports = GetImports(scriptContents, out int len);
            if (imports.Length != 0)
            {
                len += scriptContents.IndexOf(imports.First());
            }

            //don't add already existing references
            Imports.AddRange(imports.Where(e => !Imports.Contains(e.Trim())));

            scriptContents = scriptContents[..len] + $"\nnamespace {scriptNamespace} {{\n" + scriptContents[len..] +
                             "\n}";

            if (!NamespaceScripts.ContainsKey(scriptNamespace))
            {
                NamespaceScripts[scriptNamespace] = new List<string>();
            }
            
            NamespaceScripts[scriptNamespace].Add(scriptContents);
        }

        /// <summary>
        /// Compiles the script from <see cref="Body"/> with the imports from <see cref="Imports"/> into an
        /// <see cref="Assembly"/> that can be referenced and invoked.
        /// </summary>
        /// <param name="filePath">The file path describing where to write the compiled assembly to.</param>
        /// <param name="compilationOptions">Additional <see cref="CSharpCompilationOptions"/> that change how the
        /// script is compiled. By default, a new <see cref="CSharpCompilationOptions"/> instance with an
        /// <see cref="OutputKind"/> of <see cref="OutputKind.DynamicallyLinkedLibrary"/>.</param>
        /// <returns>An <see cref="ImmutableArray{T}"/> with any warnings or errors from the compilation.</returns>
        public ImmutableArray<Diagnostic> CompileIntoAssembly(string filePath,
            CSharpCompilationOptions? compilationOptions = null)
        { 
            List<SyntaxTree> syntaxTrees = new();
            foreach (List<string> scriptList in NamespaceScripts.Values)
            {
                foreach (string script in scriptList)
                {
                    syntaxTrees.Add(CSharpSyntaxTree.ParseText(script));
                }
            }

            CSharpCompilation comp = CSharpCompilation.Create(
                assemblyName: "GrappleFightScripts",
                syntaxTrees: syntaxTrees,
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
        
        private static string[] GetImports(string value, out int len)
        {
            //normalize all line endings to be Environment.NewLine
            string[] matches = Regex.Matches(value, "^using\\s.+.;", RegexOptions.Multiline)
                .Select(e => Regex.Replace(e.Value, @"\r\n|\n\r|\n|\r", "")).ToArray();
            len = matches.Sum(e => e.Length + Environment.NewLine.Length);

            return matches;
        }

        private string GetHeader(IEnumerable<string> imports)
        {
            StringBuilder builder = new();

            foreach (string import in imports) builder.Append(import + '\n');

            return builder.ToString();
        }
    }
}