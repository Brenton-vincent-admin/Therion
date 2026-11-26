using System.IO;
using Dino.Core;
using Dino.Progression;
using Dino.Save;
using Dino.Species;
using Dino.Survival;
using NUnit.Framework;
using UnityEngine;

namespace Dino.Tests
{
    public class StatTests
    {
        [Test]
        public void ClampsAndReportsChange()
        {
            var s = new Stat(100);
            Assert.AreEqual(100, s.Current);
            Assert.AreEqual(-30, s.Add(-30), 0.001f);
            Assert.AreEqual(0, s.Add(-500) + 70, 0.001f);
            Assert.IsTrue(s.IsEmpty);
            s.Set(50);
            s.SetMax(200, keepRatio: true);
            Assert.AreEqual(0.25f, s.Normalized, 0.001f);
        }
    }

    public class SurvivalStatsTests
    {
        SpeciesDefinition _species;
        GameObject _go;
        SurvivalStats _stats;

        [SetUp]
        public void SetUp()
        {
            _species = ScriptableObject.CreateInstance<SpeciesDefinition>();
            _species.foodDrainPerMinute = 60f; // 1 per second at adult
            _species.waterDrainPerMinute = 60f;
            _go = new GameObject("dino");
            _stats = _go.AddComponent<SurvivalStats>();
            _stats.Configure(_species, _species.StageCount - 1, null, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_species);
        }

        [Test]
        public void DrainsFoodAndWaterOverTime()
        {
            _stats.Tick(10f);
            Assert.AreEqual(90f, _stats.Food.Current, 0.01f);
            Assert.AreEqual(90f, _stats.Water.Current, 0.01f);
        }

        [Test]
        public void ExplorerModeDoesNotDrainOrHurt()
        {
            _stats.NoDrain = true;
            _stats.Tick(100f);
            _stats.TakeDamage(new DamageInfo(1000f, null, Vector3.zero));
            Assert.AreEqual(100f, _stats.Food.Current, 0.01f);
            Assert.IsFalse(_stats.IsFainted);
        }

        [Test]
        public void FaintsAtZeroHealthOnce()
        {
            int faints = 0;
            _stats.OnFainted += _ => faints++;
            _stats.TakeDamage(new DamageInfo(1000f, null, Vector3.zero));
            _stats.TakeDamage(new DamageInfo(1000f, null, Vector3.zero));
            Assert.IsTrue(_stats.IsFainted);
            Assert.AreEqual(1, faints);
            _stats.Revive(0.5f);
            Assert.IsFalse(_stats.IsFainted);
            Assert.AreEqual(_stats.Health.Max * 0.5f, _stats.Health.Current, 0.01f);
        }

        [Test]
        public void RunningUntilEmptyNeedsABreatherBeforeRunningAgain()
        {
            _species.runStaminaPerSecond = 100f;
            _species.staminaRegenPerSecond = 10f;
            _stats.Configure(_species, _species.StageCount - 1, null, false);
            Assert.IsTrue(_stats.CanRun);
            _stats.IsRunning = true;
            _stats.Tick(0.5f);
            _stats.Tick(0.6f);
            Assert.IsTrue(_stats.Stamina.IsEmpty);
            Assert.IsTrue(_stats.IsExhausted);
            Assert.IsFalse(_stats.CanRun);

            _stats.IsRunning = false;
            _stats.Tick(1.6f);  // catch-breath pause before stamina starts coming back
            _stats.Tick(1.0f);  // +10
            Assert.IsFalse(_stats.CanRun, "10% is not enough to run again");
            _stats.Tick(2.0f);  // +20 -> 30%
            Assert.IsFalse(_stats.IsExhausted);
            Assert.IsTrue(_stats.CanRun);
        }

        [Test]
        public void StarvingHurts()
        {
            _stats.Food.Set(0);
            float before = _stats.Health.Current;
            _stats.Tick(2f);
            Assert.Less(_stats.Health.Current, before);
        }

        [Test]
        public void HatchlingHasSmallerMetersButSameEmptyTime()
        {
            var adultMinutes = _stats.Food.Max / (_species.foodDrainPerMinute);
            _stats.Configure(_species, 0, null, false);
            _stats.Tick(1f);
            float drainedPerSec = _stats.Food.Max - _stats.Food.Current;
            float hatchMinutes = _stats.Food.Max / (drainedPerSec * 60f);
            Assert.Less(_stats.Food.Max, 100f);
            Assert.AreEqual(adultMinutes, hatchMinutes, 0.05f);
        }
    }

    public class ProgressionTests
    {
        [Test]
        public void XpCurveMatchesDesign()
        {
            Assert.AreEqual(0, XpCurve.XpForLevel(1));
            Assert.AreEqual(300, XpCurve.XpForLevel(2));
            Assert.AreEqual(13300, XpCurve.XpForLevel(15));
            Assert.AreEqual(1, XpCurve.LevelForXp(299));
            Assert.AreEqual(2, XpCurve.LevelForXp(300));
        }

        [Test]
        public void UnlocksSpeciesAtThresholdAndNeverLosesXp()
        {
            var catalog = ScriptableObject.CreateInstance<SpeciesCatalog>();
            var starter = ScriptableObject.CreateInstance<SpeciesDefinition>();
            starter.id = "parasaur"; starter.isStarter = true;
            var trike = ScriptableObject.CreateInstance<SpeciesDefinition>();
            trike.id = "triceratops"; trike.unlockXp = 300;
            catalog.species.Add(starter); catalog.species.Add(trike);

            var profile = new ProfileData();
            var p = new ProgressionService(profile, catalog);
            Assert.IsTrue(p.IsUnlocked(starter));
            Assert.IsFalse(p.IsUnlocked(trike));
            Assert.AreEqual(0, p.AddXp(299, "test").Count);
            var newly = p.AddXp(1, "test");
            Assert.AreEqual(1, newly.Count);
            Assert.AreSame(trike, newly[0]);
            Assert.AreEqual(0, p.AddXp(50, "test").Count, "unlock is announced once");
            Assert.AreEqual(350, profile.lifetimeXp);
        }
    }

    public class SaveTests
    {
        string _dir;

        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "dino_save_test_" + System.Guid.NewGuid()); SaveSystem.Folder = _dir; }
        [TearDown] public void TearDown() { SaveSystem.Folder = null; if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [Test]
        public void RoundTripsAndKeepsBackup()
        {
            var d = SaveSystem.Load();
            Assert.AreEqual(0, d.profile.lifetimeXp);
            d.profile.lifetimeXp = 1234;
            d.run.active = true;
            d.run.speciesId = "trex";
            d.run.mode = GameMode.Hardcore;
            d.run.position = new Vector3(1, 2, 3);
            Assert.IsTrue(SaveSystem.Save(d));
            d.profile.lifetimeXp = 2000;
            Assert.IsTrue(SaveSystem.Save(d));

            var loaded = SaveSystem.Load();
            Assert.AreEqual(2000, loaded.profile.lifetimeXp);
            Assert.AreEqual("trex", loaded.run.speciesId);
            Assert.AreEqual(GameMode.Hardcore, loaded.run.mode);
            Assert.AreEqual(new Vector3(1, 2, 3), loaded.run.position);

            File.WriteAllText(SaveSystem.PathMain, "{ corrupted");
            Assert.AreEqual(1234, SaveSystem.Load().profile.lifetimeXp, "falls back to backup");
        }
    }
}
