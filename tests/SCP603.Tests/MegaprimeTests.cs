using Scp603.Core.Morphik;
using static Scp603.Tests.Check;
using static Scp603.Tests.TestHarness;

namespace Scp603.Tests;

public static class MegaprimeTests
{
    public static void RegisterAll()
    {
        Register("megaprime: сериализация и обратное чтение", () =>
        {
            MorphikProfile profile = MorphikProfileFactory.ForVersion(5);
            ProgramNode program = ProgramTemplate.Build(profile);
            MegaprimeArtifact artifact = MorphikToolchain.BuildArtifact(profile, program);

            byte[] bytes = artifact.Serialize();
            MegaprimeArtifact loaded = MegaprimeArtifact.Deserialize(bytes);

            Equal(artifact.ProfileDigest, loaded.ProfileDigest);
            Equal(artifact.ProfileVersion, loaded.ProfileVersion);
            Equal(artifact.Source, loaded.Source);
            Equal(artifact.Bytecode.Instructions.Length, loaded.Bytecode.Instructions.Length);

            var hostA = new RecordingMorphikHost { StopAfterEmits = 20 };
            var hostB = new RecordingMorphikHost { StopAfterEmits = 20 };
            new MorphikVm().Run(artifact.Bytecode, hostA);
            new MorphikVm().Run(loaded.Bytecode, hostB);
            SequenceEqual(hostA.Emitted.Select(Convert.ToInt64), hostB.Emitted.Select(Convert.ToInt64));
        });

        Register("megaprime: файл читается с диска", () =>
        {
            string dir = TestFs.NewTempDir();
            try
            {
                MorphikProfile profile = MorphikProfileFactory.ForVersion(3);
                MegaprimeArtifact artifact = MorphikToolchain.BuildArtifact(profile, ProgramTemplate.Build(profile));
                string path = Path.Combine(dir, "megaprime");
                artifact.Save(path);

                True(File.Exists(path));
                MegaprimeArtifact loaded = MegaprimeArtifact.Load(path);
                Equal(artifact.ProfileDigest, loaded.ProfileDigest);
            }
            finally
            {
                TestFs.Cleanup(dir);
            }
        });

        Register("megaprime: чужая сигнатура отвергается", () =>
        {
            Throws<InvalidDataException>(() => MegaprimeArtifact.Deserialize(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
        });
    }
}
