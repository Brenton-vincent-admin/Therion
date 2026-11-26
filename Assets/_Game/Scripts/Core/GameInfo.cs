namespace Dino.Core
{
    /// <summary>
    /// The one place the game's public name lives. Change these and run
    /// "Dino/Project Setup" in the editor to update Player Settings.
    /// The code namespace (Dino.*) never needs to change.
    /// </summary>
    public static class GameInfo
    {
        public const string ProductName = "Therion";
        public const string CompanyName = "Brenton";
        public const string AndroidPackageId = "com.brenton.therion"; // registered with Firebase/Google: never change
        public const int SaveVersion = 1;
    }
}
