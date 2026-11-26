# Playing together

One phone hosts, up to four friends join. Nothing runs (or costs anything) when nobody is playing.

## How to play

1. Everyone needs the **same version** of the game. If versions differ, the joiner sees
   "Ask a grown-up to update the game".
2. Tap **Friends** (top right).
3. **Same house:** the host taps **Host on home Wi-Fi**. On the other phones the host's dino name
   appears under "Games on this Wi-Fi": tap it. If it doesn't show up, type the 7-letter Wi-Fi code
   from the host's screen. No internet or account is used.
4. **Different houses:** a grown-up holds **Host online** for 3 seconds. The host's screen shows a
   6-character friend code; the others type it and tap **Join**. This needs the one-time setup below.

When you join you appear next to the host. Everyone keeps their own dino, growth and XP on their own
phone. If the host leaves, the others simply carry on playing on their own.

The host can switch **Friends can bite each other** on or off. It is off by default.

## One-time setup for online play (free)

Friend codes use Unity Relay, which is free at family scale. Wi-Fi play works without this.

1. In Unity: **Edit > Project Settings > Services**. Sign in with your Unity account and choose
   **Create a Unity Project ID** (organisation: yours, name: Therion).
2. If it asks whether the game is aimed at children under 13, answer **Yes**.
3. Open [cloud.unity.com](https://cloud.unity.com), pick the Therion project, go to
   **Multiplayer > Relay** and click **Get started / Enable**.
4. Save the project in Unity and commit the changed `ProjectSettings/ProjectSettings.asset`
   (it now holds the project ID).

Sign-in is anonymous: the game sends no name, email or account, only a random ID Unity makes up.

## What is shared

- **Dinos:** position, species, growth stage, variant, health/food/water, and the bite, roar and
  jump animations. Each phone moves its own dino; the host passes positions to everyone else.
- **Food:** the host's amounts win. When a friend eats from a plant, everyone sees it shrink.
- **Carcasses:** created on the host and copied to the others.
- **Names:** two words from a fixed list ("Swift Raptor"). Nobody can type a name or a message.

Not shared yet: wild (AI) dinos, day/night and weather, and one game mode for the whole group.
Each player currently uses their own Survival/Hardcore/Explorer setting.

## For developers

Code lives in `Assets/_Game/Scripts/Net` (assembly `Dino.Net`). Netcode for GameObjects provides
the connection and transport; replication is a small set of custom named messages (`NetProtocol`)
because dinos are built in code rather than from NetworkObject prefabs. `NetSession` owns the
connection, `NetSession.Players` the dinos, `NetSession.World` food and carcasses, `LanBeacon`
same-Wi-Fi discovery, `RelayLink` friend codes, `NetMenu` the UI. Bump `NetProtocol.Version`
whenever a message changes shape.
