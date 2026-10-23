// Offline metadata reader: never loads or executes the inspected assembly.
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
if (args.Length != 1) return 2;
using var stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
var metadata = pe.GetMetadataReader();
var definition = metadata.GetAssemblyDefinition();
string? target = null, informational = null;
bool sawTarget = false, sawInformational = false;
foreach (var handle in definition.GetCustomAttributes())
{
    var attribute = metadata.GetCustomAttribute(handle);
    if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
    var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
    if (constructor.Parent.Kind != HandleKind.TypeReference) continue;
    var type = metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
    var fullName = metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
    if (fullName != "System.Runtime.Versioning.TargetFrameworkAttribute" && fullName != "System.Reflection.AssemblyInformationalVersionAttribute") continue;
    var value = metadata.GetBlobReader(attribute.Value);
    if (value.ReadUInt16() != 1) throw new BadImageFormatException("Invalid attribute prolog");
    var text = value.ReadSerializedString();
    if (fullName.EndsWith(".TargetFrameworkAttribute"))
    {
        if (sawTarget) throw new BadImageFormatException("Duplicate framework attribute");
        sawTarget = true;
        target = text;
    }
    else
    {
        if (sawInformational) throw new BadImageFormatException("Duplicate source attribute");
        sawInformational = true;
        informational = text;
    }
}
using var output = new Utf8JsonWriter(Console.OpenStandardOutput());
output.WriteStartObject();
output.WriteString("name", metadata.GetString(definition.Name));
output.WriteString("assemblyVersion", definition.Version.ToString());
output.WriteString("culture", definition.Culture.IsNil ? "" : metadata.GetString(definition.Culture));
output.WriteString("publicKey", Convert.ToHexString(metadata.GetBlobBytes(definition.PublicKey)));
output.WriteStartArray("runtimeReferences");
foreach (var handle in metadata.AssemblyReferences)
{
    var reference = metadata.GetAssemblyReference(handle);
    if (metadata.GetString(reference.Name) == "System.Runtime")
        output.WriteStringValue(reference.Version.ToString());
}
output.WriteEndArray();
output.WriteString("targetFramework", target);
output.WriteString("informationalVersion", informational);
output.WriteEndObject();
output.Flush();
return 0;
