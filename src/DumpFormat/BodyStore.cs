namespace ServiceControl.Migrate4to5.DumpFormat;

using System;
using System.IO;
using System.Security.Cryptography;

public class BodyRef
{
    public required string Sha256 { get; init; }
    public required string ContentType { get; init; }
    public required long ContentLength { get; init; }
}

public class BodyStore(DumpPaths paths)
{
    public BodyRef Store(byte[] content, string contentType)
    {
        string sha;
        using (var sha256 = SHA256.Create())
        {
            sha = BitConverter.ToString(sha256.ComputeHash(content)).Replace("-", "").ToLowerInvariant();
        }

        var path = paths.BodyPath(sha);
        if (!File.Exists(path))
        {
            // Write via temp + move so a crash never leaves a truncated body under its final name
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, content);
            File.Move(tmp, path);
        }

        return new BodyRef { Sha256 = sha, ContentType = contentType, ContentLength = content.Length };
    }

    public bool Exists(string sha256) => File.Exists(paths.BodyPath(sha256));

    public byte[] Load(string sha256) =>
        Exists(sha256)
            ? File.ReadAllBytes(paths.BodyPath(sha256))
            : throw new InvalidDumpException($"Body {sha256} referenced by a document is missing from the dump");
}
