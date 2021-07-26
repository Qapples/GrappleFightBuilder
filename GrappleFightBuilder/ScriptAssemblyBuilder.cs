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
    /// <summary>
    /// Class that is used to construct an <see cref="Assembly"/> from script files.
    /// </summary>
    public sealed class ScriptAssemblyBuilder : Builder
    {
        private const string DefaultNamespace = "ScriptData";

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
        // we have to use null here because we can't set it to any variable non-const value
        public ScriptAssemblyBuilder(string[]? imports = null, string? nspace = null,
            MetadataReference[]? references = null, params string[]? scriptContents)
        {
            (Imports, References, Namespace) = (imports?.ToList() ?? DefaultImports.ToList(),
                references?.ToList() ?? DefaultReferences.ToList(), nspace ?? DefaultNamespace);
            Body = new StringBuilder();

            if (scriptContents is not null)
            {
                foreach (string script in scriptContents) Add(script);
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

        private const string _exampleClass = "public static class Example{}";
    }
}