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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Xna.Framework;

namespace GrappleFightBuilder
{
    public abstract class Builder
    {
        /// <summary>
        /// The references that will be used when no references are specified in the constructor.
        /// </summary>
        protected static readonly MetadataReference[] DefaultReferences = new[]
        {
            Assembly.GetAssembly(typeof(Entity)).Location, Assembly.GetAssembly(typeof(GameTime)).Location,
            AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "netstandard").Location,
            Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location), "System.Runtime.dll"),
            Assembly.GetAssembly(typeof(System.Console)).Location, Assembly.GetAssembly(typeof(System.Object)).Location,
            Assembly.GetAssembly(typeof(System.Runtime.GCSettings)).Location
        }.Select(e => MetadataReference.CreateFromFile(e)).ToArray();

        protected static readonly string[] DefaultImports =
        {
            "using System;", "using System.Diagnostics;", "using DefaultEcs;", "using Microsoft.Xna.Framework;"
        };
        
        public string Namespace { get; protected set; }
        
        public List<string> Imports { get; protected set; }
        public List<MetadataReference> References { get; protected set; }

        public StringBuilder Body { get; protected set; }

        private const string _exampleClass = "public static class Example{}";

        /// <summary>
        /// Compiles the <see cref="Body"/> and <see cref="Imports"/> together into a single string script. <br/>
        /// Note: the outgoing string value may not be formatted properly. If <see cref="Imports"/> is null then
        /// default imports are used.
        /// </summary>
        /// <returns>A new script that has the imports from <see cref="Imports"/> and the body from <see cref="Body"/>.
        /// </returns>
        protected virtual string GenerateFinalizedSource()
        {
            StringBuilder final = new();

            final.Append(GetHeader((IEnumerable<string>?) Imports ?? DefaultImports));
            final.Append(_exampleClass); //have this example class so that we can get the assembly via typeof().Assembly
            final.Append(Body);
            final.Append('}'); //this is for the open bracket from the namespace declaration.

            return final.ToString();
        }

        protected virtual void Add(string contents)
        {
            string[] imports = GetImports(contents);
            Imports.AddRange(imports.Where(e => !Imports.Contains(e))); //don't add already existing references();
            
            //substring past the header
            string body =
                contents[((imports.Length > 0 ? contents.IndexOf(imports.Last()) + imports.Last().Length : -1) + 1)..];
            Body.Append(body);
        }

        /// <summary>
        /// Compiles the script from <see cref="Body"/> with the imports from <see cref="Imports"/> into an
        /// <see cref="Assembly"/> that can be referenced and invoked.
        /// </summary>
        /// <param name="filePath">The file path describing where to write the compiled assembly to.</param>
        /// <param name="assemblyName">The name of the assembly.</param>
        /// <param name="compilationOptions">Additional <see cref="CSharpCompilationOptions"/> that change how the
        /// script is compiled. By default, a new <see cref="CSharpCompilationOptions"/> instance with an
        /// <see cref="OutputKind"/> of <see cref="OutputKind.DynamicallyLinkedLibrary"/>.</param>
        /// <returns>An <see cref="ImmutableArray"/> with any warnings or errors from the compilation.</returns>
        public virtual ImmutableArray<Diagnostic> CompileIntoAssembly(string filePath, string assemblyName,
            CSharpCompilationOptions? compilationOptions = null)
        {
           string finalCode = GenerateFinalizedSource();

            CSharpCompilation comp = CSharpCompilation.Create(
                assemblyName: assemblyName,
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
        
        protected static string[] GetImports(in string value) =>
            Regex.Matches(value, "using.+").Select(e => e.Value).ToArray();

        protected string GetHeader(IEnumerable<string> imports)
        {
            StringBuilder builder = new();

            foreach (string import in imports) builder.Append(import + '\n');
            builder.Append('\n');
            builder.Append($"namespace {Namespace}\n{{");

            return builder.ToString();
        }
    }
}