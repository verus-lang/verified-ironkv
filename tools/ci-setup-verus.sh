#!/bin/bash

set -e

git clone \
  --depth 1 \
  --recurse-submodules --shallow-submodules \
  https://github.com/verus-lang/verus
cd verus/source
# trigger toolchain installation
rustc --version
echo "Verus is at $PWD"
echo "Verus is at git revision: $(git rev-parse HEAD)"
