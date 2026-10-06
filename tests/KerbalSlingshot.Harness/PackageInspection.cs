using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace KerbalSlingshot.Harness;

internal static class PackageInspection
{
    public static void Check(string plugin,string managed)
    {
        using var stream=File.OpenRead(plugin);
        using var pe=new PEReader(stream);
        MetadataReader reader=pe.GetMetadataReader();
        TypeDefinition addon=reader.TypeDefinitions.Select(h=>reader.GetTypeDefinition(h))
            .Single(t=>reader.GetString(t.Name)=="PlannerPlugin");
        if (addon.BaseType.Kind!=HandleKind.TypeReference) throw new Exception("Plugin lacks external MonoBehaviour base");
        TypeReference parent=reader.GetTypeReference((TypeReferenceHandle)addon.BaseType);
        if (reader.GetString(parent.Namespace)!="UnityEngine" || reader.GetString(parent.Name)!="MonoBehaviour")
            throw new Exception("Plugin base is not UnityEngine.MonoBehaviour");
        CustomAttribute attribute=addon.GetCustomAttributes().Select(h=>reader.GetCustomAttribute(h)).Single(a=>
        {
            if (a.Constructor.Kind!=HandleKind.MemberReference) return false;
            MemberReference ctor=reader.GetMemberReference((MemberReferenceHandle)a.Constructor);
            return ctor.Parent.Kind==HandleKind.TypeReference && reader.GetString(reader.GetTypeReference((TypeReferenceHandle)ctor.Parent).Name)=="KSPAddon";
        });
        using var kspStream=File.OpenRead(Path.Combine(managed,"Assembly-CSharp.dll"));
        using var kspPe=new PEReader(kspStream);
        MetadataReader ksp=kspPe.GetMetadataReader();
        TypeDefinition startup=ksp.TypeDefinitions.Select(h=>ksp.GetTypeDefinition(h)).Single(t=>
            ksp.GetString(t.Name)=="Startup" && !t.GetDeclaringType().IsNil && ksp.GetString(ksp.GetTypeDefinition(t.GetDeclaringType()).Name)=="KSPAddon");
        FieldDefinition flight=startup.GetFields().Select(h=>ksp.GetFieldDefinition(h)).Single(f=>ksp.GetString(f.Name)=="Flight");
        BlobReader constant=ksp.GetBlobReader(ksp.GetConstant(flight.GetDefaultValue()).Value);
        int flightValue=constant.ReadInt32();
        BlobReader args=reader.GetBlobReader(attribute.Value);
        if (args.ReadUInt16()!=1 || args.ReadInt32()!=flightValue || args.ReadByte()!=0)
            throw new Exception("KSPAddon is not nonpersistent Flight startup");
        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            AssemblyReference reference=reader.GetAssemblyReference(handle);
            string name=reader.GetString(reference.Name);
            string path=Path.Combine(name=="KerbalSlingshot.Core"?Path.GetDirectoryName(plugin)!:managed,name+".dll");
            AssemblyName installed=AssemblyName.GetAssemblyName(path); // Metadata only; does not load or execute KSP.
            if (installed.Name!=name || installed.Version!=reference.Version) throw new Exception("Reference identity mismatch: "+name);
            Console.WriteLine("PASS runtime reference identity "+name+" "+reference.Version);
        }
        Console.WriteLine("PASS actual MonoBehaviour plugin with KSPAddon Flight startup; metadata inspected without executing game assemblies.");
    }
}
