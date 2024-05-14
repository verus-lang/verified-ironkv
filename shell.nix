# Minimal set of build requirements for this directory
#
# Open a shell which has access to nothing but these requirements using
#    nix-shell --pure shell.nix
# from the root of the repository.

{ pkgs ? import <nixpkgs> {} }:
  pkgs.mkShell {
    nativeBuildInputs = with pkgs; [
      scons
      dotnet-sdk
      iconv
    ];
}
