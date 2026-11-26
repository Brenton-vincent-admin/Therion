using System.Collections.Generic;
using Dino.World.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Dino.World.Tests
{
    /// <summary>
    /// Checks the island against the design doc's rules: six biomes, a nest in each, water and nests
    /// close everywhere, food for every diet, and a deterministic result.
    /// </summary>
    public class IslandGeneratorTests
    {
        static IslandData _full;
        static IslandSpec _spec;

        [OneTimeSetUp]
        public void BuildOnce()
        {
            _spec = IslandSpec.CreateDefault();
            _full = IslandGenerator.Generate(_spec);
            IslandPlacer.Place(_spec, _full);
        }

        [Test]
        public void Noise_MatchesPythonPreview()
        {
            // Values printed by Tools/IslandPreview/island.py, so previews show the real island.
            Assert.AreEqual(0.7151926755905151f, IslandNoise.Hash01(1, 2, 3), 1e-6f);
            Assert.AreEqual(0.8209189772605896f, IslandNoise.Hash01(-5, 7, 1987), 1e-6f);
            Assert.AreEqual(0.3700547042121202f, IslandNoise.Fbm(0.37f, 1.91f, 1988, 5), 1e-4f);
        }

        [Test]
        public void Generation_IsDeterministic()
        {
            var spec = IslandSpec.CreateDefault();
            spec.heightmapResolution = 257;
            var a = IslandGenerator.Generate(spec);
            var b = IslandGenerator.Generate(spec);
            CollectionAssert.AreEqual(a.Heights, b.Heights);
            CollectionAssert.AreEqual(a.Biomes, b.Biomes);
        }

        [Test]
        public void Island_IsAboutOneAndAHalfKilometresAcross()
        {
            int res = _full.Resolution, minX = res, maxX = -1;
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                if (_full.Heights[_full.Index(x, z)] > 0f) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
            float width = (maxX - minX) * _full.Cell;
            Assert.That(width, Is.InRange(1300f, 1700f), $"island width {width:0} m");
        }

        [Test]
        public void AllSixBiomes_ArePresent()
        {
            var seen = new HashSet<byte>(_full.Biomes);
            foreach (var code in new[] { BiomeIds.MeadowCode, BiomeIds.RiverbanksCode, BiomeIds.ForestCode, BiomeIds.SwampCode, BiomeIds.HighlandsCode, BiomeIds.LakeCode })
                Assert.IsTrue(seen.Contains(code), $"missing biome {BiomeIds.FromCode(code)}");
        }

        [Test]
        public void Volcano_IsTheHighestLandmark()
        {
            float max = float.MinValue; int at = 0;
            for (int i = 0; i < _full.Heights.Length; i++) if (_full.Heights[i] > max) { max = _full.Heights[i]; at = i; }
            float x = at % _full.Resolution * _full.Cell / _full.Size, z = at / _full.Resolution * _full.Cell / _full.Size;
            Assert.Greater(max, 120f);
            Assert.Less(Vector2.Distance(new Vector2(x, z), _spec.volcanoCenter), _spec.volcanoRadius);
            Assert.Less(max, _spec.terrainBottom + _spec.terrainHeight, "volcano is clipped by the terrain height");
        }

        [Test]
        public void MainNest_IsInTheMeadow()
        {
            var main = _full.Props.Find(p => p.kind == WorldPropKind.Nest && p.isMain);
            Assert.AreEqual(BiomeIds.MeadowCode, main.biome);
            Assert.Greater(main.position.y, 1f);
        }

        [Test]
        public void EveryBiome_HasANest()
        {
            var biomes = new HashSet<byte>();
            foreach (var p in _full.Props) if (p.kind == WorldPropKind.Nest) biomes.Add(p.biome);
            foreach (var code in new[] { BiomeIds.MeadowCode, BiomeIds.RiverbanksCode, BiomeIds.ForestCode, BiomeIds.SwampCode, BiomeIds.HighlandsCode, BiomeIds.LakeCode })
                Assert.IsTrue(biomes.Contains(code), $"no nest in {BiomeIds.FromCode(code)}");
        }

        [Test]
        public void WaterAndNests_AreAlwaysCloseBy()
        {
            float slack = _full.Cell * _spec.placementStep * 1.5f;
            Assert.LessOrEqual(_full.WorstWaterDistance, _spec.waterSpacing + slack, "farthest walk to drinkable water");
            Assert.LessOrEqual(_full.WorstNestDistance, _spec.nestSpacing + slack, "farthest walk to a nest");
        }

        [Test]
        public void EveryLandBiome_HasPlantFoodAndBugs()
        {
            var plants = new HashSet<byte>();
            var bugs = new HashSet<byte>();
            foreach (var p in _full.Props)
            {
                if (p.kind == WorldPropKind.BerryBush || p.kind == WorldPropKind.LeafShrub || p.kind == WorldPropKind.Fern) plants.Add(p.biome);
                if (p.kind == WorldPropKind.BugMound) bugs.Add(p.biome);
            }
            foreach (var code in new[] { BiomeIds.MeadowCode, BiomeIds.RiverbanksCode, BiomeIds.ForestCode, BiomeIds.SwampCode, BiomeIds.HighlandsCode, BiomeIds.LakeCode })
            {
                Assert.IsTrue(plants.Contains(code), $"no plant food in {BiomeIds.FromCode(code)}");
                Assert.IsTrue(bugs.Contains(code), $"no bugs in {BiomeIds.FromCode(code)}");
            }
        }

        [Test]
        public void FishSpots_SitOnDrinkableWater()
        {
            int fish = 0;
            foreach (var p in _full.Props)
            {
                if (p.kind != WorldPropKind.FishSpot) continue;
                fish++;
                int x = Mathf.RoundToInt(p.position.x / _full.Cell), z = Mathf.RoundToInt(p.position.z / _full.Cell);
                Assert.IsTrue(_full.IsDrinkable(_full.Index(x, z)), $"fish spot on dry land at {p.position}");
            }
            Assert.Greater(fish, 10);
        }

        [Test]
        public void Food_IsNeverUnderwater()
        {
            foreach (var p in _full.Props)
            {
                if (p.kind == WorldPropKind.FishSpot || p.kind == WorldPropKind.Puddle) continue;
                int x = Mathf.RoundToInt(p.position.x / _full.Cell), z = Mathf.RoundToInt(p.position.z / _full.Cell);
                Assert.IsFalse(_full.IsWater(_full.Index(x, z)) && p.kind != WorldPropKind.Nest && _full.WaterLevel[_full.Index(x, z)] - _full.Heights[_full.Index(x, z)] > 0.5f,
                    $"{p.kind} under water at {p.position}");
            }
        }

        [Test]
        public void Rivers_NeverFlowUphill()
        {
            foreach (var r in _full.Rivers)
                for (int k = 1; k < r.level.Length; k++)
                    Assert.LessOrEqual(r.level[k], r.level[k - 1] + 1e-3f, $"{r.name} flows uphill at point {k}");
        }
    }
}
