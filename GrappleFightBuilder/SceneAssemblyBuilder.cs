using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DefaultEcs;
using DefaultEcs.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Xna.Framework;

namespace GrappleFightBuilder
{
    /// <summary>
    /// Class that is used for constructing <see cref="Assembly"/> instances from files that describe a
    /// <see cref="DefaultEcs.World"/>
    /// </summary>
    public class SceneAssemblyBuilder
    {
        private const string DefaultNamespace = "SceneData";

        private static readonly MetadataReference[] DefaultReferences = new[]
        {
            Assembly.GetAssembly(typeof(Entity)).Location, Assembly.GetAssembly(typeof(GameTime)).Location,
            AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "netstandard").Location,
            Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location),
                "System.Runtime.dll"),
            Assembly.GetAssembly(typeof(System.Console)).Location, Assembly.GetAssembly(typeof(System.Object)).Location,
            Assembly.GetAssembly(typeof(System.Runtime.GCSettings)).Location
        }.Select(e => MetadataReference.CreateFromFile(e)).ToArray();

        private static readonly string[] DefaultImports =
        {
            "using System;", "using System.Diagnostics;", "using DefaultEcs;", "using Microsoft.Xna.Framework;",
            "using DefaultEcs.Serialization;", "using System.IO;"
        };

        /// <summary>
        /// A list of the each scene's file contents and name.
        /// </summary>
        public List<(string Name, string Contents)> SceneContents { get; set; }

        /// <summary>
        /// The name of the main namespace when compiling the assembly.
        /// </summary>
        public string NamespaceName { get; set; }
        

        /// <summary>
        /// Constructs a <see cref="SceneAssemblyBuilder"/> instance.
        /// </summary>
        /// <param name="namespaceName">The name of the main namespace when compiling the assembly. If this value is
        /// null, then the namespace name of "Scenes". will be used.</param>
        /// <param name="filePaths">The FILE PATHS (not contents!) of the scene files to include.</param>
        public SceneAssemblyBuilder(string? namespaceName = null, params string[] filePaths)
        {
            SceneContents = filePaths.Select(p => (Path.GetFileName(p), File.ReadAllText(p))).ToList();
            NamespaceName = namespaceName ?? DefaultNamespace;
        }

        private const string _classHeader = @"public static class";
        private const string _dataHeader = @"public static string Base64WorldContents = ";

        private const string _worldField =
            @"public static readonly World World = new BinarySerializer().Deserialize(new MemoryStream(Convert.FromBase64String(Base64WorldContents)));";

        private const string _dataInterface =
            @"public interface IWorldDataInterface
            {
                public static string Base64WorldContents;
                public static readonly World World;
            }";

        private string GetFinalizedSource(ISerializer serializer)
        {
            StringBuilder final = new();

            final.Append(GetHeader(DefaultImports));

            foreach (var (name, contents) in SceneContents)
            {
                final.Append($"{_classHeader} _{name.Remove(name.IndexOf('.'))}\n{{\n");
                final.Append($"{_dataHeader} \"{GetBase64FromWorldContents(contents, serializer)}\";\n");
                final.Append($"{_worldField}\n");
                final.Append("}");
            }

            final.Append(_dataInterface);
            final.Append("}"); //closing bracket from the namespace

            return final.ToString();
        }

        private string GetBase64FromWorldContents(string worldContents, ISerializer serializer)
        {
            //convert into world first
            TextSerializer textSerializer = new();
            World world = textSerializer.Deserialize(new MemoryStream(Encoding.UTF8.GetBytes(worldContents)));

            //convert world to raw byte data
            byte[] bytes;
            using (MemoryStream stream = new())
            {
                serializer.Serialize(stream, world);
                bytes = stream.ToArray();
            }

            return Convert.ToBase64String(bytes);
        }

        private string GetHeader(IEnumerable<string> imports)
        {
            StringBuilder builder = new();

            foreach (string import in imports) builder.Append(import + '\n');
            builder.Append('\n');
            builder.Append($"namespace {NamespaceName}\n{{\n");

            return builder.ToString();
        }

        private static readonly BinarySerializationContext DefaultContext = new();
        
        /// <summary>
        /// Compiles an <see cref="Assembly"/> instance from the contents of the scene.
        /// </summary>
        /// <param name="filePath">The file path and name to store the generated assembly as a .dll file.</param>
        /// <param name="context">A <see cref="BinarySerializationContext"/> instance that provides context for the
        /// <see cref="BinarySerializer"/> instance used in this method.</param>
        /// <param name="compilationOptions">Additional <see cref="CSharpCompilationOptions"/> that change how the
        /// script is compiled. By default, a new <see cref="CSharpCompilationOptions"/> instance with an
        /// <see cref="OutputKind"/> of <see cref="OutputKind.DynamicallyLinkedLibrary"/>.</param>
        /// <returns>An <see cref="ImmutableArray{T}"/> with any warnings or errors from the compilation.</returns>
        public ImmutableArray<Diagnostic> CompileIntoAssembly(string filePath,
            BinarySerializationContext? context = null,
            CSharpCompilationOptions? compilationOptions = null)
        {
            BinarySerializer serializer = new(context ?? DefaultContext);
            string finalCode = GetFinalizedSource(serializer);

            CSharpCompilation comp = CSharpCompilation.Create(
                assemblyName: "GrappleFightScenes",
                syntaxTrees: new[] {CSharpSyntaxTree.ParseText(finalCode)},
                references: DefaultReferences,
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
    }
}