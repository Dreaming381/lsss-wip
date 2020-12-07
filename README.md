# Latios Space Shooter Sample - WIP

This is an open game project using Unity DOTS. It is used to develop new
features and improvements in Latios Framework (currently 0.3.0) as well as
serve as an example project. A version of the framework is included as an
embedded package.

The Unity version is currently 2020.1.9f1

Feel free to clone, play with, and customize this game however you like! There
are many features exposed in the project but not used in the release version of
the game. Feel free to send me your custom versions. I would love to try them
out!

You can download the latest binary version of the game here:
<https://dreaming381.itch.io/lsss>

## Special Project Restrictions

The entire game is made using Unity and Unity only. That means no assets from
other software are included in the project. No meshes. No textures. No sound
files. All texturing is done procedurally using ShaderGraph, and meshes are
composed of Unity primitives and procedural generation.

In stark contrast to my usual style, this game has no intention to establish any
significant lore or storyline. However, feel free to contribute your own!

## Contributing

Given that I work on this in my spare time, contributions are welcome. The less
time I spend designing levels, ships, and visuals, the more time I can spend
building new and exciting tech. No programming skills are required to help.

### Design Work

The easiest way to contribute is to design prefabs, shader graphs, and scenes.
Have a look at the existing Mission scenes to understand how the scenes and
prefabs can be put together. A fun starting place is to try and create a custom
faction with its own kind of ships.

There are a few planned aspects not present in these scenes:

-   Interiors for spawn graphics

-   Asteroid fields

-   Space stations (provide spatial reference and interconnected hangars for
    interior combat)

-   Lighting

### Code

Although I would much prefer people help out with design, there are a few code
aspects people can dare try to contribute to:

-   Editor Tools (I suck at writing these. If you don't suck at them, I envy
    your skills!)

-   Procedural audio clip generation (I just need the clips to be available
    during game object conversion)

-   Advanced AI (I have a basic AI in place which I will continue to develop.
    However, a faction could use a different AI instead. The only requirement to
    an AI is it needs to write to \`ShipDesiredActions\`. Feel free to add any
    components, systems, and authoring tools to get the job done.)

### Git

I prefer a linear history in master using rebase. Make sure your pull requests
don't contain merge commits. If you aren’t a git guru, just let me know and I
can clean up your branch for you.

## License

This project is licensed under the same licenses which govern the
Latios-Framework repository:
<https://github.com/Dreaming381/Latios-Framework/blob/master/LICENSE.md>
