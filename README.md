# topdown-2d-multiplayer
top - down 2D multiplayer base on godot

Video Sample

https://www.youtube.com/playlist?list=PLlwvRbWsWmGUtEd47nyo0MUZJVJqvd4Iv

Code Reference
======
Base on following tutorials and rebuilt in C Sharp Godot Implementation (however, there are additional tweaks, so the code is no longer like their original codes)

Kehom's Forge Tutorials on Multi-Player (the first tutorial and second one using snapshots)

http://kehomsforge.com/tutorials/multi/gdMoreNetworking/

Kidcancode Youtube Channel

https://www.youtube.com/watch?v=sQ1FpD0DYF8&list=PLsk-HSGFjnaFC8kEv6MaLXnnDcevGpSWf

autonomousagents_behaviour_flock

https://bitbucket.org/gresille_siffle/godot2d_autonomousagents_behaviour_flock/src/master/

Artifact Reference
======
Graphic Asset by Kenny (please consider donate to Kenny on the website if like them, the attached one are all free ones, the paid version is not included as should purchase them through Kenney's site)

- https://www.kenney.nl/assets

Sound (from GameSounds website and following authors)

- https://gamesounds.xyz/?dir=FXHome/Bullet%20and%20Gun
- https://gamesounds.xyz/?dir=Sonniss.com%20-%20GDC%202016%20-%20Game%20Audio%20Bundle

What is working
======

- Basic top down shooter can attack and record health throughout network
- Basic network skeleton based on Authoritative Server network design with snapshot
- Basic audio and some basic effects 
- Basic enemy AI base on A* search and avoid collision with each other by disable collision between enemy
- Basic flocking/steer behavior (still need to improve)
- Weapon switch ability and framework (to support expansion of new weapon): laser/rifile/homing missles
- Destructible Obstacles
- Basic game flow conditions (base on exinging agent counts from bots and players)
- Support of game time period and expire (force winning condition base on available agents on the battlefied at that point) of 15 seconds and at server control (so server will sync time against connected client)
- Support of team concept (fridend firing vs firing from other teams)

Limitation
======
The total units on the game at once cannot exceed 20 units for workable performance, otherwise may start to see noticeable slow down. This may due to as the unit increases, a single snapshot package contain all units deployments will be larger than a udp packet able to handle, result delays on the client side when receiving the package and cause delay in screen

TODO
======
- The bot is equip with basic AI, but still not good with collision avoidance with each other (workaround now with no collision to save AI resource (there is basic code avoidance also in there can be enabled if need))
- Currently only track winning condition base on player agents vs bot agents, should also do team based
- Currently bot agents only added into one team, should try to balance
- Better Game Play
- Code clean up and more documentation (comments etc)
