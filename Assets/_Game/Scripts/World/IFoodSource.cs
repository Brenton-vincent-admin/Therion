using Dino.Species;
using UnityEngine;

namespace Dino.World
{
    /// <summary>
    /// Anything a dino can eat from: bushes, trees, bug mounds, carcasses, fish spots, nests of eggs.
    /// Implementations must call WorldRegistry.Register(this) in OnEnable and Unregister in OnDisable.
    /// The ready-made FoodSource and Carcass components already do this.
    /// </summary>
    public interface IFoodSource
    {
        FoodType FoodType { get; }
        /// <summary>Closest point a dino at 'from' should face/bite.</summary>
        Vector3 GetFeedPoint(Vector3 from);
        /// <summary>Height of the food above the ground (leaves high in a tree need a tall dino).</summary>
        float FeedHeight { get; }
        bool HasFood { get; }
        /// <summary>Food units left (for UI).</summary>
        float Remaining { get; }
        /// <summary>Takes up to 'units' of food. Returns how much was actually taken.</summary>
        float Bite(float units, GameObject eater);
    }

    /// <summary>
    /// Optional: food whose amount the multiplayer host can overwrite on other phones.
    /// FoodSource and Carcass implement it; custom food sources should too if they need syncing.
    /// </summary>
    public interface INetSyncedFood
    {
        /// <summary>Sets the food left to the host's value (no events, no eater).</summary>
        void NetSetRemaining(float remaining);
    }

    /// <summary>Anything a dino can drink from: lakes, rivers, puddles.</summary>
    public interface IWaterSource
    {
        Vector3 GetDrinkPoint(Vector3 from);
        bool CanDrink { get; }
        /// <summary>Takes up to 'units' of water. Returns how much was taken (lakes are usually infinite).</summary>
        float Drink(float units, GameObject drinker);
    }
}
