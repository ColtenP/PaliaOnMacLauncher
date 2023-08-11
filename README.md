# Palia On Mac Launcher

This is a launcher that is an alternative to the launcher provided by Palia.
It is written with a Rust Framework that does not seem to work very well with
Whisky/Wine/GamePortingToolKit.

This launcher is intended to be ran inside of a Whisky/Wine environment.

You will need to install the VC2015 C++ Redistributable Package for this to work,
and for Palia to work.

If you wish to change the installation directory or the PatchManifest.json url,
you can pass them in as command line arguments.

i.e. PaliaOnMacLauncher.exe "C:\Users\JaneDoe\Documents" "https://mycustomdomain.com/manifest/PatchManifest.json"

To install, simply download the .exe file and place it somewhere in the Whisky/Wine bottle and then
run it! Have fun hunting Chappas!
