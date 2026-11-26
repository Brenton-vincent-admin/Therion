using System.Collections.Generic;
using System.Linq;
using Dino.Species.AI;
using NUnit.Framework;

namespace Dino.Species.Tests
{
    // Pure-logic tests: no scene, no assets. They run in the Unity Test Runner (EditMode)
    // and also outside Unity with plain NUnit.
    public class RosterTests
    {
        [Test] public void FourteenSpecies_SixThreeFive()
        {
            Assert.AreEqual(14, LaunchRoster.All.Count);
            Assert.AreEqual(6, LaunchRoster.All.Count(s => s.diet == Diet.Herbivore));
            Assert.AreEqual(3, LaunchRoster.All.Count(s => s.diet == Diet.Omnivore));
            Assert.AreEqual(5, LaunchRoster.All.Count(s => s.diet == Diet.Carnivore));
        }

        [Test] public void IdsUniqueAndLowerSnake()
        {
            var ids = LaunchRoster.All.Select(s => s.id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
            foreach (var id in ids) StringAssert.IsMatch("^[a-z][a-z0-9_]*$", id);
        }

        [Test] public void BrentonsMustHavesPresent()
        {
            foreach (var id in new[] { "trex", "brachiosaurus", "pachycephalosaurus", "velociraptor", "microraptor", "carnotaurus" })
                Assert.IsNotNull(LaunchRoster.Get(id), id);
        }

        [Test] public void FourStarters_OnePerPlaystyle()
        {
            CollectionAssert.AreEquivalent(new[] { "parasaur", "pachycephalosaurus", "oviraptor", "velociraptor" },
                LaunchRoster.Starters.Select(s => s.id).ToArray());
        }

        [Test] public void UnlockLevelsMatchDesignDoc()
        {
            var expected = new Dictionary<string, int>
            {
                { "psittacosaurus", 2 }, { "gallimimus", 3 }, { "microraptor", 4 }, { "troodon", 5 }, { "stegosaurus", 6 },
                { "dilophosaurus", 8 }, { "triceratops", 9 }, { "carnotaurus", 10 }, { "brachiosaurus", 12 }, { "trex", 15 },
            };
            foreach (var kv in expected) Assert.AreEqual(kv.Value, LaunchRoster.Get(kv.Key).unlockXpLevel, kv.Key);
        }

        [Test] public void FiveStealthSpecies()
        {
            CollectionAssert.AreEquivalent(new[] { "psittacosaurus", "troodon", "microraptor", "velociraptor", "dilophosaurus" },
                LaunchRoster.All.Where(s => s.stealthy).Select(s => s.id).ToArray());
        }

        [Test] public void NoFullyAquaticPlayables_MicroraptorGlides()
        {
            Assert.IsTrue(LaunchRoster.All.All(s => s.moveType != MoveType.Flyer));
            Assert.AreEqual(MoveType.Glider, LaunchRoster.Get("microraptor").moveType);
        }

        [Test] public void EverySpeciesHasAbilityWithThreeLevels()
        {
            foreach (var s in LaunchRoster.All)
            {
                Assert.AreNotEqual(SignatureAbility.None, s.ability, s.id);
                Assert.AreEqual(3, s.abilityLevelText.Length, s.id);
                Assert.IsTrue(s.abilityLevelText.All(t => !string.IsNullOrEmpty(t)), s.id);
            }
            Assert.AreEqual(14, LaunchRoster.All.Select(s => s.ability).Distinct().Count());
        }

        [Test] public void StarsInRange()
        {
            foreach (var s in LaunchRoster.All)
                foreach (var v in new[] { s.stars.health, s.stars.bite, s.stars.speed, s.stars.stamina, s.stars.foodDrain, s.stars.waterDrain })
                    Assert.That(v, Is.InRange(1, 5), s.id);
        }

        [Test] public void AbilityIdsRoundTrip()
        {
            foreach (var s in LaunchRoster.All)
                Assert.AreEqual(s.ability, AbilityIds.Parse(AbilityIds.For(s.ability)));
        }
    }

