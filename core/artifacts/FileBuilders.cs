using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Hydra.Core.Tools;

namespace Hydra.Core.Artifacts;

public sealed class FileBuilders(string directory)
{
    private string Job()
    {
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path;
    }
    private static void RequireTool(string path, string name)
    { if (!File.Exists(path) || !Path.IsPathFullyQualified(path)) throw new FileNotFoundException($"Configure a trusted {name} executable in Settings → File export tools."); }

    public static string RobloxXml(string script)
    {
        return new XDocument(new XElement("roblox", new XAttribute("version", "4"),
            new XElement("External", "null"), new XElement("External", "nil"),
            new XElement("Item", new XAttribute("class", "Workspace"), new XAttribute("referent", "RBX0"),
                new XElement("Properties", new XElement("string", new XAttribute("name", "Name"), "Workspace"))),
            new XElement("Item", new XAttribute("class", "ServerScriptService"), new XAttribute("referent", "RBX1"),
                new XElement("Properties", new XElement("string", new XAttribute("name", "Name"), "ServerScriptService")),
                new XElement("Item", new XAttribute("class", "Script"), new XAttribute("referent", "RBX2"),
                    new XElement("Properties", new XElement("string", new XAttribute("name", "Name"), "HydraScript"),
                        new XElement("ProtectedString", new XAttribute("name", "Source"), script)))))).ToString();
    }
    public async Task<string> BuildRobloxAsync(string script, string rojo, CancellationToken ct)
    {
        RequireTool(rojo, "Rojo"); var job = Job();
        await File.WriteAllTextAsync(Path.Combine(job, "main.server.luau"), script, ct);
        var tree = new Dictionary<string, object> { ["$className"] = "DataModel", ["Workspace"] = new Dictionary<string, object> { ["$className"] = "Workspace" },
            ["ServerScriptService"] = new Dictionary<string, object> { ["$className"] = "ServerScriptService", ["HydraScript"] = new Dictionary<string, string> { ["$path"] = "main.server.luau" } } };
        await File.WriteAllTextAsync(Path.Combine(job, "default.project.json"), JsonSerializer.Serialize(new { name = "HydraPlace", tree }), ct);
        var output = Path.Combine(job, "HydraPlace.rbxl");
        await LocalProcess.RunAsync(rojo, ["build", "default.project.json", "--output", output], job, ct);
        await CheckHeaderAsync(output, "<roblox!"u8.ToArray(), ct); return output;
    }
    public async Task<string> BuildExeAsync(string source, string dotnet, CancellationToken ct)
    {
        RequireTool(dotnet, ".NET 10 SDK (dotnet.exe)"); var job = Job();
        await File.WriteAllTextAsync(Path.Combine(job, "Program.cs"), source, ct);
        await File.WriteAllTextAsync(Path.Combine(job, "HydraProgram.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
              <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
              <ImplicitUsings>enable</ImplicitUsings><UseAppHost>true</UseAppHost>
              <EnableNETAnalyzers>false</EnableNETAnalyzers><EnableDefaultItems>false</EnableDefaultItems>
            </PropertyGroup><ItemGroup><Compile Include="Program.cs" /></ItemGroup></Project>
            """, ct);
        await File.WriteAllTextAsync(Path.Combine(job, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>", ct);
        var output = Path.Combine(job, "output");
        await LocalProcess.RunAsync(dotnet, ["publish", "HydraProgram.csproj", "-c", "Release", "-o", output, "--self-contained", "false",
            "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false", "-p:ImportDirectoryPackagesProps=false", "-p:NuGetAudit=false",
            "-p:RestoreConfigFile=" + Path.Combine(job, "NuGet.Config")], job, ct,
            new Dictionary<string, string> { ["DOTNET_CLI_HOME"] = Path.Combine(job, "sdk-home"), ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1" });
        await CheckHeaderAsync(Path.Combine(output, "HydraProgram.exe"), "MZ"u8.ToArray(), ct);
        await File.WriteAllTextAsync(Path.Combine(output, "README.txt"), "Generated Windows app. Requires the .NET 10 runtime on the destination PC. Keep all files together. Review the source before running; Hydra has compiled but has NOT executed this program.\n", ct);
        File.Copy(Path.Combine(job, "Program.cs"), Path.Combine(output, "Program.cs"));
        var zip = Path.Combine(job, "HydraProgram-windows.zip"); ZipFile.CreateFromDirectory(output, zip); return zip;
    }
    public async Task<string> BuildMeshAsync(string json, string format, string blender, CancellationToken ct)
    {
        var mesh = MeshData.Parse(json); var job = Job();
        if (format == "obj")
        { var obj = Path.Combine(job, "HydraMesh.obj"); await File.WriteAllTextAsync(obj, mesh.ToObj(), ct); return obj; }
        if (format is not ("blend" or "fbx")) throw new ArgumentException("Unsupported mesh format.");
        RequireTool(blender, "Blender");
        await File.WriteAllTextAsync(Path.Combine(job, "mesh.json"), JsonSerializer.Serialize(mesh), ct);
        // Only this fixed script runs in Blender. Model-produced JSON is parsed as data, never executed.
        await File.WriteAllTextAsync(Path.Combine(job, "export.py"), """
            import bpy, json, sys
            args = sys.argv[sys.argv.index('--') + 1:]
            with open(args[0], encoding='utf-8') as f:
                spec = json.load(f)
            bpy.ops.object.select_all(action='SELECT')
            bpy.ops.object.delete(use_global=False)
            mesh = bpy.data.meshes.new('HydraMesh')
            mesh.from_pydata(spec['Vertices'], [], spec['Faces'])
            mesh.validate()
            mesh.update()
            obj = bpy.data.objects.new('HydraMesh', mesh)
            bpy.context.collection.objects.link(obj)
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            if args[1].endswith('.blend'):
                bpy.ops.wm.save_as_mainfile(filepath=args[1], compress=False)
            else:
                bpy.ops.export_scene.fbx(filepath=args[1], use_selection=True, object_types={'MESH'}, bake_anim=False)
            """, ct);
        var output = Path.Combine(job, "HydraMesh." + format);
        await LocalProcess.RunAsync(blender, ["--background", "--factory-startup", "--disable-autoexec", "--python-exit-code", "1", "--python", Path.Combine(job, "export.py"), "--", Path.Combine(job, "mesh.json"), output], job, ct);
        await CheckHeaderAsync(output, format == "blend" ? "BLENDER"u8.ToArray() : "Kaydara FBX Binary"u8.ToArray(), ct); return output;
    }
    private static async Task CheckHeaderAsync(string path, byte[] expected, CancellationToken ct)
    {
        await using var file = File.OpenRead(path); var actual = new byte[expected.Length]; await file.ReadExactlyAsync(actual, ct);
        if (!actual.SequenceEqual(expected)) throw new InvalidDataException("The exporter did not produce the requested file format.");
    }
}

public record MeshData(double[][] Vertices, int[][] Faces)
{
    public static MeshData Parse(string json)
    {
        if (json.Length > 8 * 1024 * 1024) throw new InvalidDataException("Mesh specification exceeds 8 MB.");
        var mesh = JsonSerializer.Deserialize<MeshData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (mesh?.Vertices is not { Length: >= 3 and <= 100000 } || mesh.Faces is not { Length: >= 1 and <= 100000 })
            throw new InvalidDataException("Mesh JSON needs vertices and faces arrays (3–100,000 vertices). ");
        if (mesh.Vertices.Any(v => v is null || v.Length != 3 || v.Any(n => !double.IsFinite(n) || Math.Abs(n) > 1_000_000)) ||
            mesh.Faces.Any(f => f is null || f.Length is < 3 or > 256 || f.Distinct().Count() != f.Length || f.Any(i => i < 0 || i >= mesh.Vertices.Length)))
            throw new InvalidDataException("Mesh coordinates or face indices are invalid.");
        return mesh;
    }
    public string ToObj()
    {
        var output = new StringBuilder("# Hydra mesh export; topology not quality-certified\no HydraMesh\n");
        foreach (var vertex in Vertices) output.AppendLine("v " + string.Join(" ", vertex.Select(n => n.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
        foreach (var face in Faces) output.AppendLine("f " + string.Join(" ", face.Select(i => i + 1)));
        return output.ToString();
    }
}
