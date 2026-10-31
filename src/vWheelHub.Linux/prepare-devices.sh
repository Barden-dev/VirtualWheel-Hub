#!/bin/sh
set -eu
# Executed only after consent, via pkexec. All privileged paths are fixed.
state=/var/lib/vwheelhub/preparation
test ! -L /var/lib/vwheelhub
test ! -L "$state"
mkdir -p "$state"
chmod 700 "$state"
apply_file() {
    target=$1
    key=$2
    content=$3
    test ! -L "$target"
    if test -f "$state/$key.applied"; then
        if test -f "$target" && test "$(sha256sum "$target" | cut -d ' ' -f1)" = "$(cat "$state/$key.applied")"; then return; fi
        # Resume an interrupted atomic replacement only while the original is intact.
        if test -f "$state/$key.absent"; then
            test ! -e "$target"
        else
            test "$(sha256sum "$target" | cut -d ' ' -f1)" = "$(cat "$state/$key.original-hash")"
        fi
    elif test -f "$target"; then
        cp -p "$target" "$state/$key.original"
        sha256sum "$state/$key.original" | cut -d ' ' -f1 > "$state/$key.original-hash"
    else
        touch "$state/$key.absent"
    fi
    tmp=$(mktemp "${target}.XXXXXX")
    printf '%s\n' "$content" > "$tmp"
    chmod 644 "$tmp"
    sha256sum "$tmp" | cut -d ' ' -f1 > "$state/$key.applied"
    mv "$tmp" "$target"
}
restore_file() {
    target=$1
    key=$2
    test -f "$state/$key.applied" || return 0
    test ! -L "$target"
    test -f "$target"
    test "$(sha256sum "$target" | cut -d ' ' -f1)" = "$(cat "$state/$key.applied")"
    if test -f "$state/$key.absent"; then
        rm -- "$target"
    else
        test "$(sha256sum "$state/$key.original" | cut -d ' ' -f1)" = "$(cat "$state/$key.original-hash")"
        cp -p "$state/$key.original" "$target"
    fi
    rm -f -- "$state/$key.applied" "$state/$key.absent" "$state/$key.original" "$state/$key.original-hash"
}
case "$1" in
apply)
    apply_file /etc/udev/rules.d/70-vwheelhub.rules rules 'KERNEL=="uinput", SUBSYSTEM=="misc", MODE="0660", TAG+="uaccess", OPTIONS+="static_node=uinput"
KERNEL=="event*", SUBSYSTEM=="input", MODE="0660", TAG+="uaccess"'
    apply_file /etc/modules-load.d/vwheelhub.conf module uinput
    if test -e /etc/udev/rules.d/99-vwheelhub.rules; then
        apply_file /etc/udev/rules.d/99-vwheelhub.rules legacy '# vWheelHub rules moved to 70-vwheelhub.rules'
    fi
    modprobe uinput
    ;;
rollback)
    restore_file /etc/udev/rules.d/70-vwheelhub.rules rules
    restore_file /etc/modules-load.d/vwheelhub.conf module
    restore_file /etc/udev/rules.d/99-vwheelhub.rules legacy
    ;;
*) exit 2 ;;
esac
udevadm control --reload-rules
udevadm trigger --subsystem-match=misc --sysname-match=uinput
udevadm trigger --subsystem-match=input --sysname-match='event*'
udevadm settle
