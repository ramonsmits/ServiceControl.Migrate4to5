namespace ServiceControl.Migrate4to5.DumpFormat;

using System.IO;

public class DumpPaths
{
    public DumpPaths(string root)
    {
        Root = root;
        CollectionsDir = Path.Combine(root, "collections");
        BodiesDir = Path.Combine(root, "bodies");
        ManifestPath = Path.Combine(root, "manifest.json");
        Directory.CreateDirectory(CollectionsDir);
        Directory.CreateDirectory(BodiesDir);
    }

    public string Root { get; }
    public string ManifestPath { get; }
    public string CollectionsDir { get; }
    public string BodiesDir { get; }

    public string CollectionFile(string logicalName) => Path.Combine(CollectionsDir, logicalName + ".jsonl");
    public string BodyPath(string sha256) => Path.Combine(BodiesDir, sha256);
}
