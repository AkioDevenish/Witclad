# Wildtide (Unity)

The playable vertical slice for iOS and Android: pick a starter, explore Brightcove and Windmill Meadows,
meet wild creatures in the tall grass, battle them and bond them with lanterns. It saves automatically
and plays in landscape with touch controls.

Everything uses placeholder shapes until you drop in art from the Hugging Face pipeline
(see **Adding art** below).

## What's in the slice

- **16 creatures:** the three starter lines (Cindlet, Narlet and Budbara, each evolving at levels 16 and 36),
  six route creatures and the legendary Veyrath. All 12 elements have a strengths and weaknesses chart.
- **Turn-based battles:** Fight, Bag, Team and Run. Damage uses element matchups, same-element bonus,
  critical hits and stat stages.
- **Capture:** five Bond Lantern tiers, from Tin to Star. Lower HP and better lanterns give better odds,
  and the lantern wobbles up to three times.
- **Progression:** XP and level-ups, new moves as creatures grow, evolution after battles, and a full team
  of six with extras going to storage.
- **The world:** Brightcove (Hearth House healing, lab, harbor) and Windmill Meadows (five tall-grass patches,
  windmill, pond). Your lead creature follows you around.
- **Phone support:** a floating thumbstick, safe-area layout for the iPhone notch, 60 fps, and saves when the app
  is sent to the background. Android's back button closes menus.

## Open it

1. Install **Unity Hub**, then **Unity 6 (6000.0 LTS)** with these modules:
   **iOS Build Support** and **Android Build Support** (including OpenJDK and Android SDK & NDK).
2. In Unity Hub: **Add → Add project from disk** and pick this `unity/Wildtide` folder. If Hub asks about the
   editor version, pick any 6000.0.x you have installed.
3. On first open the project sets itself up: landscape orientation, bundle id `com.witclad.wildtide`,
   IL2CPP/ARM64 for Android, iOS 15+, URP and the toon shader, and `Assets/Scenes/Main.unity`.
   You can re-run it from **Wildtide → Set Up Project for iOS + Android**.
4. Press **Play**. Use WASD or the arrow keys in the editor, and click to advance text.
   Use **Wildtide → Delete Save** to start over.

The scene is empty on purpose: `GameRoot` builds the world, UI and cameras from code, so there's nothing to wire up.

## Run it on your iPhone

**No Mac?** Follow [PHONE.md](PHONE.md). GitHub builds the app in the cloud and sends it to TestFlight on your iPhone
(needs the Apple Developer Program). It also builds an Android APK.

**With a Mac** (free Apple ID works):

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

## Adding art

The game looks for art by the prompt id from `prompts/prompts.json` (for example `cindlet-stage-1`):

1. `Resources/Creatures/<id>`: a 3D model. Import `.glb` files with the glTFast package
   (Package Manager → add by name → `com.unity.cloud.gltfast`).
2. `Resources/Concepts/<id>.png`: a concept image, shown as a camera-facing billboard. It also
   appears on the starter pick screen. Use a transparent cutout.
3. Otherwise, a coloured placeholder.

From the repo root, the pipeline copies files in for you:

```bash
python -m wildtide cutout assets/creatures/cindlet-stage-1/concept_02.png
python -m wildtide unity cindlet-stage-1 assets/creatures/cindlet-stage-1/concept_02_cutout.png
python -m wildtide unity cindlet-stage-1 assets/sheets/cindlet/clean-view-for-3d-model.glb
```

## Code layout

| Folder | What's there |
|---|---|
| `Assets/Wildtide/Core` | Game rules with no Unity dependency: elements, species, moves, battle, capture, XP and save data. |
| `Assets/Wildtide/Runtime` | Unity side: `GameRoot` (flow and saving), `World` (greybox map), `Overworld` (player, camera, encounters), `BattleController`, `Visuals`. |
| `Assets/Wildtide/Runtime/UI` | uGUI built in code: battle screen, team panel, starter pick, joystick and dialog. |
| `Assets/Wildtide/Resources/Shaders` | `Wildtide/Toon`, the single cel shader for everything (URP, with a Built-in fallback). |
| `Assets/Wildtide/Editor` | First-open project setup for iOS and Android. |
| `Tests~/CoreTests` | Tests for the Core rules, including 500 random full battles. Run with `dotnet run` (Unity ignores this folder). |

Balance numbers live in `Core/Database.cs`. Add species, moves and encounter tables there, and the tests check
that every reference resolves.
