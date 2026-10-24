// Construit le zip du plugin et met à jour le manifest.json du dépôt de plugins Jellyfin.
//
//   dotnet run scripts/package.cs -- --base-url https://exemple.com/partyflin [--out dist] [--manifest dist/manifest.json] [--version 1.0.0.0]
//
// --base-url : adresse publique où le zip sera téléchargeable (sans le nom du fichier).
// --version  : remplace la version de build.yaml (utilisé par la CI à partir du tag).
// Les versions déjà présentes dans le manifest sont conservées ; la version courante est remplacée.

using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

var root = Directory.GetCurrentDirectory();
string? baseUrl = null;
var outDir = Path.Combine(root, "dist");
string? manifestPath = null;
string? versionOverride = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--base-url": baseUrl = args[++i].TrimEnd('/'); break;
        case "--out": outDir = Path.GetFullPath(args[++i]); break;
        case "--manifest": manifestPath = Path.GetFullPath(args[++i]); break;
        case "--version": versionOverride = args[++i].TrimStart('v'); break;
    }
}

if (baseUrl is null)
{
    Console.Error.WriteLine("Usage : dotnet run scripts/package.cs -- --base-url <url> [--out dist] [--manifest <fichier>] [--version <x.y.z.w>]");
    return 1;
}

var meta = ReadBuildYaml(Path.Combine(root, "build.yaml"));
var version = versionOverride ?? meta["version"];
manifestPath ??= Path.Combine(outDir, "manifest.json");
var project = Path.Combine(root, "Jellyfin.Plugin.Partyflin");
var publishDir = Path.Combine(project, "bin", "package");

Run("dotnet", $"publish \"{project}\" -c Release -o \"{publishDir}\" -p:Version={version} -p:AssemblyVersion={version} -p:FileVersion={version}");

Directory.CreateDirectory(outDir);
var zipName = $"partyflin_{version}.zip";
var zipPath = Path.Combine(outDir, zipName);
File.Delete(zipPath);
using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
{
    foreach (var artifact in meta["artifacts"].Split(','))
    {
        zip.CreateEntryFromFile(Path.Combine(publishDir, artifact), artifact);
    }
}

string checksum;
using (var stream = File.OpenRead(zipPath))
{
    checksum = Convert.ToHexString(MD5.HashData(stream)).ToLowerInvariant();
}

var manifest = File.Exists(manifestPath) ? JsonNode.Parse(File.ReadAllText(manifestPath))!.AsArray() : new JsonArray();
var plugin = manifest.FirstOrDefault(p => (string?)p?["guid"] == meta["guid"])?.AsObject();
if (plugin is null)
{
    plugin = new JsonObject { ["guid"] = meta["guid"], ["versions"] = new JsonArray() };
    manifest.Add(plugin);
}

plugin["name"] = meta["name"];
plugin["overview"] = meta["overview"];
plugin["description"] = meta["description"];
plugin["owner"] = meta["owner"];
plugin["category"] = meta["category"];
plugin["imageUrl"] = meta.GetValueOrDefault("imageUrl", string.Empty);

var versions = plugin["versions"]!.AsArray();
foreach (var old in versions.Where(v => (string?)v?["version"] == version).ToList())
{
    versions.Remove(old);
}

versions.Insert(0, new JsonObject
{
    ["version"] = version,
    ["changelog"] = meta["changelog"],
    ["targetAbi"] = meta["targetAbi"],
    ["sourceUrl"] = $"{baseUrl}/{zipName}",
    ["checksum"] = checksum,
    ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
});

File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
}));

Console.WriteLine($"{zipPath} (md5 {checksum})");
Console.WriteLine(manifestPath);
return 0;

static void Run(string file, string arguments)
{
    using var process = Process.Start(new ProcessStartInfo(file, arguments))!;
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"{file} {arguments} a échoué ({process.ExitCode})");
    }
}

// Lecteur minimal de build.yaml : scalaires, blocs repliés (>) et listes simples (jointes par des virgules).
static Dictionary<string, string> ReadBuildYaml(string path)
{
    var values = new Dictionary<string, string>();
    string? key = null;
    var block = new List<string>();

    void Flush()
    {
        if (key is not null && block.Count > 0)
        {
            values[key] = string.Join(key == "artifacts" ? "," : " ", block);
        }

        key = null;
        block.Clear();
    }

    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.TrimEnd();
        if (line.Length == 0 || line.TrimStart().StartsWith('#'))
        {
            continue;
        }

        if (char.IsWhiteSpace(line[0]) && key is not null)
        {
            block.Add(line.Trim().TrimStart('-').Trim().Trim('"'));
            continue;
        }

        Flush();
        var colon = line.IndexOf(':');
        var name = line[..colon].Trim();
        var value = line[(colon + 1)..].Trim();
        if (value is "" or ">" or "|")
        {
            key = name;
        }
        else
        {
            values[name] = value.Trim('"');
        }
    }

    Flush();
    return values;
}
