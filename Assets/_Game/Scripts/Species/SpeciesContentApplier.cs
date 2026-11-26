using System.Collections.Generic;
using UnityEngine;

namespace Dino.Species
{
    /// <summary>
    /// Writes a <see cref="RosterEntry"/> (stars, from the docs) into a SpeciesDefinition (real units).
    /// Used by the editor menu "Dino/Species/Rebuild Species Assets". Leaves art references
    /// (modelPrefab, icon, adultModelScale) alone so re-running never wipes hooked-up models.
    /// </summary>
    public static class SpeciesContentApplier
    {
        public static void Apply(RosterEntry e, SpeciesDefinition d)
        {
            var n = SpeciesTuning.Compute(e);

            d.id = e.id;
            d.displayName = e.displayName;
            d.kidDescription = e.kidDescription;
            d.diet = e.diet;
            d.moveType = e.moveType;
            d.size = e.size;
            d.adultHeight = n.adultHeight;
            d.adultRadius = n.adultRadius;
            SkinPalette.ToRgb01(e.defaultSkin.Base, out var r, out var g, out var b);
            d.placeholderColor = new Color(r, g, b);

            d.isStarter = n.isStarter;
            d.unlockXp = n.unlockXp;
            d.sortOrder = e.sortOrder;

            d.maxHealth = n.maxHealth;
            d.maxStamina = n.maxStamina;
            d.maxFood = n.maxFood;
            d.maxWater = n.maxWater;
            d.foodDrainPerMinute = n.foodDrainPerMinute;
            d.waterDrainPerMinute = n.waterDrainPerMinute;
            d.staminaRegenPerSecond = n.staminaRegenPerSecond;
            d.runStaminaPerSecond = n.runStaminaPerSecond;
            d.healthRegenPerSecond = n.healthRegenPerSecond;

            d.walkSpeed = n.walkSpeed;
            d.runSpeed = n.runSpeed;
            d.swimSpeed = n.swimSpeed;
            d.sneakSpeed = n.sneakSpeed;
            d.turnSpeed = n.turnSpeed;
            d.jumpHeight = n.jumpHeight;

            d.foods = new List<FoodPreference>();
            foreach (var f in n.foods) d.foods.Add(new FoodPreference(f.type, f.nutrition));
            d.biteSize = n.biteSize;
            d.biteInterval = n.biteInterval;
            d.reach = n.reach;
            d.feedHeight = n.feedHeight;
            d.attackDamage = n.attackDamage;
            d.attackCooldown = n.attackCooldown;

            d.noticeRadius = n.noticeRadius;
            d.sneakNoticeMultiplier = n.sneakNoticeMultiplier;

            d.growthStages = new List<GrowthStage>();
            foreach (var s in n.growthStages)
                d.growthStages.Add(new GrowthStage(s.name, s.scale, s.statMultiplier, s.minutesToGrow, s.xpOnReach));

            d.abilityIds = new List<string>(n.abilityIds);
            d.variants = new List<StatVariant>();
            foreach (var v in n.variants)
                d.variants.Add(new StatVariant
                {
                    id = v.id, displayName = v.displayName, unlockXp = v.unlockXp,
                    healthMultiplier = v.health, speedMultiplier = v.speed, swimMultiplier = v.swim,
                    staminaMultiplier = v.stamina, damageMultiplier = v.damage,
                    noticeRadiusMultiplier = v.noticeRadius, drainMultiplier = v.drain,
                });

            // content fields
            d.scientificName = e.scientificName;
            d.temperament = e.temperament;
            d.stealthy = e.stealthy;
            d.signatureAbility = e.ability;
            d.abilityLevelText = (string[])e.abilityLevelText.Clone();
            d.unlockXpLevel = e.unlockXpLevel;
            d.lengthMeters = e.lengthMeters;
            d.defaultSkin = e.defaultSkin;
            d.modelSource = e.modelSource;
        }
    }
}