    public class TuningTests
    {
        [Test] public void FoodLastsSixToEightMinutes()
        {
            foreach (var s in LaunchRoster.All)
            {
                var n = SpeciesTuning.Compute(s);
                float minutes = n.maxFood / n.foodDrainPerMinute;
                Assert.That(minutes, Is.InRange(6f, 8f), s.id);
                Assert.That(n.maxWater / n.waterDrainPerMinute, Is.InRange(6f, 8f), s.id);
            }
        }

        [Test] public void GallimimusAndParasaurFastest_BrachioSlowest()
        {
            var speeds = LaunchRoster.All.ToDictionary(s => s.id, s => SpeciesTuning.Compute(s).runSpeed);
            float max = speeds.Values.Max(), min = speeds.Values.Min();
            Assert.AreEqual(max, speeds["gallimimus"]);
            Assert.AreEqual(max, speeds["parasaur"]);
            Assert.AreEqual(min, speeds["brachiosaurus"]);
        }

        [Test] public void TrexHitsHardest_BrachioAndTrikeTankiest()
        {
            var n = LaunchRoster.All.ToDictionary(s => s.id, s => SpeciesTuning.Compute(s));
            Assert.AreEqual(n.Values.Max(x => x.attackDamage), n["trex"].attackDamage);
            Assert.AreEqual(n.Values.Max(x => x.maxHealth), n["brachiosaurus"].maxHealth);
            Assert.AreEqual(n["triceratops"].maxHealth, n["brachiosaurus"].maxHealth);
        }

        [Test] public void OnlyBrachioReachesTreetops()
        {
            var n = LaunchRoster.All.ToDictionary(s => s.id, s => SpeciesTuning.Compute(s));
            float brachio = n["brachiosaurus"].feedHeight;
            foreach (var kv in n) if (kv.Key != "brachiosaurus") Assert.Less(kv.Value.feedHeight, brachio * 0.5f, kv.Key);
        }

        [Test] public void DietFoods()
        {
            foreach (var s in LaunchRoster.All)
            {
                var types = SpeciesTuning.FoodsFor(s).Select(f => f.type).ToList();
                if (s.diet == Diet.Herbivore) { Assert.Contains(FoodType.Berries, types); Assert.IsFalse(types.Contains(FoodType.Meat), s.id); }
                if (s.diet == Diet.Carnivore) { Assert.Contains(FoodType.Meat, types); Assert.IsFalse(types.Contains(FoodType.Berries), s.id); }
                if (s.diet == Diet.Omnivore) { Assert.Contains(FoodType.Berries, types); Assert.Contains(FoodType.Meat, types); }
            }
            // Microraptor eats bugs and small prey; nobody has to fight to grow.
            Assert.Contains(FoodType.Bugs, SpeciesTuning.FoodsFor(LaunchRoster.Get("microraptor")).Select(f => f.type).ToList());
        }

        [Test] public void StealthNoticeRanges_25_10_4()
        {
            var raptor = SpeciesTuning.Compute(LaunchRoster.Get("velociraptor"));
            var para = SpeciesTuning.Compute(LaunchRoster.Get("parasaur"));
            Assert.AreEqual(25f, para.noticeRadius);
            Assert.AreEqual(10f, raptor.noticeRadius);
            Assert.AreEqual(4f, raptor.noticeRadius * raptor.sneakNoticeMultiplier, 0.001f);
        }

        [Test] public void GrowthTimesMatchDesignDoc()
        {
            var g = Growth.StagesFor(1f);
            Assert.AreEqual(4, g.Count);
            Assert.AreEqual(18f, g[0].minutesToGrow);
            Assert.AreEqual(50f, g[0].minutesToGrow + g[1].minutesToGrow);
            Assert.AreEqual(150f, Growth.TotalMinutesToAdult(g));
            Assert.AreEqual(225f, Growth.TotalMinutesToAdult(Growth.StagesFor(1.5f)), 0.5f); // T. rex
            Assert.AreEqual(1, Growth.AbilityLevel(0));
            Assert.AreEqual(2, Growth.AbilityLevel(2));
            Assert.AreEqual(3, Growth.AbilityLevel(3));
        }

