using System.Text;

namespace Scp603.Core.Morphik;

/// <summary>
/// The persisted result of compiling a Morphik program. SCP-603 always names this artefact
/// "megaprime". It is a self describing container: profile digest + embedded source + bytecode.
/// </summary>
public sealed class MegaprimeArtifact
{
    public const int CurrentFormat = 1;
    public static readonly byte[] Magic = { (byte)'M', (byte)'G', (byte)'P', (byte)'R' };

    public int Format { get; init; } = CurrentFormat;
    public int ProfileVersion { get; init; }
    public ulong ProfileDigest { get; init; }
    public string Source { get; init; } = string.Empty;
    public required MorphikBytecode Bytecode { get; init; }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        w.Write(Magic);
        w.Write(Format);
        w.Write(ProfileVersion);
        w.Write(ProfileDigest);

        byte[] sourceBytes = Encoding.UTF8.GetBytes(Source);
        w.Write(sourceBytes.Length);
        w.Write(sourceBytes);

        w.Write(Bytecode.Instructions.Length);
        foreach (Instruction ins in Bytecode.Instructions)
        {
            w.Write((int)ins.Op);
            w.Write(ins.A);
            w.Write(ins.S is not null);
            if (ins.S is not null)
            {
                w.Write(ins.S);
            }
        }

        w.Flush();
        return ms.ToArray();
    }

    public static MegaprimeArtifact Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);
        byte[] magic = r.ReadBytes(4);
        if (magic.Length != 4 || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
        {
            throw new InvalidDataException("Это не megaprime: неверная сигнатура.");
        }

        int format = r.ReadInt32();
        int profileVersion = r.ReadInt32();
        ulong digest = r.ReadUInt64();

        int sourceLen = r.ReadInt32();
        string source = Encoding.UTF8.GetString(r.ReadBytes(sourceLen));

        int count = r.ReadInt32();
        var instructions = new Instruction[count];
        for (int i = 0; i < count; i++)
        {
            var op = (BcOp)r.ReadInt32();
            long a = r.ReadInt64();
            bool hasS = r.ReadBoolean();
            string? s = hasS ? r.ReadString() : null;
            instructions[i] = new Instruction(op, a, s);
        }

        return new MegaprimeArtifact
        {
            Format = format,
            ProfileVersion = profileVersion,
            ProfileDigest = digest,
            Source = source,
            Bytecode = new MorphikBytecode(instructions),
        };
    }

    public void Save(string path)
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(path, Serialize());
    }

    public static MegaprimeArtifact Load(string path) => Deserialize(File.ReadAllBytes(path));
}
