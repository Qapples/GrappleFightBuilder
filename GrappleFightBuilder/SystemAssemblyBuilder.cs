using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DefaultEcs;
using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;

namespace GrappleFightBuilder
{
    /// <summary>
    /// Class that is used to construct an <see cref="Assembly"/> from assembly files.
    /// </summary>
    public sealed class SystemAssemblyBuilder : Builder
    {
        private const string DefaultNamespace = "SystemData";

        /// <summary>
        /// Constructs an instance of <see cref="SystemAssemblyBuilder"/>.
        /// </summary>
        /// <param name="imports">Defines the imports each system will use. If null, a default set of imports will be
        /// used. Default imports:<br/>
        /// using System;<br/>
        /// using System.Diagnostics;<br/>
        /// using DefaultEcs;<br/>
        /// using Microsoft.Xna.Framework;</param>
        /// <param name="nspace">Defines the namespace that will encompass all the classes and methods the systems will
        /// use. If null, then a default namespace (<see cref="DefaultNamespace"/>) will be used.</param>
        /// <param name="references"><see cref="MetadataReference"/> instances that define the references that are
        /// necessary to compile the systems.</param>
        /// <param name="systemContents">The systems (as strings, not file paths) to combine together so that an
        /// <see cref="Assembly"/> can be created later on.</param>
        // we have to use null here because we can't set it to any variable non-const value
        public SystemAssemblyBuilder(string[]? imports = null, string? nspace = null,
            MetadataReference[]? references = null, params string[]? systemContents)
        {
            (Imports, References, Namespace) = (imports?.ToList() ?? DefaultImports.ToList(),
                references?.ToList() ?? DefaultReferences.ToList(), nspace ?? DefaultNamespace);
            Body = new StringBuilder();

            if (systemContents is not null)
            {
                foreach (string system in systemContents) Add(system);
            }
            else
            {
                Body = new StringBuilder();
            }
        }

        private const string _exampleClass = "public static class Example{}";
    }
}