        [Test] public void XpCurve()
        {
            Assert.AreEqual(0, Dino.Progression.XpCurve.XpForLevel(1));
            Assert.AreEqual(300, Dino.Progression.XpCurve.XpForLevel(2));
            Assert.AreEqual(13300, Dino.Progression.XpCurve.XpForLevel(15));
            for (int l = 1; l < 20; l++)
            {
                Assert.AreEqual(l, Dino.Progression.XpCurve.LevelForXp(Dino.Progression.XpCurve.XpForLevel(l)));
                Assert.AreEqual(l, Dino.Progression.XpCurve.LevelForXp(Dino.Progression.XpCurve.XpForLevel(l + 1) - 1));
            }
            Assert.AreEqual(0, SpeciesTuning.Compute(LaunchRoster.Get("parasaur")).unlockXp);
            Assert.AreEqual(13300, SpeciesTuning.Compute(LaunchRoster.Get("trex")).unlockXp);
        }

        [Test] public void VariantsEachHaveATradeOff()
        {
            var all = Variants.All();
            CollectionAssert.AreEquivalent(new[] { "swift", "aquatic", "fierce", "sturdy", "sneaky" }, all.Select(v => v.id).ToArray());
            foreach (var v in all)
            {
                var m = new[] { v.health, v.speed, v.swim, v.stamina, v.damage };
                bool boost = m.Any(x => x > 1f) || v.noticeRadius < 1f;
                bool cost = m.Any(x => x < 1f) || v.drain > 1f;
                Assert.IsTrue(boost && cost, v.id);
                Assert.That(m.Max(), Is.LessThanOrEqualTo(1.25f), v.id);
            }
        }

        [Test] public void ToStarsInvertsTables()
        {
            for (int s = 1; s <= 5; s++)
                Assert.AreEqual(s, SpeciesTuning.ToStars(SpeciesTuning.RunSpeed, SpeciesTuning.Stars(SpeciesTuning.RunSpeed, s)));
        }
    }

    public class StealthTests
    {
        static StealthContext Ctx(Stance s, bool cover = false, float scale = 1f) => new StealthContext { stance = s, inCover = cover, scale = scale };

        [Test] public void StealthyIsNoticedFromCloser()
        {
            float normal = StealthRules.NoticeRange(25f, 0.7f, false, Ctx(Stance.Walking));
            float sneaky = StealthRules.NoticeRange(10f, 0.4f, true, Ctx(Stance.Walking));
            Assert.AreEqual(25f, normal, 0.01f);
            Assert.AreEqual(10f, sneaky, 0.01f);
            Assert.AreEqual(4f, StealthRules.NoticeRange(10f, 0.4f, true, Ctx(Stance.Sneaking)), 0.01f);
        }

        [Test] public void StillInCover_FaceToFaceOnly()
        {
            Assert.LessOrEqual(StealthRules.NoticeRange(10f, 0.4f, true, Ctx(Stance.Still, cover: true)), StealthRules.FaceToFaceRange);
        }

        [Test] public void TinyHatchlingsCanHideInGrass()
        {
            Assert.LessOrEqual(StealthRules.NoticeRange(25f, 0.7f, false, Ctx(Stance.Still, true, 0.2f)), StealthRules.FaceToFaceRange);
            Assert.Greater(StealthRules.NoticeRange(25f, 0.7f, false, Ctx(Stance.Still, true, 1f)), StealthRules.FaceToFaceRange);
        }

