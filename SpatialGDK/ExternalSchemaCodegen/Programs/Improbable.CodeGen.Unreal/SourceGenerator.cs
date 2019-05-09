using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Improbable.CodeGen.Base;

namespace Improbable.CodeGen.Unreal
{
    public static class SourceGenerator
    {
        public static string GenerateSource(TypeDescription type, List<TypeDescription> types, Dictionary<string, TypeGeneratedCode> allGeneratedTypeContent, Bundle bundle)
        {
            var sourceRef = bundle.SchemaBundle.SourceMapV1.SourceReferences[type.QualifiedName];
            var allNestedTypes = Types.GetRecursivelyNestedTypes(type);
            var allNestedEnums = Types.GetRecursivelyNestedEnums(type);
            var typeNamespaces = Text.GetNamespaceFromTypeName(type.QualifiedName);

            var builder = new StringBuilder();

            builder.AppendLine($@"// Generated by {UnrealGenerator.GeneratorTitle}

#include <set>
#include ""{UnrealGenerator.RelativeIncludePrefix}/{Types.TypeToHeaderFilename(type.QualifiedName)}""
#include ""{UnrealGenerator.RelativeIncludePrefix}/{MapEquals.HeaderName}""

// Generated from {sourceRef.FilePath}({sourceRef.Line},{sourceRef.Column})
{string.Join(Environment.NewLine, typeNamespaces.Select(t => $"namespace {t} {{"))}

{GenerateTypeFunctions(type.Name, type, bundle)}");

            if (type.IsComponent)
            {
                builder.AppendLine(GenerateComponentUpdateFunctions(type.Name, type, bundle));
            }

            if (allNestedTypes.Count() > 0)
            {
                builder.AppendLine(string.Join(Environment.NewLine, allNestedTypes.Select(topLevelType => GenerateTypeFunctions(Types.GetTypeClassDefinitionName(topLevelType.QualifiedName, bundle), types.Find(t => t.QualifiedName == topLevelType.QualifiedName), bundle))));
            }

            builder.AppendLine(GenerateHashFunction(type, bundle));

            if (allNestedTypes.Count() > 0)
            {
                builder.AppendLine(string.Join(Environment.NewLine, allNestedTypes.Select(topLevelType => GenerateHashFunction(topLevelType, bundle))));
            }
            if (allNestedEnums.Count() > 0)
            {
                builder.AppendLine(string.Join(Environment.NewLine, allNestedEnums.Select(nestedEnum => GenerateHashFunction(nestedEnum, bundle))));
            }

            builder.AppendLine(string.Join(Environment.NewLine, typeNamespaces.Reverse().Select(t => $"}} // namespace {t}")));

            return builder.ToString();        
        }

        private static string GenerateTypeFunctions(string name, TypeDescription type, Bundle bundle)
        {
            var argType = type.IsComponent ? "Schema_ComponentData" : "Schema_Object";
            var argName = type.IsComponent ? "ComponentData" : "SchemaObject";
            var componentFieldsName = "FieldsObject";
            var targetSchemaObject = type.IsComponent ? componentFieldsName : argName;

            var builder = new StringBuilder();

            if (type.Fields.Count > 0)
            {
                builder.AppendLine($@"{name}::{name}(
{Text.Indent(1, $"{string.Join($", {Environment.NewLine}", type.Fields.Select(f => $"{Types.GetConstAccessorTypeModification(f, bundle, type)} {Text.SnakeCaseToPascalCase(f.Identifier.Name)}"))})")}
: {string.Join($"{Environment.NewLine}, ", type.Fields.Select(f => $"_{Text.SnakeCaseToPascalCase(f.Identifier.Name)}{{ {Text.SnakeCaseToPascalCase(f.Identifier.Name)} }}"))} {{}}
");
            }

            builder.AppendLine($@"{name}::{name}() {{}}

bool {name}::operator==(const {name}& Value) const
{{
{Text.Indent(1, type.Fields.Count == 0
? "return true;"
: $"return {string.Join($@" && {Environment.NewLine}", type.Fields.Select(f => Types.GetFieldDefinitionEquals(f, $"_{Text.SnakeCaseToPascalCase(f.Identifier.Name)}", "Value")))};")}
}}

bool {name}::operator!=(const {name}& Value) const
{{
{Text.Indent(1, $"return !operator== (Value);")}
}}
");
            if (type.Fields.Count() > 0)
            {
                builder.AppendLine($@"{string.Join(Environment.NewLine, type.Fields.Select(field => $@"{Types.GetConstAccessorTypeModification(field, bundle, type)} {name}::Get{Text.SnakeCaseToPascalCase(field.Identifier.Name)}() const
{{
{Text.Indent(1, $"return _{Text.SnakeCaseToPascalCase(field.Identifier.Name)};")}
}}

{Types.GetFieldTypeAsCpp(field, bundle, type)}& {name}::Get{Text.SnakeCaseToPascalCase(field.Identifier.Name)}()
{{
{Text.Indent(1, $"return _{ Text.SnakeCaseToPascalCase(field.Identifier.Name)}; ")}
}}

{name}& {name}::Set{Text.SnakeCaseToPascalCase(field.Identifier.Name)}({Types.GetConstAccessorTypeModification(field, bundle, type)} Value)
{{
{Text.Indent(1, $@"_{ Text.SnakeCaseToPascalCase(field.Identifier.Name)} = Value;
return *this;")}
}}
"))}");
            }

            if (type.Fields.Count == 0 && type.Events == null)
            {
                builder.AppendLine($@"void {name}::Serialize({argType}* {argName}) const {{}}

{name} {name}::Deserialize({argType}* {argName})
{{
{Text.Indent(1, $"return {name}::Create();")}
}}");
            }
            else
            {
                builder.AppendLine($@"void {name}::Serialize({argType}* {argName}) const
{{");
                if (type.IsComponent)
                {
                    builder.AppendLine(Text.Indent(1, $"Schema_Object* {componentFieldsName} = Schema_GetComponentDataFields({argName});"));
                    builder.AppendLine();
                }

                builder.AppendLine($@"{Text.Indent(1, string.Join(Environment.NewLine, type.Fields.Select(field => $@"// serializing field {Text.SnakeCaseToPascalCase(field.Identifier.Name)} = {field.FieldId}
{Serialization.GetFieldSerialization(field, targetSchemaObject, $"_{Text.SnakeCaseToPascalCase(field.Identifier.Name)}", type, bundle)}")))}
}}

{name} {name}::Deserialize({argType}* {argName})
{{");

                if (type.IsComponent)
                {
                    builder.AppendLine(Text.Indent(1, $"Schema_Object * {componentFieldsName} = Schema_GetComponentDataFields({argName});"));
                }

                builder.AppendLine($@"{Text.Indent(1, $@"{name} Data;

{string.Join(Environment.NewLine, type.Fields.Select(field => $@"// deserializing field {Text.SnakeCaseToPascalCase(field.Identifier.Name)} = {field.FieldId}
{Serialization.GetFieldDeserialization(field, targetSchemaObject, $"Data._{Text.SnakeCaseToPascalCase(field.Identifier.Name)}", type, bundle)}
"))}
return Data;")}
}}");
            }

            return builder.ToString();
        }

        private static string GenerateComponentUpdateFunctions(string name, TypeDescription type, Bundle bundle)
        {
            var updatesObjectName = "UpdatesObject";
            var eventsObjectName = "EventsObject";
            var componentUpdateObjectName = "ComponentUpdate";
            var deserializingTargetObjectName = "Data";

            var builder = new StringBuilder();

            builder.AppendLine($@"bool {name}::Update::operator==(const {name}::Update& Value) const
{{
{Text.Indent(1, type.Fields.Count == 0 ? "return true;"
: $"return {string.Join($@" && {Environment.NewLine}", type.Fields.Select(f => Types.GetFieldDefinitionEquals(f, $"_{Text.SnakeCaseToPascalCase(f.Identifier.Name)}", "Value")))};")}
}}

bool {name}::Update::operator!=(const {name}::Update& Value) const
{{
{Text.Indent(1, $"return !operator== (Value);")}
}}

{name}::Update {name}::Update::FromInitialData(const {name}& Data)
{{ 
{Text.Indent(1, $@"{name}::Update Update;
{string.Join(Environment.NewLine, type.Fields.Select(f => $"Update._{Text.SnakeCaseToPascalCase(f.Identifier.Name)} = Data.Get{Text.SnakeCaseToPascalCase(f.Identifier.Name)}();"))}
return Update;")}
}}

{name} {name}::Update::ToInitialData() const
{{
{Text.Indent(1, $@"return {name} (
{string.Join($",{Environment.NewLine}", type.Fields.Select(f => Text.Indent(1, $"*_{Text.SnakeCaseToPascalCase(f.Identifier.Name)}")))});")}
}}         

void {name}::Update::ApplyTo({name}& Data) const
{{
{Text.Indent(1, string.Join(Environment.NewLine, type.Fields.Select(field => $@"if (_{Text.SnakeCaseToPascalCase(field.Identifier.Name)})
{{
{Text.Indent(1, $@"Data.Set{Text.SnakeCaseToPascalCase(field.Identifier.Name)}(*_{Text.SnakeCaseToPascalCase(field.Identifier.Name)});")}
}}")))}
}}
");
            if (type.Fields.Count() > 0)
            {
                builder.AppendLine(string.Join(Environment.NewLine, type.Fields.Select(field => $@"const {Types.CollectionTypesToQualifiedTypes[Types.Collection.Option]}<{Types.GetFieldTypeAsCpp(field, bundle, type)}>& {name}::Update::Get{Text.SnakeCaseToPascalCase(field.Identifier.Name)}() const
{{
{Text.Indent(1, $"return _{Text.SnakeCaseToPascalCase(field.Identifier.Name)};")}
}}

{Types.CollectionTypesToQualifiedTypes[Types.Collection.Option]}<{Types.GetFieldTypeAsCpp(field, bundle, type)}>& {name}::Update::Get{Text.SnakeCaseToPascalCase(field.Identifier.Name)}()
{{
{Text.Indent(1, $"return _{Text.SnakeCaseToPascalCase(field.Identifier.Name)};")}
}}

{name}::Update& {name}::Update::Set{Text.SnakeCaseToPascalCase(field.Identifier.Name)}({Types.GetConstAccessorTypeModification(field, bundle, type)} value)
{{
{Text.Indent(1, $@"_{ Text.SnakeCaseToPascalCase(field.Identifier.Name)} = value;
return *this;")}
}}
")));
            }

            if (type.Events.Count() > 0)
            {
                builder.AppendLine(string.Join(Environment.NewLine, type.Events.Select(_event => $@"const {Types.CollectionTypesToQualifiedTypes[Types.Collection.List]}< {Types.GetTypeDisplayName(_event.Type.Type.QualifiedName)} >& {name}::Update::Get{Text.SnakeCaseToPascalCase(_event.Identifier.Name)}List() const
{{
{Text.Indent(1, $"return _{Text.SnakeCaseToPascalCase(_event.Identifier.Name)}List;")}
}}

{Types.CollectionTypesToQualifiedTypes[Types.Collection.List]}< {Types.GetTypeDisplayName(_event.Type.Type.QualifiedName)} >& {name}::Update::Get({Text.SnakeCaseToPascalCase(_event.Identifier.Name)}List()
{{
{Text.Indent(1, $"return _{Text.SnakeCaseToPascalCase(_event.Identifier.Name)}List;")}
}}

{ name}::Update& {name}::Update::Add{Text.SnakeCaseToPascalCase(_event.Identifier.Name)}(const {Types.GetTypeDisplayName(_event.Type.Type.QualifiedName)}& Value)
{{
{Text.Indent(1, $@"_{Text.SnakeCaseToPascalCase(_event.Identifier.Name)}List.Add(Value);
return *this;")}
}}
")));
            }

            if (type.Fields.Count == 0 && type.Events.Count == 0)
            {
                builder.AppendLine($@"void {name}::Update::Serialize(Schema_ComponentUpdate* ComponentUpdate) const {{}}

{name}::Update {name}::Update::Deserialize(Schema_ComponentUpdate* ComponentUpdate)
{{
{Text.Indent(1, $"return {name}::Update::Create();")}
}}");
                return builder.ToString();
            }

            builder.AppendLine($@"void {name}::Update::Serialize(Schema_ComponentUpdate* ComponentUpdate) const
{{");
            if (type.Fields.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, $"Schema_Object* {updatesObjectName} = Schema_GetComponentUpdateFields(ComponentUpdate);"));
            }

            if (type.Events.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, $"Schema_Object* {eventsObjectName} = Schema_GetComponentUpdateEvents(ComponentUpdate);"));
            }

            if (type.Fields.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, string.Join(Environment.NewLine, type.Fields.Select(field => $@"// serializing field {Text.SnakeCaseToPascalCase(field.Identifier.Name)} = {field.FieldId}
if (_{Text.SnakeCaseToPascalCase(field.Identifier.Name)}.IsSet())
{{
{Text.Indent(1, field.TypeSelector != FieldType.Singular ? $@"if ({Serialization.GetFieldClearingCheck(field)})
{{
{Text.Indent(1, $"Schema_AddComponentUpdateClearedField(ComponentUpdate, {field.FieldId});")}
}}
else
{{
{Text.Indent(1, Serialization.GetFieldSerialization(field, updatesObjectName, $"(*_{Text.SnakeCaseToPascalCase(field.Identifier.Name)})", type, bundle))}
}}"
:
Serialization.GetFieldSerialization(field, updatesObjectName, $"(*_{Text.SnakeCaseToPascalCase(field.Identifier.Name)})", type, bundle))}
}}"))));
            }

            if (type.Events.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, $@"{Text.Indent(1, string.Join(Environment.NewLine, type.Events.Select(_event => $@"// serializing event {Text.SnakeCaseToPascalCase(_event.Identifier.Name)} = {_event.EventIndex}
for (const {Types.GetTypeDisplayName(_event.Type.Type.QualifiedName)}& Element : _{Text.SnakeCaseToPascalCase(_event.Identifier.Name)}List)
{{
{Text.Indent(1, $"Element.Serialize(Schema_AddObject({eventsObjectName}, {_event.EventIndex}));")}
}}")))}"));
            }

            builder.AppendLine("}");
            builder.AppendLine();

            builder.AppendLine($@"{name}::Update { name}::Update::Deserialize(Schema_ComponentUpdate * ComponentUpdate)
{{");

            if (type.Fields.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, $"Schema_Object* {updatesObjectName} = Schema_GetComponentUpdateFields({componentUpdateObjectName});"));
            }

            if (type.Events.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, $"Schema_Object* {eventsObjectName} = Schema_GetComponentUpdateEvents({componentUpdateObjectName});"));
            }

            builder.AppendLine(Text.Indent(1, $@"auto FieldsToClear = new Schema_FieldId[Schema_GetComponentUpdateClearedFieldCount({componentUpdateObjectName})];
Schema_GetComponentUpdateClearedFieldList({componentUpdateObjectName}, FieldsToClear);
std::set<Schema_FieldId> FieldsToClearSet(FieldsToClear, FieldsToClear + sizeof(FieldsToClear) / sizeof(Schema_FieldId));

{name}::Update {deserializingTargetObjectName};
"));

            if (type.Fields.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, string.Join(Environment.NewLine, type.Fields.Select(field => $@"// deserializing field {Text.SnakeCaseToPascalCase(field.Identifier.Name)} = {field.FieldId}
if ({Serialization.GetFieldTypeCount(field, updatesObjectName)} > 0)
{{
{Text.Indent(1, Serialization.GetFieldDeserialization(field, updatesObjectName, $"{deserializingTargetObjectName}._{Text.SnakeCaseToPascalCase(field.Identifier.Name)}", type, bundle, false, true))}
}}
{(field.TypeSelector != FieldType.Singular ? $@"else if (FieldsToClearSet.count({field.FieldId})) // only check if lists, maps, or options should be cleared
{{
{Text.Indent(1, $"{deserializingTargetObjectName}._{Text.SnakeCaseToPascalCase(field.Identifier.Name)} = {{}};")}
}}
" : string.Empty)}"))));
            }

            if (type.Events.Count > 0)
            {
                builder.AppendLine(Text.Indent(1, string.Join(Environment.NewLine, type.Events.Select(_event => $@"// deserializing event {Text.SnakeCaseToPascalCase(_event.Identifier.Name)} = {_event.EventIndex}
{Serialization.GetEventDeserialization(_event, eventsObjectName, deserializingTargetObjectName)}
"))));
            }

            builder.AppendLine($@"{Text.Indent(1, $@"for (uint32 i = 0; i < Schema_GetComponentUpdateClearedFieldCount({componentUpdateObjectName}); ++i)
{{
{Text.Indent(1, $"auto clearedFieldId = Schema_IndexComponentUpdateClearedField({componentUpdateObjectName}, i);")}
}}

return {deserializingTargetObjectName};")}
}}");

            return builder.ToString();
        }

        private static string GenerateHashFunction(TypeDescription type, Bundle bundle)
        {
            return $@"uint32 GetTypeHash(const {Types.GetTypeClassDefinitionQualifiedName(type.QualifiedName, bundle)}& Value)
{{
{Text.Indent(1, $@"uint32 Result = 1327;
{string.Join(Environment.NewLine, type.Fields.Select(field => $"{Types.GetFieldDefinitionHash($"Value.Get{Text.SnakeCaseToPascalCase(field.Identifier.Name)}()", field, "Result", bundle)}"))}
return Result;")}
}}
";
        }

        private static string GenerateHashFunction(EnumDefinition enumDef, Bundle bundle)
        {
            return $@"uint32 GetTypeHash(const {Types.GetTypeClassDefinitionQualifiedName(enumDef.Identifier.QualifiedName, bundle)}& Value)
{{
{Text.Indent(1, "return static_cast<size_t>(Value);")}
}}";
        }
    }
}
