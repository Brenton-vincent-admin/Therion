# Therion

A kid-safe dinosaur survival game for Android, made in Unity 6. No ads, no purchases, no chat with strangers.

## Open the project
1. Install **Unity Hub**, then **Unity 6000.0 LTS** (6000.0.58f2 or newer 6000.0.x) with **Android Build Support** (incl. OpenJDK + Android SDK/NDK).
2. Clone this repo (Git LFS needed: `git lfs install` once), then in Unity Hub: Add > select the `therion` folder.
3. On first open Unity imports packages and may ask to enable the new Input System backend: click **Yes** (it restarts).
4. The `Dino/Project Setup` step runs automatically and sets the name, package id and Android settings.
5. Commit the generated `ProjectSettings/` and `.meta` files after this first open.

## Layout
See [CONTRACTS.md](CONTRACTS.md) for folders, namespaces and the shared interfaces every part of the game uses.

## Rules
- Code namespace is `Dino.*`; the game's name and package id live only in `Assets/_Game/Scripts/Core/GameInfo.cs`.
- Android package id `com.brenton.therion` is final.
- Work on a branch and open a pull request into `main`.

## Building for phones

GitHub Actions builds the Android APK and can send it to family phones through Firebase App Distribution. One-time setup: [docs/BUILD_AND_INSTALL.md](docs/BUILD_AND_INSTALL.md).
