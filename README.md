# Latios Space Shooter Sample - WIP
This is a Unity DOTS game used to stabilize Latios Framework 0.2.0. A version of the framework is included as an embedded package. Once the game reaches a sufficient state of completeness and stability, it will be included as a sample project in Latios Framework.

The Unity version is currently 2020.1.0f1

## Special Project Restrictions
The entire game is made using Unity and Unity only. That means no assets from other software are included in the project. No meshes. No textures. No sound files. All texturing is done procedurally using ShaderGraph, and meshes are composed of Unity primitives and procedural generation.

In stark contrast to my usual style, this game has no intention to establish any significant lore or storyline.

## Contributing
Given that I work on this in my spare time, contributions are welcome. The sooner I get this game done, the sooner I can move on to documentation and new features in the framework. No programming skills are required to help.

### Design Work
The easiest way to contribute is to design prefabs, shader graphs, and scenes. Have a look at GameplayTest and MassiveBattleTest1 to understand how the scenes and prefabs can be put together. A fun starting place is to try and create a custom faction with its own kind of ships.

There are a few planned aspects not present in these scenes:
* Interiors for spawn graphics
* Asteroid fields
* Space stations (provide spatial reference and interconnected hangars for interior combat)
* Lighting

### Code
Although I would much prefer people help out with design, there are a few code aspects people can dare try to contribute to:
* Editor Tools (I suck at writing these. If you don't suck at them, I envy your skills!)
* DSP Graph Audio (I just haven't had time to dig into this much yet. Talk to me if you are interested in helping in this area!)
* Advanced AI (I am currently working on a basic AI. However, a faction could use a different AI instead. The only requirement to an AI is it needs to write to ShipDesiredActions. Feel free to add any components, systems, and authoring tools to get the job done.)

### Git
I prefer a linear history in master using rebase. Make sure your pull requests don't contain merge commits.

## License
This project is licensed under the same licenses which govern the Latios-Framework repository.
