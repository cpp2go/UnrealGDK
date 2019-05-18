using Improbable.CodeGen.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Improbable.CodeGen.Unreal
{
    public static class HeaderGenerator
    { 
        public static string GenerateHeader(TypeDescription type, List<TypeDescription> types, Dictionary<string, TypeGeneratedCode> allGeneratedTypeContent, Bundle bundle)
        {
            var sourceRef = bundle.SchemaBundle.SourceMapV1.SourceReferences[type.QualifiedName];
            var allTopLevelTypes = Types.SortTopLevelTypesTopologically(type, types, bundle);
            var typeNamespaces = Text.GetNamespaceFromTypeName(type.QualifiedName);
            var requiredIncludes = Types.GetRequiredTypeIncludes(type, bundle).Select(inc => $"#include \"{string.Concat(Enumerable.Repeat("../", type.QualifiedName.Count(c => c == '.')))}{inc}\"");
            var allNestedEnums = Types.GetRecursivelyNestedEnums(type);
            var enumDefs = allNestedEnums.Select(enumDef => EnumGenerator.GenerateEnum(Types.GetTypeClassDefinitionName(enumDef.Identifier.QualifiedName, bundle), enumDef, bundle).Replace($"enum {enumDef.Identifier.Name}", $"class {enumDef.Identifier.Name}_{enumDef.Identifier.Name}"));

            var builder = new StringBuilder();

            builder.AppendLine($@"// Generated by {UnrealGenerator.GeneratorTitle}

#pragma once

#include ""CoreMinimal.h""
#include ""Utils/SchemaOption.h""
#include <WorkerSDK/improbable/c_schema.h>
#include <WorkerSDK/improbable/c_worker.h>

#include ""{string.Concat(Enumerable.Repeat("../", type.QualifiedName.Count(c => c == '.')))}{HelperFunctions.HeaderPath}""
");
            if (requiredIncludes.Count() > 0)
            {
                builder.AppendLine(string.Join(Environment.NewLine, requiredIncludes));
                builder.AppendLine();
            }

            builder.AppendLine(string.Join(Environment.NewLine, typeNamespaces.Select(t => $"namespace {t} {{")));
            builder.AppendLine();

            if (allTopLevelTypes.Count() > 1)
            {
                builder.AppendLine(string.Join(Environment.NewLine, allTopLevelTypes.Select(topLevelType => $"class {Types.GetTypeClassDefinitionName(topLevelType.QualifiedName, bundle)};")));
                builder.AppendLine();
            }

            if (allNestedEnums.Count() > 0)
            {
                builder.AppendLine(string.Join(Environment.NewLine, enumDefs));
                builder.AppendLine();
            }

            builder.AppendLine($@"{string.Join(Environment.NewLine, allTopLevelTypes.Select(topLevelType => GenerateTypeClass(Types.GetTypeClassDefinitionName(topLevelType.QualifiedName, bundle), types.Find(t => t.QualifiedName == topLevelType.QualifiedName), types, bundle)))}
{string.Join(Environment.NewLine, allTopLevelTypes.Select(nestedType => GenerateHashFunction(Types.GetTypeClassDefinitionName(nestedType.QualifiedName, bundle))))}

{string.Join(Environment.NewLine, typeNamespaces.Reverse().Select(t => $"}} // namespace {t}"))}
");

            return builder.ToString();
        }

        private static string GenerateTypeClass(string name, TypeDescription type, List<TypeDescription> types, Bundle bundle)
        {
            var sourceRef = bundle.SchemaBundle.SourceMapV1.SourceReferences[type.QualifiedName];
            var hasFields = type.Fields.Count > 0;
            var serializedArgType = type.IsComponent ? "Schema_ComponentData" : "Schema_Object";
            var serializedArgName = type.IsComponent ? "ComponentData" : "SchemaObject";

            var builder = new StringBuilder();

            builder.AppendLine($@"// Generated from {Path.GetFullPath(sourceRef.FilePath)}({sourceRef.Line},{sourceRef.Column})
class {name} : public {(type.IsComponent ? "SpatialComponent" : "SpatialType")}
{{
public:");

            if (type.IsComponent)
            {
                builder.AppendLine(Text.Indent(1, $"static const Worker_ComponentId ComponentId = {type.ComponentId.Value};"));
            }

            if (type.NestedTypes.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, $@"// Nested types
{string.Join(Environment.NewLine, type.NestedTypes.Select(nestedType => Text.Indent(1, $"using {nestedType.Name} = {Types.GetTypeClassDefinitionName(nestedType.QualifiedName, bundle)};")))}"));
            }

            if (type.NestedEnums.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, $@"// Nested enums
{string.Join(Environment.NewLine, type.NestedEnums.Select(nestedEnum => $"using {nestedEnum.Identifier.Name} = {Types.GetTypeClassDefinitionName(nestedEnum.Identifier.QualifiedName, bundle)};"))}"));
            }

            if (type.Fields.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, $@"// Creates a new instance with specified arguments for each field.
{name}({string.Join(", ", type.Fields.Select(f => $"{Types.GetConstAccessorTypeModification(f, bundle, type)} {Text.SnakeCaseToPascalCase(f.Identifier.Name)}"))});"));
            }

            builder.AppendLine(Text.Indent(1, $@"// Creates a new instance with default values for each field.
{name}();
// Creates a new instance with default values for each field. This is
// equivalent to a default-constructed instance.
static {name} Create() {{ return {{}}; }}
// Copyable and movable.
{name}({name}&&) = default;
{name}(const {name}&) = default;
{name}& operator=({name}&&) = default;
{name}& operator=(const {name}&) = default;
~{name}() = default;

bool operator==(const {name}&) const;
bool operator!=(const {name}&) const;

// Serialize this object data into the C API argument
void Serialize({serializedArgType}* {serializedArgName}) const override;

// Deserialize the C API object argument into an instance of this class and return it
static {name} Deserialize({serializedArgType}* {serializedArgName});
"));

            if (hasFields)
            {
                builder.AppendLine($@"{Text.Indent(1, string.Join(Environment.NewLine, type.Fields.Select(field => $@"// Field {Text.SnakeCaseToPascalCase(field.Identifier.Name)} = {field.FieldId}
{Types.GetConstAccessorTypeModification(field, bundle, type)} Get{Text.SnakeCaseToPascalCase(field.Identifier.Name)}() const;
{Types.GetFieldTypeAsCpp(field, bundle, type)}& Get{Text.SnakeCaseToPascalCase(field.Identifier.Name)}();
{name}& Set{Text.SnakeCaseToPascalCase(field.Identifier.Name)}({Types.GetConstAccessorTypeModification(field, bundle, type)});
")))}
private:
{Text.Indent(1, string.Join(Environment.NewLine, type.Fields.Select(field => $"{Types.GetFieldTypeAsCpp(field, bundle, type)} _{Text.SnakeCaseToPascalCase(field.Identifier.Name)};")))}");
            }

            if (type.IsComponent)
            {
                if (hasFields)
                {
                    builder.AppendLine();
                    builder.AppendLine("public:");
                }
                builder.AppendLine($@"{Text.Indent(1, $@"class Update : public SpatialComponentUpdate
{{
public:
{Text.Indent(1, $@"// Creates a new instance with default values for each field.
Update() = default;
// Creates a new instance with default values for each field. This is
// equivalent to a default-constructed instance.
static Update Create() {{ return {{}}; }}
// Copyable and movable.
Update(Update&&) = default;
Update(const Update&) = default;
Update& operator=(Update&&) = default;
Update& operator=(const Update&) = default;
~Update() = default;
bool operator==(const Update&) const;
bool operator!=(const Update&) const;

// Creates an Update from a {Types.GetNameFromQualifiedName(type.QualifiedName)} object.
static Update FromInitialData(const {Types.GetNameFromQualifiedName(type.QualifiedName)}& Data);

/**
 * Converts to a {Types.GetNameFromQualifiedName(type.QualifiedName)}
 * object. It is an error to call this function unless *all* of the optional fields in this
 * update are filled in.
 */
{Types.GetNameFromQualifiedName(type.QualifiedName)} ToInitialData() const;

/**
 * Replaces fields in the given {Types.GetNameFromQualifiedName(type.QualifiedName)}
 * object with the corresponding fields in this update, where present.
 */
void ApplyTo({Types.GetNameFromQualifiedName(type.QualifiedName)}&) const;

// Serialize this update object data into the C API component update argument
void Serialize(Schema_ComponentUpdate* ComponentUpdate) const override;

// Deserialize the C API component update argument into an instance of this class and return it
static Update Deserialize(Schema_ComponentUpdate* ComponentUpdate);
")}")}");

                if (type.Fields.Count > 0)
                {
                    builder.AppendLine(Text.Indent(2, string.Join(Environment.NewLine, type.Fields.Select(field => $@"// Field {Text.SnakeCaseToPascalCase(field.Identifier.Name)} = {field.FieldId}
const {Types.CollectionTypesToQualifiedTypes[Types.Collection.Option]}<{Types.GetFieldTypeAsCpp(field, bundle, type)}>& Get{Text.SnakeCaseToPascalCase(field.Identifier.Name)}() const;
{Types.CollectionTypesToQualifiedTypes[Types.Collection.Option]}<{Types.GetFieldTypeAsCpp(field, bundle, type)}>& Get{Text.SnakeCaseToPascalCase(field.Identifier.Name)}();
{name}::Update& Set{Text.SnakeCaseToPascalCase(field.Identifier.Name)}({Types.GetConstAccessorTypeModification(field, bundle, type)});
"))));
                }

                if (type.Events.Count > 0)
                {
                    builder.AppendLine(Text.Indent(2, string.Join(Environment.NewLine, type.Events.Select(_event => $@"// Event {Text.SnakeCaseToPascalCase(_event.Identifier.Name)} = {_event.EventIndex}
const {Types.CollectionTypesToQualifiedTypes[Types.Collection.List]}<{Types.GetTypeDisplayName(_event.Type.Type.QualifiedName)}>& Get{Text.SnakeCaseToPascalCase(_event.Identifier.Name)}List() const;
{Types.CollectionTypesToQualifiedTypes[Types.Collection.List]}<{Types.GetTypeDisplayName(_event.Type.Type.QualifiedName)}>& Get{Text.SnakeCaseToPascalCase(_event.Identifier.Name)}List();
{name}::Update& Add{Text.SnakeCaseToPascalCase(_event.Identifier.Name)}(const {Types.GetTypeDisplayName(_event.Type.Type.QualifiedName)}&);
"))));
                }

                if (type.Fields.Count + type.Events.Count > 0)
                {
                    builder.AppendLine(Text.Indent(1, $@"private:"));
                    if (type.Fields.Count > 0)
                    {
                        builder.AppendLine(Text.Indent(2, string.Join(Environment.NewLine, type.Fields.Select(field => $"{Types.CollectionTypesToQualifiedTypes[Types.Collection.Option]}<{Types.GetFieldTypeAsCpp(field, bundle, type)}> _{Text.SnakeCaseToPascalCase(field.Identifier.Name)};"))));
                    }

                    if (type.Events.Count > 0)
                    {
                        builder.AppendLine(Text.Indent(2, string.Join(Environment.NewLine, type.Events.Select(_event => $"{Types.CollectionTypesToQualifiedTypes[Types.Collection.List]}<{Types.GetTypeDisplayName(_event.Type.Type.QualifiedName)}> _{Text.SnakeCaseToPascalCase(_event.Identifier.Name)}List;"))));
                    }
                }

                builder.AppendLine(Text.Indent(1, "};"));
                builder.AppendLine();

                if (bundle.Components[type.QualifiedName].CommandDefinitions.Count > 0)
                {
                    builder.AppendLine(Text.Indent(1, $@"class Commands
{{
public:
{Text.Indent(1, string.Join(Environment.NewLine, bundle.Components[type.QualifiedName].CommandDefinitions.Select(command => $@"class {Text.SnakeCaseToPascalCase(command.Identifier.Name)}
{{
public:
{Text.Indent(1, $@"static const Schema_FieldId CommandIndex = {command.CommandIndex};
struct Request
{{
{Text.Indent(1, $@"using Type = {Types.GetTypeDisplayName(command.RequestType.Type.QualifiedName)};
Request({string.Join($", ", types.Find(t => t.QualifiedName == command.ResponseType.Type.QualifiedName).Fields.Select(f => $"{Types.GetConstAccessorTypeModification(f, bundle, type)} {Text.SnakeCaseToPascalCase(f.Identifier.Name)}"))})
: Data({string.Join($", ", types.Find(t => t.QualifiedName == command.ResponseType.Type.QualifiedName).Fields.Select(f => $"{Text.SnakeCaseToPascalCase(f.Identifier.Name)}"))}) {{}}
Request(Type Data) : Data{{ Data }} {{}}
Type Data;")}
}};

struct Response
{{
{Text.Indent(1, $@"using Type = {Types.GetTypeDisplayName(command.ResponseType.Type.QualifiedName)};
Response({string.Join($", ", types.Find(t => t.QualifiedName == command.ResponseType.Type.QualifiedName).Fields.Select(f => $"{Types.GetConstAccessorTypeModification(f, bundle, type)} {Text.SnakeCaseToPascalCase(f.Identifier.Name)}"))})
: Data({string.Join($", ", types.Find(t => t.QualifiedName == command.ResponseType.Type.QualifiedName).Fields.Select(f => $"{Text.SnakeCaseToPascalCase(f.Identifier.Name)}"))}) {{}}
Response(Type Data) : Data{{ Data }} {{}}
Type Data;")}
}};
using RequestOp = ::improbable::CommandRequestOp<Request>;
using ResponseOp = ::improbable::CommandResponseOp<Response>;")} 
}};")))}
}};"));
                }

                builder.AppendLine(Text.Indent(1, $@"using AddComponentOp = ::improbable::AddComponentOp<{name}>;
using RemoveComponentOp = ::improbable::RemoveComponentOp<{name}>;
using ComponentUpdateOp = ::improbable::ComponentUpdateOp<Update>;
using AuthorityChangeOp = ::improbable::AuthorityChangeOp<{name}>;"));
            }

            builder.AppendLine("};");

            return builder.ToString();
        }

        private static string GenerateHashFunction(string typeName)
        {
            return $"inline uint32 GetTypeHash(const {typeName}& Value);";
        }
    }
}
