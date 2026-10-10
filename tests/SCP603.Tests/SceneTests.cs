using Scp603.Core.Morphik;
using Scp603.Core.Rendering;
using Scp603.Core.Scenes;
using static Scp603.Tests.Check;
using static Scp603.Tests.TestHarness;

namespace Scp603.Tests;

public static class SceneTests
{
    private static MorphikProfile Profile => MorphikProfileFactory.ForVersion(1);

    private static T Start<T>(T scene, ulong seed = 12345)
        where T : IScene
    {
        scene.Start(Profile, seed);
        return scene;
    }

    public static void RegisterAll()
    {
        Register("scene 44 «Умереть»: команды и завершение", () =>
        {
            var die = Start(new DieScene());
            die.Execute("оглядеться");
            Contains(die.ToText(), "маяк");

            die.Execute("идти на север");
            Contains(die.ToText(), "Ты не можешь пойти на север.");

            die.Execute("поднять яблоко");
            Contains(die.ToText(), "яблоко");

            die.Execute("петь");
            Contains(die.ToText(), "Ты не можешь петь.");

            die.Execute("умереть");
            Equal(SceneStatus.Finished, die.Status);
            Contains(die.ToText(), "черноту");
            Contains(die.ToText(), "СИМУЛЯЦИЯ");
        });

        Register("scene 20 «Яков»: детерминированные ответы", () =>
        {
            var jacob = Start(new JacobScene(), 7);
            Equal("Привет.", jacob.Respond("здравствуйте"));
            Equal("Яков.", jacob.Respond("кто ты?"));
            Equal("Вас зовут Джозеф Браун.", jacob.Respond("ты помнишь моё имя?"));
            Equal(jacob.Respond("абракадабра"), jacob.Respond("абракадабра"));

            jacob.Submit("кто ты?");
            Equal(2, jacob.Transcript.Count);
            Contains(jacob.ToText(), "Яков.");

            var other = Start(new JacobScene(), 999);
            Equal(other.Respond("абракадабра"), other.Respond("абракадабра"));
        });

        Register("scene 508: фиксированный диалог и null", () =>
        {
            var scene = Start(new Jacob508Scene(), 3);
            for (int i = 0; i < 40; i++)
            {
                scene.Advance();
            }

            Equal(SceneStatus.Finished, scene.Status);
            Contains(scene.ToText(), "null");
        });

        Register("scene 95: рост памяти и авария", () =>
        {
            var scene = Start(new VisualNoiseScene(), 5);
            double previous = scene.MemoryMb;
            for (int i = 0; i < 1000 && scene.Status == SceneStatus.Running; i++)
            {
                scene.Update(1.0);
                if (scene.MemoryMb < previous)
                {
                    throw new AssertionException("память уменьшилась");
                }

                previous = scene.MemoryMb;
            }

            Equal(SceneStatus.Crashed, scene.Status);
            True(scene.MemoryMb >= scene.CapMb - 0.001);
            Contains(scene.ToText(), "СИМУЛЯЦИЯ");
        });

        Register("scene 70 «Тамагочи»: кнопки и настроения", () =>
        {
            var scene = Start(new TamagotchiScene(), 11);
            True(TamagotchiScene.Moods.Contains(scene.CurrentMood));
            scene.PressButton(1);
            scene.PressButton(2);
            scene.PressButton(3);
            IReadOnlyList<SceneEffect> effects = scene.DrainEffects();
            Equal(3, effects.Count);
            Equal(0, scene.DrainEffects().Count);
            Contains(scene.ToText(), "Тамагочи");
        });

        Register("scene 101 «Требуется Ввод»: приём строки", () =>
        {
            var scene = Start(new RequiresInputScene());
            False(scene.Received);
            scene.Submit("привет");
            True(scene.Received);
            Equal("привет", scene.Submitted);
            Contains(scene.ToText(), "ВВОД ПОЛУЧЕН");
        });

        Register("scene 115: тональности из набора", () =>
        {
            var scene = Start(new CrossesScene(), 4);
            for (int i = 0; i < 50; i++)
            {
                scene.Update(0.1);
                True(scene.CurrentFrequency is >= 262 and <= 784);
            }

            Contains(scene.ToText(), "Три креста");
        });

        Register("scene 116: отсчёт до -1 и отказ питания", () =>
        {
            var scene = Start(new CountdownScene(), 8);
            Equal(256, scene.Value);
            for (int i = 0; i < 400 && !scene.PowerFailure; i++)
            {
                scene.Update(2.0);
            }

            True(scene.PowerFailure, "отказ питания не наступил");
            Equal(-1, scene.Value);
            IReadOnlyList<SceneEffect> effects = scene.DrainEffects();
            Equal(1, effects.Count);
            Equal("power-flicker", effects[0].Kind);

            for (int i = 0; i < 10 && scene.Status == SceneStatus.Running; i++)
            {
                scene.Update(2.0);
            }

            Equal(SceneStatus.Finished, scene.Status);
        });

        Register("scene 118: поиск принтера и документ", () =>
        {
            var scene = Start(new PrinterScene(), 2);
            for (int i = 0; i < 200 && !scene.Printed; i++)
            {
                scene.Update(0.4);
            }

            True(scene.Printed);
            IReadOnlyList<SceneEffect> effects = scene.DrainEffects();
            True(effects.Any(e => e.Kind == "print"));
            Contains(effects[0].Payload, "SCP FOUNDATION");
        });

        Register("scene 121 «Сказитель»: генерация повествования", () =>
        {
            var scene = new TellerScene(Array.Empty<string>(), broadScan: false)
            {
                SearchSeconds = 0.1,
                ProcessingSeconds = 0.1,
            };
            Start(scene, 6);
            scene.Update(0.2);
            scene.Update(0.2);
            Equal(SceneStatus.Finished, scene.Status);
            NotEmpty(scene.Narrative);
            IReadOnlyList<SceneEffect> effects = scene.DrainEffects();
            True(effects.Any(e => e.Kind == "narrative"));
            Contains(scene.ToText(), "Сказитель");
        });

        Register("scene 734: верные ответы завершают, ошибочный на контроле ломает", () =>
        {
            var good = Start(new IsThisScene(), 9);
            string[] answers = { "да", "нет", "да", "нет", "да", "нет", "да", "да", "нет", "да" };
            foreach (string a in answers)
            {
                good.Submit(a);
            }

            Equal(SceneStatus.Running, good.Status);
            good.Submit("да"); // item 11 (control A) expects yes
            good.Submit("нет"); // item 12 (control B) expects no
            Equal(SceneStatus.Finished, good.Status);

            var bad = Start(new IsThisScene(), 9);
            foreach (string a in answers)
            {
                bad.Submit(a);
            }

            bad.Submit("нет"); // wrong on control A
            Equal(SceneStatus.Crashed, bad.Status);
            Contains(bad.ToText(), "НЕОЖИДАННЫЙ КОНЕЦ ФАЙЛА");
        });

        Register("scene: каталог и запуск без UI", () =>
        {
            Equal(11, SceneCatalog.AllIds.Length);
            foreach (string id in SceneCatalog.AllIds)
            {
                IScene scene = SceneCatalog.Create(id);
                Equal(id, scene.Id);
                scene.Start(Profile, 1);
                scene.Update(0.1);
                NotEmpty(scene.ToText());
            }

            Throws<ArgumentOutOfRangeException>(() => SceneCatalog.Create("SCP-000"));

            var runner = new HeadlessSceneRunner();
            var effects = new List<SceneEffect>();
            runner.EffectProduced += effects.Add;
            IScene die = runner.Open(SceneCatalog.DieId, Profile, 1);
            runner.UpdateAll(0.1);
            Equal(1, runner.Active.Count);
            die.OnInput(SceneInput.Text_("умереть"));
            Equal(SceneStatus.Finished, die.Status);
        });

        Register("canvas: текстовый холст рисует", () =>
        {
            var canvas = new TextCanvas(320, 200);
            canvas.Clear(Rgba.Black);
            canvas.FillRect(0, 0, 100, 50, Rgba.Red);
            canvas.DrawText(4, 0, "ABC", Rgba.White, 12);
            string text = canvas.Render();
            Contains(text, "ABC");
            Contains(text, "█");
        });
    }
}
