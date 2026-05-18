# Isolated WSL Sandbox for Claude Code

A WSL2 Ubuntu instance configured for running `claude --dangerously-skip-permissions` with meaningful isolation from the Windows host: separate filesystem, no Windows PATH inheritance, read-only access to Windows files, no LAN reachability, sandbox-only SSH credentials.

This manual covers the happy path only. Adapt commands to your own usernames, paths, and project repos.

---

## Prerequisites

- Windows 10 (version 2004+) or Windows 11 with WSL2 installed and updated (`wsl --update`).
- VS Code installed on Windows with the **WSL** extension (publisher: Microsoft).
- A GitHub account if cloning private repos or pushing.

---

## Step 1: Create a dedicated Ubuntu instance

Why: keeps the sandbox separate from your main dev environment, so a misbehaving Claude session can't trash your real work or credentials.

1. Download the Ubuntu 24.04 WSL image from <https://releases.ubuntu.com/noble/> — pick the file ending in `.wsl`.

2. From **PowerShell** (any window, not WSL):
   ```powershell
   mkdir C:\WSL\Ubuntu-Claude
   wsl --install --from-file C:\path\to\downloaded.wsl --name Ubuntu-Claude
   ```

3. Launch and create your user account when prompted:
   ```powershell
   wsl -d Ubuntu-Claude
   ```

---

## Step 2: Configure isolation in `/etc/wsl.conf`

Why: turns off Windows PATH inheritance (so the sandbox can't run `notepad.exe` etc. by name), mounts Windows drives read-only (so Claude can't write to or delete Windows files), and gives the instance a distinct hostname.

Inside the sandbox:

```bash
sudo nano /etc/wsl.conf
```

Replace the contents with (substitute your username for `frankray`):

```ini
[boot]
systemd=true

[user]
default=frankray

[automount]
enabled = true
mountFsTab = false
options = "metadata,uid=1000,gid=1000,umask=022,fmask=033,ro"

[interop]
enabled = true
appendWindowsPath = false

[network]
hostname = claude-sandbox
generateHosts = true
generateResolvConf = true
```

Note: `interop.enabled = true` is required for VS Code's WSL extension to install its server. `appendWindowsPath = false` still prevents Claude from running Windows binaries by name.

Apply by exiting and running from PowerShell:

```powershell
wsl --shutdown
```

Wait 30 seconds, then `wsl -d Ubuntu-Claude`.

---

## Step 3: Unmount unwanted drives via systemd

Why: `automount.enabled = true` mounts all Windows drives. If you have drives with sensitive data (Dropbox folders, source repos with secrets, personal documents), unmount them so Claude can't read them.

Create a systemd unit (substitute your drive letter for `d`):

```bash
sudo nano /etc/systemd/system/unmount-mnt-d.service
```

Paste:

```ini
[Unit]
Description=Unmount /mnt/d to keep it invisible to the sandbox
After=local-fs.target
ConditionPathIsMountPoint=/mnt/d

[Service]
Type=oneshot
ExecStart=/bin/umount /mnt/d
RemainAfterExit=no

[Install]
WantedBy=multi-user.target
```

Enable:

```bash
sudo systemctl daemon-reload
sudo systemctl enable unmount-mnt-d.service
```

From PowerShell, `wsl --shutdown` and reopen. Verify with `ls /mnt/` — only desired drives should appear with content.

Repeat the unit for each additional drive you want unmounted.

---

## Step 4: Network firewall — block LAN access

Why: by default the sandbox can reach your home router, NAS, other machines on your LAN, and other WSL2 instances. The firewall restricts it to the public internet only.

First, find your WSL gateway IP (changes per-machine):

```bash
ip route | awk '/default/ {print $3}'
```

Substitute the result wherever you see `172.26.80.1` below.

Install and configure UFW:

```bash
sudo apt update
sudo apt install -y ufw

sudo ufw default allow outgoing
sudo ufw default deny incoming

# Allow only the WSL gateway (DNS + outbound routing). Narrower than allowing the
# whole /20 subnet — this also blocks sandbox-to-sandbox traffic between WSL2 instances.
sudo ufw allow out to 172.26.80.1

# Block private network ranges
sudo ufw deny out to 10.0.0.0/8
sudo ufw deny out to 172.16.0.0/12
sudo ufw deny out to 192.168.0.0/16
sudo ufw deny out to 169.254.0.0/16

sudo ufw enable
```

Order matters: the allow rule must come before the broader denies it overrides. Confirm with `sudo ufw status numbered`.

