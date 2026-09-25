# Getting Wildtide onto your phone (no Mac needed)

GitHub builds the game in the cloud (`.github/workflows/phones.yml`):

- **iPhone:** a GitHub Mac builds the app and uploads it to **TestFlight**. You install it from the
  TestFlight app, and every push to the repo sends a new build to your phone.
- **Android:** you download an `.apk` from the workflow run and install it.

You set this up once. It needs three accounts: Apple Developer, Unity (free), and GitHub (you already have it).

> Apple only allows installing an app on an iPhone in two ways: from a Mac with Xcode (free Apple ID, but
> the app expires after 7 days), or through TestFlight or the App Store, which need the **$99/year Apple
> Developer Program**. This guide is the TestFlight route. If you have a Mac, `README.md` → *Run it on your
> iPhone* is quicker to try.

---

## 1. Apple (about 20 minutes, plus Apple's approval)

1. **Join the Apple Developer Program** at <https://developer.apple.com/programs/enroll/>.
   Approval can take up to 48 hours.
2. **Register the app's ID.** Go to <https://developer.apple.com/account/resources/identifiers/list>, click
   **+**, then **App IDs → App**. Give it the description `Wildtide` and the **Explicit** Bundle ID
   `com.witclad.wildtide`. Then **Continue → Register**.
   If that ID is taken, use your own (for example `com.yourname.wildtide`), and in GitHub add a repository
   *variable* (not a secret) named `BUNDLE_ID` with that value.
3. **Create the app record.** Go to <https://appstoreconnect.apple.com> → **Apps → + → New App**. Choose platform
   **iOS**, name `Wildtide` (it must be unique on the App Store, so try `Wildtide RPG` if it's taken), and
   pick the bundle ID from step 2. Use `wildtide` as the SKU and **Full Access**.
4. **Create an API key.** In App Store Connect, go to **Users and Access → Integrations → App Store Connect API →
   Team Keys → +**. Name it `GitHub`, set Access to **Admin**, then **Generate**.
   - **Download** the `.p8` file. Apple only lets you download it once.
   - Note the **Key ID** (shown in the table) and the **Issuer ID** (shown above the table).
5. **Find your Team ID** at <https://developer.apple.com/account> → **Membership details**. It's 10 characters.
6. **Add yourself as a tester.** In App Store Connect, open your app → **TestFlight → Internal Testing → +**.
   Create a group, add yourself, and turn on automatic distribution.
7. On your iPhone, install **TestFlight** from the App Store and sign in with the same Apple ID.

## 2. Unity license (about 10 minutes, needs any computer)

The cloud build needs a Unity license file. The free Personal license works.

1. Create a Unity account at <https://id.unity.com> if you don't have one.
2. Install **Unity Hub** on your Windows PC or Mac and sign in. Then go to
   **Settings → Licenses → Add → Get a free personal license**.
3. Find the license file and open it in a text editor:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`

## 3. GitHub secrets (5 minutes)

In the repo, go to **Settings → Secrets and variables → Actions → New repository secret**, and add each of these:

| Secret | Value |
|---|---|
| `UNITY_LICENSE` | The whole contents of `Unity_lic.ulf` |
| `UNITY_EMAIL` | Your Unity account email |
| `UNITY_PASSWORD` | Your Unity account password |
| `APPLE_TEAM_ID` | Team ID from step 1.5 |
| `APPSTORE_KEY_ID` | Key ID from step 1.4 |
| `APPSTORE_ISSUER_ID` | Issuer ID from step 1.4 |
| `APPSTORE_P8` | The whole contents of the `.p8` file, including the `-----BEGIN PRIVATE KEY-----` lines |

## 4. Build

Go to **Actions → Build for phones → Run workflow**. After that, it runs on every push that changes the game.

- The **Unity Android** and **Unity iOS** jobs take about 20 to 40 minutes the first time, and less once the cache is warm.
- **iPhone → TestFlight** uploads the build. About 5 to 15 minutes later it appears in the TestFlight app. Tap **Install**.
- For Android, open the finished run, download **Wildtide-Android** under *Artifacts*, unzip it, copy the `.apk`
  to your phone and open it. You'll need to allow installing from that source.

With only the Unity secrets set, you still get the Android APK and the iOS Xcode project. Only the TestFlight
step is skipped.

## If something fails

Open the failed job's log. The usual causes:

- **Unity license errors:** the `.ulf` is incomplete, or it came from a different Unity account than
  `UNITY_EMAIL`. Copy the file contents again.
- **"No Accounts" / "No profiles" in the TestFlight step:** the API key doesn't have the **Admin** role, or the
  bundle ID in App Store Connect doesn't match `com.witclad.wildtide` (or your `BUNDLE_ID` variable).
- **"The bundle version must be higher":** re-run the workflow. Build numbers come from the run number and only go up.