        [Test] public void RunningBreaksStealth()
        {
            Assert.Greater(StealthRules.NoticeRange(10f, 0.4f, true, Ctx(Stance.Running)), 25f);
            var broken = Ctx(Stance.Sneaking); broken.stealthBroken = true;
            Assert.Greater(StealthRules.NoticeRange(10f, 0.4f, true, broken), 25f);
        }

        [Test] public void SharperEyesAndNight()
        {
            Assert.AreEqual(Awareness.Seen, StealthRules.Check(12f, 10f, StealthRules.EyesMultiplier(true, false, false)));
            Assert.AreEqual(Awareness.Curious, StealthRules.Check(12f, 10f, StealthRules.EyesMultiplier(false, false, false)));
            Assert.AreEqual(Awareness.Hidden, StealthRules.Check(12f, 10f, StealthRules.EyesMultiplier(false, true, false)));
            Assert.Greater(StealthRules.EyesMultiplier(false, true, true), StealthRules.EyesMultiplier(false, true, false));
        }
    }

    public class SkinTests
    {
        [Test] public void PackRoundTrip()
        {
            foreach (var s in LaunchRoster.All)
                Assert.AreEqual(s.defaultSkin, SkinLoadout.Unpack(s.defaultSkin.Pack()));
        }

        [Test] public void PaletteCoversEnum()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(SkinColor)).Length, SkinPalette.Count);
        }

        [Test] public void SanitizeFixesBadSave()
        {
            var bad = new SkinLoadout(250, 1, 200, 2, 3, 4).Sanitized();
            Assert.AreEqual(0, bad.baseColor);
            Assert.AreEqual(0, bad.pattern);
            Assert.AreEqual(1, bad.bellyColor);
        }

        [Test] public void RandomUsesOnlyOwnedColors_AndUndoWorks()
        {
            var rng = new System.Random(1);
            var owned = new[] { SkinColor.Leaf, SkinColor.Sky, SkinColor.Cream };
            var hist = new SkinEditHistory();
            var first = SkinLoadout.Of(SkinColor.Leaf, SkinColor.Cream, SkinPattern.None, SkinColor.Sky, SkinColor.Sky);
            for (int i = 0; i < 50; i++)
            {
                hist.Push(first);
                var s = SkinPalette.Random(rng, owned, SkinPalette.StarterPatterns);
                Assert.Contains(s.Base, owned);
                Assert.Contains(s.PatternColor, owned);
                Assert.AreNotEqual(s.Base, s.PatternColor);
            }
            Assert.AreEqual(SkinEditHistory.Max, hist.Count);
            Assert.IsTrue(hist.TryUndo(out var back));
            Assert.AreEqual(first, back);
        }

        [Test] public void UnlocksAreEarnedNotSold()
        {
            var colors = new List<SkinColor>(); var patterns = new List<SkinPattern>();
            SkinUnlocks.Owned(new List<string>(), colors, patterns);
            Assert.IsFalse(colors.Contains(SkinColor.Gold));
            SkinUnlocks.Owned(new List<string> { "hardcore_adult" }, colors, patterns);
            Assert.IsTrue(colors.Contains(SkinColor.Gold));
            Assert.IsNotNull(SkinUnlocks.HintFor(SkinColor.Gold));
        }
    }

    public class WildDecisionTests
    {
        static WildSnapshot Me(Diet diet, SizeClass size, Temperament t, params SeenDino[] seen) => new WildSnapshot
        {
            diet = diet, size = size, temperament = t, food = 0.9f, water = 0.9f, health = 1f, stamina = 1f,
            lastAttackerIndex = -1, seen = seen, seenCount = seen.Length, foodKnown = true, waterKnown = true,
        };

        static SeenDino D(int i, float dist, SizeClass size, bool predator = false, bool player = true)
            => new SeenDino { index = i, distance = dist, size = size, isPredator = predator, isPlayer = player };

        [Test] public void SkittishFleesFromPredator()
        {
            var d = WildDinoDecisions.Decide(Me(Diet.Herbivore, SizeClass.Large, Temperament.Skittish, D(0, 10f, SizeClass.Huge, true)));
            Assert.AreEqual(WildState.Flee, d.state);
            Assert.IsTrue(d.run);
        }

        [Test] public void HungryPredatorHuntsSmallerPrey_ButGivesUp()
        {
            var me = Me(Diet.Carnivore, SizeClass.Huge, Temperament.Predator, D(0, 20f, SizeClass.Small));
            me.food = 0.3f;
            Assert.AreEqual(WildState.Chase, WildDinoDecisions.Decide(me).state);
            me.chaseSeconds = WildDinoDecisions.SurvivalChaseGiveUp + 0.1f;
            Assert.AreNotEqual(WildState.Chase, WildDinoDecisions.Decide(me).state);
            me.hardcore = true;
            Assert.AreEqual(WildState.Chase, WildDinoDecisions.Decide(me).state); // hardcore chases longer
        }

        [Test] public void FullPredatorLeavesYouAlone()
        {
            var me = Me(Diet.Carnivore, SizeClass.Huge, Temperament.Predator, D(0, 20f, SizeClass.Small));
            Assert.AreNotEqual(WildState.Chase, WildDinoDecisions.Decide(me).state);
        }

        [Test] public void FriendlyPredatorsNeverChasePlayers()
        {
            var me = Me(Diet.Carnivore, SizeClass.Huge, Temperament.Predator, D(0, 5f, SizeClass.Small));
            me.food = 0.1f; me.friendlyPredators = true;
            var s = WildDinoDecisions.Decide(me).state;
            Assert.AreNotEqual(WildState.Chase, s);
            Assert.AreNotEqual(WildState.Attack, s);
        }

        [Test] public void NestsAreSafe()
        {
            var prey = D(0, 5f, SizeClass.Small); prey.inSafeZone = true;
            var me = Me(Diet.Carnivore, SizeClass.Huge, Temperament.Predator, prey);
            me.food = 0.1f;
            Assert.AreNotEqual(WildState.Chase, WildDinoDecisions.Decide(me).state);
        }

        [Test] public void TerritorialWarnsThenAttacks()
        {
            var me = Me(Diet.Herbivore, SizeClass.Large, Temperament.Territorial, D(0, 6f, SizeClass.Medium));
            Assert.AreEqual(WildState.Warn, WildDinoDecisions.Decide(me).state);
            me.seen[0].distance = 1.5f;
            Assert.AreEqual(WildState.Attack, WildDinoDecisions.Decide(me).state);
        }

        [Test] public void ThirstyGoesToWater_HungryHerbivoreGrazes()
        {
            var me = Me(Diet.Herbivore, SizeClass.Medium, Temperament.Defensive);
            me.water = 0.2f; me.food = 0.2f;
            Assert.AreEqual(WildState.Drink, WildDinoDecisions.Decide(me).state);
            me.water = 0.9f;
            Assert.AreEqual(WildState.Graze, WildDinoDecisions.Decide(me).state);
        }

        [Test] public void DefensiveFightsBackUnlessOutmatched()
        {
            var me = Me(Diet.Herbivore, SizeClass.Large, Temperament.Defensive, D(0, 2f, SizeClass.Medium, true));
            me.lastAttackerIndex = 0; me.lastAttackerSize = SizeClass.Medium; me.lastAttackerDistance = 2f;
            Assert.AreEqual(WildState.Attack, WildDinoDecisions.Decide(me).state);
            me.lastAttackerSize = SizeClass.Huge;
            Assert.AreEqual(WildState.Flee, WildDinoDecisions.Decide(me).state);
        }

        [Test] public void HungryCarnivoreScavengesFirst()
        {
            var me = Me(Diet.Carnivore, SizeClass.Medium, Temperament.Predator, D(0, 20f, SizeClass.Small));
            me.food = 0.2f; me.carcassKnown = true;
            Assert.AreEqual(WildState.Scavenge, WildDinoDecisions.Decide(me).state);
        }
    }
}
