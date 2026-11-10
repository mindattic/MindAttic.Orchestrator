# Talk to your Orchestrator from bed

Your Windows PC does the work: its RAM and CPU run every Claude CLI. Your phone is just the remote. Tailscale gets the phone
to the PC from anywhere, without port forwarding (it works on an apartment or HOA network). Remote Desktop shows you the PC.
On a phone-sized screen the Orchestrator switches to its phone layout (M.ORCH-T1543): the CLI fills the screen, with touch
keys and a mic underneath.

## 1. Install the Orchestrator on the PC

1. Download the newest `MindAttic.Orchestrator-<tag>.zip` from the repo's
   [Releases](https://github.com/mindattic/MindAttic.Orchestrator/releases), unzip it anywhere and double-click
   **Install.cmd**. It installs the Orchestrator and the MindAttic Launcher (which starts every CLI) into `C:\Apps`, puts
   `tickets` on your PATH, and opens the dashboard.
2. The **Welcome** window (M.ORCH-T1545) walks you through four steps, each one button:
   - **Tools**: installs git, the GitHub CLI, Claude Code, Python (the pacing hooks run on it) and the .NET 10 runtimes if they're missing.
   - **GitHub**: signs you in to GitHub in your browser, so your repos clone and your CLIs can push.
   - **Workspace**: the folder your repos live in. **Start an empty one** and clone your own repos into it; that's all
     anyone needs. Or clone MindAttic's own workspace, which needs its owner to add your GitHub account as a collaborator
     on the private `mindattic/codex-standard` (`gh api -X PUT repos/mindattic/codex-standard/collaborators/<username>`),
     or point at a folder of repos already on the PC.
   - **Claude account**: type your Claude account's email and sign in. A seat in the MindAttic Claude Team works as is:
     sign in with that seat's email. More accounts can be added later from the dashboard's **Logins**.
3. **Open the dashboard.** Start a CLI in a repo from it, and you're running.

## 2. Make the PC reachable

Do these once, at the PC.

1. **Tailscale on the PC.** Install it from tailscale.com/download and sign in. Right-click the tray icon, then
   **Preferences**, and turn on **Run unattended**, so it stays connected after a reboot even when nobody is signed in.
2. **No key expiry.** In the admin console (login.tailscale.com/admin/machines), open the **…** menu next to the PC and choose
   **Disable key expiry**. Otherwise Tailscale eventually asks you to sign in again, which you can't do remotely.
3. **Remote Desktop on.** Settings → System → Remote Desktop → **On**, with Network Level Authentication left checked. Remote
   Desktop needs Windows 11 **Pro**.
4. **Password sign-in.** Remote Desktop needs your Microsoft account *password*, not a PIN or Windows Hello. Settings →
   Accounts → Sign-in options → Additional settings: turn off **Only allow Windows Hello sign-in for Microsoft accounts**, and
   remove the PIN. Sign out, then at the sign-in screen choose **Sign-in options → key icon** and sign in with the password once:
   Windows checks the password it saved at your last password sign-in. To confirm it works, run this in Command Prompt:
   `runas /user:MicrosoftAccount\<your-email> cmd`
5. **Voice typing.** Settings → Privacy & security → Speech → **Online speech recognition: On**, so the mic button's
   Windows voice typing (Win+H) works.
6. **Stay awake.** Settings → System → Power: sleep **Never** when plugged in (the screen may turn off). Pause Windows Update
   before a trip so it doesn't restart unattended.

## 3. Set up the phone

1. Install **Tailscale** and sign in with the same account as the PC. If the PC belongs to someone else's tailnet, ask them to
   **Share** the machine with you from the admin console. Both should show **Connected**. Note the PC's Tailscale name (or its
   `100.x.x.x` address).
2. Install Microsoft's **Windows App** (iPhone, iPad, Android, Mac). Add a PC:
   - **PC name**: the Tailscale name, or the 100.x address.
   - **User**: `MicrosoftAccount\<your-email>`, and your Microsoft account password.
   - **Display**: resolution matched to the device, so Windows is phone-sized and the Orchestrator pivots to its phone layout.
   - **Device & audio redirection**: turn on the **Microphone**, so the PC hears you.

## 4. In bed

Connect from the Windows App and open the Orchestrator (it's on the desktop). Narrow or portrait, it shows the phone layout:

| Control | What it does |
| --- | --- |
| CLI tabs (top) | switch between your CLIs |
| The terminal | the CLI itself, full screen; scroll down for usage, the queue and the rest of the dashboard |
| Mic | opens Windows voice typing on the say box: talk, and your words land in it |
| Say box + **Send** | sends what's in the box to the CLI as one prompt and submits it (Enter does too) |
| `/voice` | starts Claude Code's own voice mode in the CLI |
| Esc, Tab, ⇧Tab, arrows, ^C, Enter | the keys a phone keyboard lacks: answer menus, switch mode (⇧Tab), interrupt (^C) |

When you're done, close the Windows App instead of signing out, so your CLIs keep running. Back at the desk the window widens
and the full dashboard returns. A window opened at desk size is maximized to fit when you reconnect from the phone.

## If something goes wrong

- **Password rejected.** Windows only updates its saved password when you sign in at the PC with it. Don't change the
  Microsoft account password while you're away; if you do, Remote Desktop expects the old one until you sign in at the PC again.
- **Connection times out but Tailscale shows Connected.** The Tailscale network may be set to Public. In an administrator
  PowerShell, `Get-NetConnectionProfile` shows it; set it to Private.
- **Too many failed attempts.** Windows locks the account for about 10 minutes after 10 wrong passwords. Wait it out.
- **The mic hears nothing.** Check the Windows App's microphone redirection, then Online speech recognition (step 2.5).
  Typing in the say box with the phone's keyboard always works.
- **Test it before you rely on it.** Put the phone on mobile data and connect. If it works there, it works anywhere.
