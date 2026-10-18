#!/usr/bin/env bash
# Start the VisualWeb development browser on a given absolute URL.
#
# Usage: scripts/run-browser.sh [--unsandboxed] URL [extra browser args...]
#
# Default: confined --multiprocess mode (requires supported Linux/Windows sandboxing).
# --unsandboxed: single-process mode for TRUSTED content only.
# Environment: FONT overrides the trusted font face; CONFIGURATION (default Debug).
set -x
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${root}"

unsandboxed=0
if [[ ${1:-} == "--unsandboxed" ]]; then
	unsandboxed=1
	shift
fi

if [[ $# -lt 1 || $1 == "-h" || $1 == "--help" ]]; then
	sed -n '2,8s/^# \{0,1\}//p' "$0"
	exit 2
fi

url="$1"
shift
config="${CONFIGURATION:-Debug}"
font="${FONT:-tests/Engine.Text.Tests/Data/NotoSans.ttf}"
renderer="src/Apps/VisualWeb.Renderer/bin/${config}/net10.0/VisualWeb.Renderer.dll"

dotnet build VisualWeb.slnx -c "${config}" --nologo -v quiet

if [[ ${unsandboxed} -eq 1 ]]; then
	mode=(--development-single-process --allow-unsandboxed-development)
else
	mode=(--multiprocess --renderer "${renderer}")
fi

exec dotnet run --no-build -c "${config}" --project src/Apps/VisualWeb.Browser -- \
	"${mode[@]}" --font "${font}" --url "${url}" "$@"
