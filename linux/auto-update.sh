#!/bin/sh
# Keeps an installed FishBowl in step with this git checkout's upstream branch.
#   linux/auto-update.sh enable    check daily (systemd user timer) and reinstall when upstream changes
#   linux/auto-update.sh disable   stop checking
#   linux/auto-update.sh run       check now
# Only fast-forwards: if the checkout has local commits or edits, it stops and says so.
set -eu

# The timer runs an installed copy of this script, so remember which checkout to follow.
repo=${FISHBOWL_REPO:-$(cd "$(dirname "$0")/.." && pwd)}
self=${XDG_BIN_HOME:-$HOME/.local/bin}/fishbowl-auto-update
units=${XDG_CONFIG_HOME:-$HOME/.config}/systemd/user
stamp=${XDG_DATA_HOME:-$HOME/.local/share}/FishBowl/installed-commit

# Installs this script as $self with the checkout path built in, so manual runs of the copy work too.
install_self() {
    mkdir -p "$(dirname "$self")"
    sed "s|^repo=.*|repo=\${FISHBOWL_REPO:-$repo}|" "$1" > "$self.tmp" && chmod +x "$self.tmp" && mv "$self.tmp" "$self"
}

notify() { command -v notify-send >/dev/null 2>&1 && notify-send -a FishBowl -i fishbowl "FishBowl" "$1" || true; echo "$1"; }

run() {
    cd "$repo"
    branch=$(git rev-parse --abbrev-ref HEAD)
    upstream=$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null) || { notify "Auto-update skipped: branch $branch has no upstream to follow."; exit 1; }
    git fetch --quiet
    if [ -n "$(git status --porcelain --untracked-files=no)" ]; then notify "Auto-update skipped: $repo has uncommitted changes."; exit 1; fi
    git merge --quiet --ff-only "$upstream" || { notify "Auto-update skipped: $branch has diverged from $upstream."; exit 1; }
    head=$(git rev-parse HEAD)
    if [ "$(cat "$stamp" 2>/dev/null || true)" = "$head" ]; then echo "FishBowl is up to date ($(git log -1 --format=%h))."; exit 0; fi
    "$repo/linux/install-linux.sh"
    mkdir -p "$(dirname "$stamp")"; echo "$head" > "$stamp"
    [ -f "$repo/linux/auto-update.sh" ] && [ -f "$self" ] && install_self "$repo/linux/auto-update.sh"
    notify "FishBowl updated to $(git log -1 --format='%h: %s')"
}

case "${1:-run}" in
    run) run ;;
    enable)
        mkdir -p "$units"
        install_self "$0"
        cat > "$units/fishbowl-update.service" <<UNIT
[Unit]
Description=Update FishBowl from $repo
Wants=network-online.target
After=network-online.target

[Service]
Type=oneshot
Environment=FISHBOWL_REPO=$repo
ExecStart=$self run
UNIT
        cat > "$units/fishbowl-update.timer" <<UNIT
[Unit]
Description=Check for FishBowl updates daily

[Timer]
OnBootSec=5min
OnUnitActiveSec=1d
Persistent=true

[Install]
WantedBy=timers.target
UNIT
        systemctl --user daemon-reload
        systemctl --user enable --now fishbowl-update.timer
        echo "Daily FishBowl updates enabled. Logs: journalctl --user -u fishbowl-update" ;;
    disable)
        systemctl --user disable --now fishbowl-update.timer 2>/dev/null || true
        rm -f "$units/fishbowl-update.service" "$units/fishbowl-update.timer" "$self"
        systemctl --user daemon-reload
        echo "FishBowl auto-update disabled." ;;
    *) echo "Usage: $0 [run|enable|disable]" >&2; exit 2 ;;
esac
