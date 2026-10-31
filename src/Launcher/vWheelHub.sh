#!/bin/sh
set -eu
launch_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
if [ ! -x "$launch_dir/app/vWheelHub" ]; then
    printf '%s\n' 'Cannot start app/vWheelHub. Restore the complete application folder and its execute permission.' >&2
    exit 2
fi
exec "$launch_dir/app/vWheelHub" "$@"
