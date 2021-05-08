# DMDungeonGenerator
![DMDungeonGen](https://i.imgur.com/yrEyK3f.png)

## WHAT
A new version of my dungeon generator I wrote a few years back.  Generates procedural dungeons based off premade rooms in a way that makes cool dungeons with vertical changes, and intertwineing rooms. Included demo shows placing keys and locking doors (both unique keys, and small keys), and how to select rooms for "special" things (like bosses, or whatever).  Note, using this does requre you to write some code yourself. There is an example "CallbacksExample.cs" which hooks into the generator and shows some examples of "post generatation' stuff, like picking which doors to lock, where to place keys, etc, but since these may require game specific logic, you may have to write code yourself. I've tried to make it easy, though!

Rooms are created as prefabs with a custom data component and editor tool that allows you to easily draw the "voxels" the room occupies.  This voxel data is then used during the generation phase to check for overlaps.  I use this instead of standard mesh overlap checks as it's faster, and allows you to create interesting shaped rooms that can then fit together closely.  One cool example is an O shaped room, since it occupies no voxels in the center, an elevator shaft is free to spawn through that hole resulting in some really cool possibilities like this: 

![gif](https://i.imgur.com/NR2t53n.gif) ![gif2](https://i.imgur.com/F3nmEkq.gif) ![gif3](https://i.imgur.com/QplXWgv.gif)

## WHY
The old version (https://github.com/DMeville/Unity3d-Dungeon-Generator) was cool, but never got finished.  I had some free time (and some interest in some projects using the code) and decided to rewrite it from scratch to be better, faster, stronger and especially, easier to use. 

## WHO
### LICENSE
**MIT License. Do whatever you want!**

## WHERE
Drop the main DMDungeonGenerator folder anywhere in your project.  Open up the DMDungeonGenerator scene, press play to see the magic.

# Showcase Video
[Click to watch!](http://www.youtube.com/watch?v=z-4rLXBbI8k)

[![Dungeon gen](http://img.youtube.com/vi/z-4rLXBbI8k/0.jpg)](http://www.youtube.com/watch?v=z-4rLXBbI8k "DMDungeon Showcase")


# Room Setup Tutorial
[Click to watch!](http://www.youtube.com/watch?v=0bhOsZAI7OA)

[![](http://img.youtube.com/vi/0bhOsZAI7OA/0.jpg)](http://www.youtube.com/watch?v=0bhOsZAI7OA "Room Setup Tut")


# Support
Hit me up on twitter @DMeville or something. Only offering limited support.  PR are cool!