Note: if WSL renumbers its virtual network (rare, but happens on some Windows updates), the gateway IP can change, and the firewall will silently block all outbound traffic. Re-check `ip route` and update the allow rule if internet stops working.

---

## Step 5: Install development tooling

Why: minimal stack for .NET development plus Claude Code.

```bash
sudo apt update
sudo apt install -y curl git build-essential ca-certificates

# .NET SDK (Ubuntu 24.04 ships .NET in its own repos — no Microsoft repo needed)
sudo apt install -y dotnet-sdk-8.0
dotnet --version

# Claude Code (Anthropic's official installer)
curl -fsSL https://claude.ai/install.sh | bash
claude --version
```

First run of `claude` will walk you through authentication via browser.

---

## Step 6: Generate a sandbox-only SSH key and clone

Why: never copy your main SSH keys into the sandbox. Generate a fresh key here; if it's ever compromised, you revoke only the sandbox's access.

```bash
ssh-keygen -t ed25519 -C "claude-sandbox" -f ~/.ssh/id_ed25519
cat ~/.ssh/id_ed25519.pub
```

Copy the output line. On GitHub: **Settings → SSH and GPG keys → New SSH key** → paste → save.

Test:

```bash
ssh -T git@github.com
```

Configure git identity (use your GitHub noreply email to keep your real email private):

```bash
git config --global user.name "yourusername"
git config --global user.email "<id>+yourusername@users.noreply.github.com"
```

Find your noreply address at <https://github.com/settings/emails>.

Clone your project:

```bash
mkdir -p ~/projects
cd ~/projects
git clone git@github.com:youruser/yourrepo.git
```

---

## Step 7: Connect VS Code

Why: VS Code's WSL extension installs a server inside the sandbox and connects to it. Editor, terminal, debugger all run with the sandbox as backend.

1. On Windows, open VS Code.
2. `Ctrl+Shift+P` → **WSL: Connect to WSL using Distro** → `Ubuntu-Claude`.
3. A new window opens; bottom-left shows `WSL: Ubuntu-Claude` in green.
4. `File → Open Folder` → `/home/<username>/projects/<repo>` → Open.
5. When prompted, install the **C# Dev Kit** (Microsoft) **into WSL: Ubuntu-Claude**, not locally.

The integrated terminal (`Ctrl+\``) runs inside the sandbox. Confirm by checking the prompt shows your sandbox hostname.

---

## Step 8: Take snapshots

Why: rollback insurance. If Claude trashes the instance later, restore from a snapshot in one command.

From PowerShell:

```powershell
mkdir C:\WSL\backups -ErrorAction SilentlyContinue
wsl --export Ubuntu-Claude C:\WSL\backups\ubuntu-claude-final.tar
```

Recommended snapshots to keep:
- **Baseline** — after Step 4 (isolation done, no tooling yet). Useful for rebuilds with different tooling.
- **Working** — after Step 5 (tooling installed). Useful if a later config change breaks things.
- **Final** — after Step 7 (fully configured). Your day-to-day rollback point.

Restore any snapshot with:

```powershell
wsl --shutdown
wsl --unregister Ubuntu-Claude
wsl --import Ubuntu-Claude C:\WSL\Ubuntu-Claude C:\WSL\backups\<snapshot>.tar
```

---

## Step 9: Run Claude

```bash
cd ~/projects/<repo>
claude --dangerously-skip-permissions
```

---

## Operational notes

**Don't put real credentials in this sandbox.** No AWS/Azure/GCP CLI logins, no production API keys, no SSH keys from your main account. The SSH key created here should be sandbox-only.

**Don't `cd` into mounted Windows directories for project work.** Files at `/mnt/c/...` are read-only and slow over 9P. Keep work in `~/projects/`.

**The Windows host can still read sandbox files** via `\\wsl.localhost\Ubuntu-Claude\home\<user>\`. The sandbox can't reach out to Windows, but Windows can reach in — usually what you want (so you can pull files out), but worth knowing.

**The isolation has limits.** WSL2 distros share a kernel and VM. A kernel-level exploit could cross between them. This sandbox protects against the realistic threat (an agent doing something dumb or getting prompt-injected); it's not a defence against targeted kernel attacks.

**Two failed cosmetic services are normal.** `console-getty.service` and `getty@tty1.service` will show as failed in `systemctl --failed`. WSL has no physical consoles for them to attach to. Mask them with `sudo systemctl mask console-getty.service getty@tty1.service` if the `degraded` system state bothers you.
