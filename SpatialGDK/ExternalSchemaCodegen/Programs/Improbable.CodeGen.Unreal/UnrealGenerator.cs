using Improbable.Codegen.Base;
using Improbable.CodeGen.Base;
using System.Collections.Generic;
using System.Linq;

namespace Improbable.CodeGen.Unreal
{
    public class UnrealGenerator : ICodeGenerator
    {
        public static string GeneratorTitle = "Unreal External Schema Codegen";
        public static string RelativeIncludePrefix = "ExternalSchemaCodegen";

        public List<GeneratedFile> GenerateFiles(Bundle bundle)
        {
            var generatedFiles = new List<GeneratedFile>();
            var allGeneratedTypeContent = new Dictionary<string, TypeGeneratedCode>();
            var types = bundle.Types.Select(kv => new TypeDescription(kv.Key, bundle))
                .Union(bundle.Components.Select(kv => new TypeDescription(kv.Key, bundle)))
                .ToList();
            var topLevelTypes = types.Where(type => !type.IsNestedType);
            var topLevelEnums = bundle.Enums.Where(_enum => !bundle.IsNestedEnum(_enum.Key));

            // Generate utils files
            generatedFiles.AddRange(HelperFunctions.GetHelperFunctionFiles());
            generatedFiles.AddRange(MapEquals.GenerateMapEquals());

            // Generate extenral schema interface
            generatedFiles.AddRange(InterfaceGenerator.GenerateInterface(types.Where(type => type.IsComponent).ToList(), bundle));

            // Generated all type file content
            foreach (var toplevelType in topLevelTypes)
            {
                generatedFiles.Add(new GeneratedFile(Types.TypeToHeaderFilename(toplevelType.QualifiedName), HeaderGenerator.GenerateHeader(toplevelType, types, allGeneratedTypeContent, bundle)));
                generatedFiles.Add(new GeneratedFile(Types.TypeToSourceFilename(toplevelType.QualifiedName), SourceGenerator.GenerateSource(toplevelType, types, allGeneratedTypeContent, bundle)));
            }

            // Add enum files to generated files, ignoring nested enum which are defined in parent files
            foreach (var (enumQualifiedName, enumDefinition) in topLevelEnums)
            {
                generatedFiles.Add(new GeneratedFile(Types.TypeToHeaderFilename(enumQualifiedName), EnumGenerator.GenerateTopLevelEnum(enumDefinition, bundle)));
            }

            return generatedFiles;
        }
    }
}
