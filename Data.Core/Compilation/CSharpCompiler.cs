using Microsoft.CSharp;
using Common;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Compilation
{
    public static class CSharpCompiler
    {
        //static HashSet<string> _deleteSet = new HashSet<string>();
        static int ver = 0;
        public static Type Compile(string template, string typeName, string targetPath, string[] references, out bool hasErrors, out CompilerErrorCollection errors)
        {
            if (!Path.IsPathRooted(targetPath))
                targetPath = Path.Combine(AppContext.Current.CodeBase, targetPath);

            hasErrors = false;
            errors = null;
            // that's all we need to do with the template, now just compile it and return the new type
            CodeDomProvider codeProvider = new CSharpCodeProvider();
            CompilerParameters compilerParams = new CompilerParameters();
            // setup the basic compiler options
            compilerParams.CompilerOptions = "/target:library";
            compilerParams.GenerateExecutable = false;


            Directory.CreateDirectory(targetPath);

            lock (typeof(CSharpCompiler))
            {
                ver++;
                compilerParams.OutputAssembly = Path.Combine(targetPath, String.Format("v{0}.dll", ver));
            }
            bool debug = bool.Parse(AppContext.GetEnvironmentVariable("DebugInjections", true).ToString());
            compilerParams.GenerateInMemory = !debug;
            compilerParams.IncludeDebugInformation = debug;
            compilerParams.TempFiles = new TempFileCollection(targetPath, debug); // change to true to load in VS debugger

            // add references to external assemblies
            compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
            compilerParams.ReferencedAssemblies.Add("system.dll");
            compilerParams.ReferencedAssemblies.Add("system.core.dll");
            compilerParams.ReferencedAssemblies.Add("system.data.dll");
            compilerParams.ReferencedAssemblies.Add("system.xml.dll");
            compilerParams.ReferencedAssemblies.Add("system.xml.linq.dll");
            compilerParams.ReferencedAssemblies.Add("microsoft.csharp.dll");
            compilerParams.ReferencedAssemblies.Add("system.drawing.dll");
            compilerParams.ReferencedAssemblies.Add("system.windows.forms.dll");
            compilerParams.ReferencedAssemblies.Add(new System.Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
            string caller = Assembly.GetCallingAssembly().CodeBase;
            AddReference(compilerParams, caller);

            if (references != null)
            {
                foreach (string reference in references)
                    AddReference(compilerParams, reference);
            }


            // compile the source code
            CompilerResults results = codeProvider.CompileAssemblyFromSource(compilerParams, template);
            if (results.Errors.HasErrors)
            {
                hasErrors = true;
                errors = results.Errors;
                return null;
            }
            else
            {
                Assembly dynamicAssembly = results.CompiledAssembly;
                return dynamicAssembly.GetTypes().Where(t => t.Name.Equals(typeName)).FirstOrDefault();
            }
        }

        private static void AddReference(CompilerParameters compilerParams, string reference)
        {
            try
            {
                Assembly asm = null;
                if (Path.GetExtension(reference).Equals(".dll", StringComparison.InvariantCultureIgnoreCase)
                    || Path.GetExtension(reference).Equals(".exe", StringComparison.InvariantCultureIgnoreCase))
                {
                    asm = Assembly.LoadFrom(reference);
                }
                else
                {
                    try
                    {
                        asm = Assembly.LoadFrom(reference + ".dll");
                    }
                    catch
                    {
                        asm = Assembly.LoadFrom(reference + ".exe");
                    }
                }
                Uri path = new Uri(asm.CodeBase);
                if (!compilerParams.ReferencedAssemblies.Contains(asm.ManifestModule.Name.ToLowerInvariant())
                    && !compilerParams.ReferencedAssemblies.Contains(path.LocalPath))
                    compilerParams.ReferencedAssemblies.Add(path.LocalPath);
            }
            catch { }
        }
    }

    public static class CompilerErrorCollectionEx
    {
        public static string ToErrorString(this CompilerErrorCollection errors)
        {
            // log failure
            StringBuilder sb = new StringBuilder("");
            for (int i = 0; i < errors.Count; i++)
            {
                // LogError(results.Errors[i].ErrorText);
                sb.AppendLine(String.Format("Error {0}: {1}, Line: {2}, Column: {3}",
                    errors[i].ErrorNumber,
                    errors[i].ErrorText,
                    errors[i].Line,
                    errors[i].Column));
            }
            return sb.ToString();
        }
    }
}
