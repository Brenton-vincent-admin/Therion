# Building the game and getting it onto phones

Everything here costs $0. GitHub builds the Android APK for you in the cloud
(GameCI), and Firebase App Distribution sends each new version to the family's
phones with a notification. No Google Play account is used.

You set this up once. It takes about an hour, mostly clicking through websites.
After that, a new version reaches everyone's phones with one button press.

> **Why step 5 matters now:** Indonesia is one of the first four countries
> where Google's developer verification is enforced (since 30 September 2026).
> Certified Android phones there won't install an app whose package name isn't
> registered by a verified developer. The free hobbyist account covers this.

All secrets below go in **GitHub > your `therion` repo > Settings >
Secrets and variables > Actions > New repository secret**. Names must match
exactly.

---

## 1. Check GitHub Actions is on

Open the repo's **Actions** tab. If it asks, click **I understand my
workflows, go ahead and enable them**. Private repos get 2,000 free build
minutes a month; one build takes about 40 minutes the first time and 10 to 15
after that, so that's roughly 100 builds a month.

Until steps 2 to 4 are done, the "Android build" run shows a yellow warning
saying what's missing. That's expected.

## 2. Let GitHub use your free Unity licence

1. In **Unity Hub**, sign in with your Unity account using **email and
   password** (if you normally use "Sign in with Google", first set a password
   at [id.unity.com](https://id.unity.com)).
2. In Unity Hub go to **Preferences (gear) > Licenses > Add > Get a free
   personal license**. Skip this if a Personal licence is already listed.
3. Open this file in Notepad: `C:\ProgramData\Unity\Unity_lic.ulf`
   (on a Mac: `/Library/Application Support/Unity/Unity_lic.ulf`).
4. Add three secrets:

| Secret name | Value |
|---|---|
| `UNITY_LICENSE` | The **entire** contents of `Unity_lic.ulf` |
| `UNITY_EMAIL` | Your Unity account email |
| `UNITY_PASSWORD` | Your Unity account password |

If the build later fails with a licence or two-factor error, tell Claude in the
project thread; there's a workaround.

## 3. Make the game's signing key (keep it forever)

Every version of the game must be signed with the same key, or phones refuse
to update it and Google won't recognise it as yours. **If this key is lost you
can never update the game on existing phones or keep the package name
registered.**

1. Open the project in Unity 6. Go to **Edit > Project Settings > Player >
   Android tab > Publishing Settings > Keystore Manager**.
2. Choose **Keystore... > Create New > Anywhere**, save it as
   `therion-release.keystore` somewhere outside the repo (for example
   `Documents\Therion\`). Pick a strong password.
3. Under **New Key Values** set Alias to `therion`, give it a password
   (can be the same), Validity 50 years, a name in "First and Last Name", and
   click **Add Key**.
4. **Back it up now**: copy the `.keystore` file and both passwords to a USB
   stick and to a password manager or private cloud folder. Never commit it to
   GitHub.
5. Turn the file into text for GitHub. In **PowerShell**:

   ```powershell
   [Convert]::ToBase64String([IO.File]::ReadAllBytes("$HOME\Documents\Therion\therion-release.keystore")) | Set-Clipboard
   ```

   That copies a long block of text to your clipboard.
6. Add four secrets:

| Secret name | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | Paste the clipboard text |
| `ANDROID_KEYSTORE_PASS` | The keystore password |
| `ANDROID_KEYALIAS_NAME` | `therion` |
| `ANDROID_KEYALIAS_PASS` | The key password |

## 4. Set up Firebase App Distribution

1. Go to [console.firebase.google.com](https://console.firebase.google.com),
   click **Create a project**, name it `therion`. **Turn off Google
   Analytics** when asked (the game collects no data). Stay on the free
   **Spark** plan; never add billing.
2. On the project page click the **Android** icon to add an app.
   Package name: **`com.brenton.therion`** (must match Unity's Player
   Settings > Package Name, set from `GameInfo.AndroidPackageId`).
   Nickname: Therion. Click **Register app**, then **skip** the
   `google-services.json` and SDK steps (the game doesn't use Firebase code).
3. Click the gear > **Project settings**, and copy the **App ID** under your
   Android app (it looks like `1:123456789:android:abc123...`).
   Add it as secret **`FIREBASE_APP_ID`**.
4. In the left menu find **App Distribution** (under Run, or Release & Monitor) and click **Get started**.
   Open **Testers & Groups**, click **Add group**, name it `family`
   (the group alias must be `family`). Add the Gmail addresses of each person
   who will play, including the account on your son's phone.
5. Make a robot account that GitHub uses to upload:
   1. Open [console.cloud.google.com/iam-admin/serviceaccounts](https://console.cloud.google.com/iam-admin/serviceaccounts)
      and pick the `therion` project at the top.
   2. **Create service account**, name `github-uploader`, click **Create and
      continue**.
   3. Role: search for **Firebase App Distribution Admin**, select it,
      **Continue > Done**.
   4. Click the new account > **Keys > Add key > Create new key > JSON**.
      A `.json` file downloads.
   5. Open it in Notepad and paste the whole contents into secret
      **`FIREBASE_SERVICE_ACCOUNT`**. Then delete the downloaded file.

## 5. Register as a developer with Google (free, hobbyist)

1. Make sure your Google account has
   [2-Step Verification](https://myaccount.google.com/signinoptions/two-step-verification) on.
2. Go to [android.google.com/developerconsole](https://android.google.com/developerconsole)
   and create an account. Choose the **student / hobbyist (limited
   distribution)** type: no fee and no government ID. It asks for a payments
   profile (for your legal name and address, nothing is charged), a contact
   email and a phone number it checks with a code.
3. Run the first signed build (step 6) so you have the key's fingerprint. Open
   that run on the Actions tab; its summary has a box headed **Signing
   certificate SHA-256** holding a value like `AB:12:CD:...`.
4. In the console go to **Packages > Register package name**. Enter
   `com.brenton.therion`, friendly name Therion, **Next**. Click **Add
   key** and paste that SHA-256 fingerprint. Status shows **In review**; Google
   emails you when it's **Registered**.
5. Because the game is new (never installed anywhere yet), there's no extra
   "proof of ownership" APK upload. If the console asks for one anyway, tell
   Claude; it's a small file added to the game.

The hobbyist account lets the game go to a limited number of phones (Google's
pages say 20 in one place and 50 in another). Google describes devices being
added by a QR code or link and consent on the phone; follow the console's
prompts for each family phone. Phones can always install by USB cable (ADB)
even without this.

## 6. Build and send a version

1. On GitHub open **Actions > Android build > Run workflow**.
2. Leave **Send this build to the family testers** ticked, type a line for
   "What's new" (for example "Added the Triceratops!"), and click **Run
   workflow**.
3. About 15 to 40 minutes later, everyone in the `family` group gets an email
   and an App Tester notification.

Pushing a tag such as `v0.2.0` also builds and sends automatically. Ordinary
pushes to `main` build the APK (downloadable from the run's **Artifacts**) but
don't send it, so phones only get versions you choose.

## 7. Set up each phone (once)

1. On the phone, open the Firebase invite email and tap **Get started**. Sign
   in with the invited Google account and accept.
2. It offers to install **Firebase App Tester**. Allow "install unknown apps"
   for your browser when Android asks, then install it.
3. Open App Tester, tap **Dino Valley > Download**, and allow "install unknown
   apps" for App Tester when Android asks.
4. From now on, App Tester shows a notification when there's a new version;
   tap **Update**.

For your son's phone: if it uses a child account with Family Link, the parent
may need to approve App Tester and the install from your Family Link app. You
can also invite your own Gmail and sign in to App Tester with it on his phone.

## When something goes wrong

- **Yellow "secrets are missing" warning**: one of steps 2 to 4 isn't done, or
  a secret name is misspelled.
- **"App not installed" on update**: the new APK was signed with a different
  key. Make sure the keystore secrets are set; the very first unsigned test
  build must be uninstalled once.
- **"Can't install, developer not verified"**: step 5 isn't finished, or the
  registration is still "In review".
- Anything else: paste the error from the Actions run into the project thread.
