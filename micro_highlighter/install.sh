#!/bin/sh
# Installs the YRN assembly syntax highlighter for the micro editor.
# Copies yrn.yaml into micro's syntax directory (created if missing).

set -e

DIR="$(cd "$(dirname "$0")" && pwd)"

if [ -n "$MICRO_CONFIG_HOME" ]; then
    CONFIG_DIR="$MICRO_CONFIG_HOME"
elif [ -n "$XDG_CONFIG_HOME" ]; then
    CONFIG_DIR="$XDG_CONFIG_HOME/micro"
else
    CONFIG_DIR="$HOME/.config/micro"
fi

mkdir -p "$CONFIG_DIR/syntax"
cp "$DIR/yrn.yaml" "$CONFIG_DIR/syntax/yrn.yaml"
echo "Installed micro syntax to $CONFIG_DIR/syntax/yrn.yaml"
