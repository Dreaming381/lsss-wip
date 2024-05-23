# Latios Space Shooter Sample – Latios Framework Open Project \#1

This is the first open game project using Unity DOTS. It was used to validate
new features and improvements in Latios Framework up to version 0.4 and still
serves as an example project and compatibility validation. A version of the
framework is included as an embedded package.

The Unity version is currently 6000.0.2f1

Feel free to clone, play with, and customize this game however you like! There
are many features exposed in the project but not used in the release version of
the game. Feel free to send me your custom versions. I would love to try them
out!

You can download a binary version of the game here:
<https://dreaming381.itch.io/lsss>

## Getting Started

The entry scene is called Title and Menu in the Scenes folder. From there you
will be able to enter playmode. The game will use a gamepad as input if a
gamepad is detected. Otherwise it will fall back to mouse and keyboard controls.

## Special Project Restrictions

All assets in the game can be modified either directly in Unity (free UPM
package tools count) or via text editors. No other tools should be required.
Currently, meshes are procedurally generated from built-in primitives. All
texturing is done procedurally using ShaderGraph. Sound Effects are generated
from USFXR.

In stark contrast to my usual style, I have no intention to establish any
significant lore or storyline. However, feel free to contribute your own! The
contributions in the 0.3 version were amazing and I hope to see more!

## Contributing

Contributions are welcome, and I will monitor for them and ensure they are
integrated smoothly. No programming skills are required to help.

Please note that I primarily developed this game to help facilitate development
of new Latios Framework features. As the Latios Framework feature set has
matured beyond what this game required, I have very little motivation to develop
new gameplay features. But if you want to add new features, by all means go for
it! I still keep the project up to date, and it serves as an excellent tool for
detecting regressions.

If you have questions or would like to be involved in this or other open
projects, [join the Discord](https://discord.gg/FrqYeUv2dJ).

### Design Work

The easiest way to contribute is to design prefabs, shader graphs, and scenes.
Have a look at the existing Mission scenes to understand how the scenes and
prefabs can be put together. A fun starting place is to try and create a custom
faction with its own kind of ships.

There are a few originally planned aspects not present in these scenes:

-   A secondary spawning world where spawned ships fly through the wormhole
    effect to enter the space battle
-   Asteroid fields
-   Space stations (provide spatial reference and interconnected hangars for
    interior combat)
-   Lighting

### Code

Although I would much prefer people help out with design, there are a few code
aspects people can dare try to contribute to:

-   Editor Tools (I suck at writing these. If you don't suck at them, I envy
    your skills!)
-   Advanced AI (I have a basic AI in place which I will continue to develop.
    However, a faction could use a different AI instead. The only requirement to
    an AI is it needs to write to \`ShipDesiredActions\`. Feel free to add any
    components, systems, and authoring tools to get the job done.)

### Git

I prefer a linear history in master using rebase. Make sure your pull requests
don't contain merge commits. If you aren’t a git guru, just let me know and I
can clean up your branch for you.

## License

This project is licensed under the MIT license.

## Third Party Notices

This project includes an embedded version of the Latios Framework which is
licensed under the Unity Companion License as a derivative work:
<https://github.com/Dreaming381/Latios-Framework/blob/master/LICENSE.md>

This project includes USFXR via UPM which is licensed under Apache License 2.0.
