using System.IO.Compression;
using System.Xml.Linq;
using Hydra.Core.Artifacts;
using Hydra.Core.Tools;

internal static class ArtifactChecks
{
    public static void Register(string root, Action<string, Func<Task>> test)
    {
        static void Check(bool condition) { if (!condition) throw new Exception("Artifact assertion failed."); }
        static async Task Reject<T>(Func<Task> action) where T : Exception
        { try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        test("Generated file extraction preserves source and rejects unsafe filenames", async () =>
        {
            var files = GeneratedFiles.Extract("Here is a file:\n```html filename=index.html\n<h1>Hello</h1>\n```\n```html filename=../../outside.html\n<p>data</p>\n```\n```exe filename=bad.exe\nnot binary\n```\n");
            Check(files.Count == 3 && files[0].Name == "index.html" && files[0].Content == "<h1>Hello</h1>" && files[1].Name == "generated-2.html" && files[2].Name.EndsWith(".txt"));
            Check(GeneratedFiles.Extract("```html\nunfinished").Count == 0);
            var store = new ArtifactStore(Path.Combine(root, "artifact-test"));
            await Reject<InvalidDataException>(() => store.SaveTextAsync(new("fake.exe", "text", "exe"), null, "test"));
            await Reject<ArgumentException>(() => store.SaveTextAsync(new("CON.txt", "text", "text"), null, "test"));
        });
        test("Artifact library survives reopen, avoids manifest collisions and detects tampering", async () =>
        {
            var folder = Path.Combine(root, "artifact-library"); var store = new ArtifactStore(folder);
            var saved = await store.SaveTextAsync(new("artifact.json", "{\"test\":true}", "json"), "chat-one", "test fixture");
            var reopened = new ArtifactStore(folder); Check(reopened.List("chat-one").Single().Name == "artifact.json");
            Check(await File.ReadAllTextAsync(store.PathFor(saved)) == "{\"test\":true}");
            await File.AppendAllTextAsync(store.PathFor(saved), "tamper");
            await Reject<InvalidDataException>(() => store.CopyToAsync(saved, Path.Combine(root, "tampered-export.json")));
        });
        test("Obsidian snapshots preserve existing files and support binary outputs", async () =>
        {
            var vault = Path.Combine(root, "vault"); Directory.CreateDirectory(vault);
            await File.WriteAllTextAsync(Path.Combine(vault, "README.md"), "user note");
            var store = new ArtifactStore(Path.Combine(root, "backup-artifacts"));
            var artifact = await store.SaveTextAsync(new("README.md", "generated note", "md"), null, "test");
            var backup = new BackupService(Path.Combine(root, "backup-work"));
            var configuration = new BackupSettings(vault, "", "", false);
            var result = await backup.BackUpAsync(store, artifact, configuration, default);
            Check(result.Single() == "Obsidian vault backup copied.");
            await backup.BackUpAsync(store, artifact, configuration, default);
            Check(await File.ReadAllTextAsync(Path.Combine(vault, "README.md")) == "user note");
            Check(Directory.GetFiles(Path.Combine(vault, "HydraAI-Backups"), "README.md", SearchOption.AllDirectories).Length == 4);
            var bytes = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            var binary = Path.Combine(root, "binary-test.fbx"); await File.WriteAllBytesAsync(binary, bytes);
            var imported = await store.ImportAsync(binary, null, "user file fixture");
            await backup.BackUpAsync(store, imported, configuration, default);
            var copy = Directory.GetFiles(vault, "binary-test.fbx", SearchOption.AllDirectories).Single(); Check(File.ReadAllBytes(copy).SequenceEqual(bytes));
        });
        test("Mesh parsing validates topology bounds and emits actual OBJ", async () =>
        {
            var mesh = MeshData.Parse(Triangle); Check(mesh.ToObj().Contains("f 1 2 3"));
            await Reject<System.IO.InvalidDataException>(() => { MeshData.Parse("{\"vertices\":[[0,0,0],[1,0,0],[0,1,0]],\"faces\":[[0,1,99]]}"); return Task.CompletedTask; });
            var builder = new FileBuilders(Path.Combine(root, "mesh-builds"));
            var path = await builder.BuildMeshAsync(Triangle, "obj", "", default); Check(File.ReadAllText(path).Contains("v 1 0 0"));
            await Reject<FileNotFoundException>(() => builder.BuildMeshAsync(Triangle, "blend", "missing.exe", default));
        });
        test("Roblox XML place contains escaped script source, not renamed binary", () =>
        {
            const string source = "print('</ProtectedString> & hello')";
            var doc = XDocument.Parse(FileBuilders.RobloxXml(source));
            Check(doc.Root!.Name == "roblox" && doc.Descendants("ProtectedString").Single().Value == source);
            Check(doc.Descendants("Item").Any(e => e.Attribute("class")?.Value == "ServerScriptService")); return Task.CompletedTask;
        });
        if (Environment.GetEnvironmentVariable("HYDRA_TEST_GIT") is { } git)
            test("REAL Git backup commits only exports and preserves unrelated staged work", async () =>
            {
                var repo = Path.Combine(root, "git-backup"); Directory.CreateDirectory(repo);
                Task<string> Git(params string[] args) => LocalProcess.RunAsync(git, new[] { "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false" }.Concat(args), repo, default);
                await Git("init", "-b", "main"); await File.WriteAllTextAsync(Path.Combine(repo, "base.txt"), "base");
                await Git("add", "base.txt"); await Git("commit", "-m", "base");
                await File.WriteAllTextAsync(Path.Combine(repo, "user-staged.txt"), "unrelated staged work"); await Git("add", "user-staged.txt");
                var store = new ArtifactStore(Path.Combine(root, "git-artifacts")); var artifact = await store.SaveTextAsync(new("page.html", "<p>actual source</p>", "html"), null, "test");
                var results = await new BackupService(Path.Combine(root, "git-work")).BackUpAsync(store, artifact, new("", repo, git, true), default);
                Check(results.Single() == "Git backup copied and committed locally.");
                Check(await Git("diff", "--cached", "--name-only") == "user-staged.txt");
                var tree = await Git("ls-tree", "-r", "--name-only", "HEAD");
                Check(tree.Contains("page.html") && !tree.Contains("user-staged.txt"));
                Check((await Git("status", "--porcelain")).Split('\n').Length == 1);
            });
        if (Environment.GetEnvironmentVariable("HYDRA_TEST_DOTNET") is { } dotnet)
            test("REAL C# compiler produces EXE and companion files without executing it", async () =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var zip = await new FileBuilders(Path.Combine(root, "exe-build")).BuildExeAsync("Console.WriteLine(\"Hydra compiler test fixture\");", dotnet, timeout.Token);
                using var archive = ZipFile.OpenRead(zip); Check(archive.GetEntry("HydraProgram.runtimeconfig.json") is not null);
                using var exe = archive.GetEntry("HydraProgram.exe")!.Open(); Check(exe.ReadByte() == 'M' && exe.ReadByte() == 'Z');
            });
        if (Environment.GetEnvironmentVariable("HYDRA_TEST_ROJO") is { } rojo)
            test("REAL Rojo creates a binary Roblox place", async () =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                var path = await new FileBuilders(Path.Combine(root, "rojo-build")).BuildRobloxAsync("print('Hydra export test fixture')", rojo, timeout.Token);
                Check(new FileInfo(path).Length > 100);
            });
        if (Environment.GetEnvironmentVariable("HYDRA_TEST_BLENDER") is { } blender)
            test("REAL Blender exports BLEND and FBX from validated mesh data", async () =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var builder = new FileBuilders(Path.Combine(root, "blender-build"));
                foreach (var format in new[] { "blend", "fbx" })
                { var path = await builder.BuildMeshAsync(Triangle, format, blender, timeout.Token); Check(new FileInfo(path).Length > 1000); }
            });
    }
    internal const string Triangle = "{\"vertices\":[[0,0,0],[1,0,0],[0,1,0]],\"faces\":[[0,1,2]]}";
}
