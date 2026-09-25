# Wildtide (Unity)

A 3D isometric platformer for iOS and Android: hop across five islands rising out of the sea, gather pearls,
bop crabs, ride drifting rafts and reach the golden shell at the end of each island. It saves your best
pearls and times, and plays in landscape with touch controls.

Everything is built from coloured shapes in code, so there are no art files to import.

## What's in it

- **Five islands**, each teaching something new: Shell Beach (running and jumping), Crab Cove (bopping crabs,
  bounce pads), Drift Bridge (moving rafts), Urchin Cliffs (crumbling stones, spikes) and Lighthouse Rise (all of it).
- **Platforming that forgives you:** a short grace window after running off a ledge, jump presses buffered just
  before landing, and a tap for a hop or a hold for a full jump. A shadow under the player shows where you'll land.
- **Checkpoints and a timer:** falling in the sea sends you back to the last flag. Clearing an island opens the next
  one, and the island list shows your best pearls and time.
- **Phone support:** a floating thumbstick on the left, a big jump button on the right, safe-area layout for the
  iPhone notch, 60 fps. Android's back button (Esc in the editor) returns to the island list.

## Open it

1. Install **Unity Hub**, then **Unity 6 (6000.0 LTS)** with these modules:
   **iOS Build Support** and **Android Build Support** (including OpenJDK and Android SDK & NDK).
2. In Unity Hub: **Add → Add project from disk** and pick this `unity/Wildtide` folder. If Hub asks about the
   editor version, pick any 6000.0.x you have installed.
3. On first open the project sets itself up: landscape orientation, bundle id `com.witclad.wildtide`,
   IL2CPP/ARM64 for Android, iOS 15+, URP and the toon shader, and `Assets/Scenes/Main.unity`.
   You can re-run it from **Wildtide → Set Up Project for iOS + Android**.
4. Press **Play**. In the editor, move with WASD or the arrow keys and jump with Space.
   Use **Wildtide → Delete Save** to start over.

The scene is empty on purpose: `GameRoot` builds the islands, UI and camera from code, so there's nothing to wire up.

## Run it on your iPhone

**No Mac?** Follow [PHONE.md](PHONE.md). GitHub builds the app in the cloud and sends it to TestFlight on your iPhone
(needs the Apple Developer Program). It also builds an Android APK.

**With a Mac, one command** (free Apple ID works): plug in the iPhone and paste this into Terminal. It downloads the
latest game, builds it and installs it. Run it again after changes, or every 7 days on a free Apple ID.

```bash
curl -fsSL https://raw.githubusercontent.com/AkioDevenish/Witclad/claude/bold-curie-0aqljk/unity/Wildtide/build-iphone.sh -o /tmp/wt.sh && bash /tmp/wt.sh
```

**With a Mac, by hand** (free Apple ID works):

1. **File → Build Profiles** (or **Build Settings**): choose **iOS**, **Switch Platform**, then **Build**.
   Pick an output folder, for example `Builds/iOS`.
2. Open `Builds/iOS/Unity-iPhone.xcodeproj` in Xcode.
3. Select the **Unity-iPhone** target, then **Signing & Capabilities**: tick **Automatically manage signing** and
   choose your Apple ID team. A free Apple ID works for your own phone; the app then expires after 7 days,
   and you just rebuild. The $99/year Apple Developer Program removes the 7-day limit and unlocks TestFlight
   and the App Store.
   If Xcode says the bundle id is taken, change it (for example `com.yourname.wildtide`) in
   `Assets/Wildtide/Editor/ProjectSetup.cs` and in Unity's Player Settings.
4. Plug in the iPhone, trust the computer, and turn on **Developer Mode**
   (iPhone Settings → Privacy & Security → Developer Mode). Pick the phone as the run destination and press **Run**.
5. The first time, the iPhone may block the app: go to Settings → General → VPN & Device Management and trust
   your developer certificate.


## Run it on Android

1. **File → Build Profiles**: choose **Android**, then **Switch Platform**.
2. On the phone, turn on Developer Options and USB debugging, then plug it in.
3. **Build And Run**. For the Play Store, tick **Build App Bundle (.aab)** and set up a keystore under
   Player Settings → Publishing Settings.

## Heroes

Pick **Lyra** (battle-mage) or **Sir Gareth** (paladin) with the Hero button on the island list. Their models are
`Assets/Wildtide/Resources/Characters/*.fbx`, built in Blender by `tools/blender/heroes.py`
(`pip install bpy==4.2.0`, then `python tools/blender/heroes.py`), which also saves `tools/blender/source/heroes.blend`
for hand editing and preview renders in `tools/blender/renders/`. The game recolours them with its toon shader
using the `#RRGGBB` in each material's name. They aren't rigged yet: the game squashes and bobs the whole model.

## Making islands

Islands are text in `Assets/Wildtide/Core/Levels.cs`: two grids of the same size, north at the top.
`Heights` gives each cell's column height (`1`-`9` steps, `.` is sea); `Things` puts something on top of it:

| Char | Thing |
|---|---|
| `S` / `G` | Start / golden shell (goal) |
| `o` | Pearl |
| `c` | Checkpoint flag |
| `^` | Sea-urchin spikes |
| `b` | Bounce pad (launches six steps up) |
| `e` / `E` | Crab walking east-west / north-south along flat ground |
| `x` / `z` | Raft drifting east-west / north-south across the open sea (its `Heights` digit is the raft's height) |
| `f` | Crumbling stone (its `Heights` digit is the stone's height) |

The player can climb 2 steps onto the next cell, jump a 1-cell gap going up 1 step, or a 2-cell gap on the level.
The tests (below) check every island with those limits, so a level you can't finish fails the build.

## Code layout

| Folder | What's there |
|---|---|
| `Assets/Wildtide/Core` | Rules with no Unity dependency: the level format and parser, the islands, a solver that proves each one can be finished, and saved progress. |
| `Assets/Wildtide/Runtime` | Unity side: `GameRoot` (flow and saving), `Stage` (builds an island and runs pickups, hazards and the goal, plus rafts, crabs, crumbling stones and bounce pads), `Player` (the character controller and isometric camera). |
| `Assets/Wildtide/Runtime/UI` | uGUI built in code: HUD with thumbstick and jump button, island select, cleared panel. |
| `Assets/Wildtide/Resources/Shaders` | `Wildtide/Toon`, the single cel shader for everything (URP, with a Built-in fallback). |
| `Assets/Wildtide/Editor` | First-open project setup for iOS and Android. |
| `Tests~/CoreTests` | Tests for the Core rules, including that every island can be finished. Run with `dotnet run` (Unity ignores this folder). |

Jump tuning lives in `Runtime/Player.cs` (`JumpHeight`, `RunSpeed`, `Gravity`) and must keep up with `Moves.Rise` in `Core/Reach.cs`.
