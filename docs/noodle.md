# Noodle

Open **Noodle** from the desktop or Start menu. Use the arrow keys or the four direction buttons to start and turn. Collect food, avoid the walls and your tail, and choose a direction to start again after a game ends.

Switching to another window pauses movement. Minimizing the window or leaving the computer also suspends its updates. Closing the app or shutting down the computer discards that round; scores are not saved. The game area keeps its aspect ratio when the window is maximized.

Noodle uses an independently cloned native game and tile grid from the installed game, with its native icon and colors. The desktop controls input and movement timing without opening the TV, changing its camera, or navigating through its home screen. The original TV's game is not used as the desktop's running instance. The cloned TV canvas stays hidden; only its owned playfield is placed in the desktop viewport.

If a game update removes required assets or native methods, the app displays an unavailable message and logs the reason. It uses the installed game's private movement hooks, so compatibility must be checked after game updates. No extracted game assets or implementations are distributed.

`Run-VfsSmoke.ps1 -Noodle` exercises button start, native movement and food consumption, wall collision and restart, a living instance paused behind Settings, minimize/resume, desk exit/return, source/tile isolation, normal and maximized viewport fit, and shutdown disposal of the game, grid, and sound objects. It boots the computer again and checks that reopening creates a fresh round, then repeats the scenario after a process reload in a disposable save. The initial phase uses the light theme and the reload phase uses dark. Both runtimes pass these checks. Physical keyboard input, audible sound quality, live scene unload with the app open, and simultaneous interaction with a placed TV still require validation.
