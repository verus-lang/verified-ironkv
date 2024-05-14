#!/bin/bash

set -eu

cd verus/source
if [ -f z3 ]; then echo "Z3 already exists"; else ./tools/get-z3.sh; fi
# shellcheck disable=SC1091
source ../tools/activate
env VERUS_Z3_PATH="$PWD/z3" vargo build --release

# make verus binary accessible
echo "$PWD/target-verus/release" >>"$GITHUB_PATH"
