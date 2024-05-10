// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ProjectMetadataGenerator.Generators;

internal static class AWSLambdaExecutableProjectMetadataGenerator
{
    public static void Run(string projectPath, string metadataTypeName, string assemblyPath, string outputPath)
    {
        var assemblyName = Path.GetFileName(assemblyPath).Replace(".dll", "");
        var lambdaAssemblyPath = Path.GetDirectoryName(assemblyPath)!;

        var compilation = CSharpCompilation.Create("MetadataGenerator",
            references: [CreateMetadataReference(assemblyPath, assemblyName)]);

        var traits = FindTraits(compilation, assemblyName);

        var metadata =
            Render(metadataTypeName, projectPath, lambdaAssemblyPath, assemblyName, traits);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, metadata);
    }

    private static List<string> FindTraits(
        CSharpCompilation compilation, string assemblyName)
    {
        var traits = new List<string> { "IsExecutableProject" };

        var assembly = compilation.SourceModule.ReferencedAssemblySymbols.Single(x => x.Name == assemblyName);
        var module = assembly.Modules.Single();

        if (module.ReferencedAssemblySymbols.Any(x => x.Name == "Amazon.Lambda.AspNetCoreServer.Hosting"))
        {
            traits.Add("Amazon.Lambda.AspNetCoreServer.Hosting");
        }

        return traits;
    }

    private static string Render(string metadataTypeName, string projectPath, string outputPath, string assemblyName,
        List<string> traits)
    {
        var traitsStr = string.Join(", ", traits.Select(x => $"\"{x}\""));

        return $$""""
                 // <auto-generated/>
                 namespace LambdaFunctions;

                 [global::System.CodeDom.Compiler.GeneratedCode("Aspire.Hosting", null)]
                 [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Generated code.")]
                 [global::System.Diagnostics.DebuggerDisplay("Type = {GetType().Name,nq}, Handler = {Handler}")]
                 public class {{metadataTypeName}} : global::Aspire.Hosting.AWS.Lambda.ILambdaFunctionMetadata
                 {
                     public string ProjectPath => """{{projectPath}}""";
                     public string Handler => "{{assemblyName}}";
                     public string OutputPath => """{{outputPath}}""";
                     public string[] Traits => [{{traitsStr}}];
                 }
                 """";
    }

    private static PortableExecutableReference CreateMetadataReference(string path, string assemblyName)
    {
        var doc =
            $"<?xml version=\"1.0\"?><doc><assembly><name>{assemblyName}</name></assembly><members></members></doc>";
        var documentationProvider = XmlDocumentationProvider.CreateFromBytes(Encoding.UTF8.GetBytes(doc));

        return MetadataReference.CreateFromFile(path, documentation: documentationProvider);
    }
}