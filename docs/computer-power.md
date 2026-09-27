# Computer power

The Start menu has **Shut down** and **Restart**. Shutdown closes all apps on that computer and leaves a dark power screen. Click **Power on** or press **Enter** while using the computer to start it. Escape leaves the desk from the desktop, power screen, or boot screen.

Startup takes four real-time seconds: a fictional BIOS check, boot-device selection, desktop service initialization, and a welcome screen. This is an in-game presentation, not a hardware diagnostic. Restart closes the current apps and runs the same sequence. Apps can be opened again once startup finishes.

Leaving the desk keeps the session open. Booting also continues while away. Power state belongs to each computer for the current scene; computers begin running when created or loaded. Shutdown does not erase virtual files, and it does not restart the game or the shared driver kernel. Drivers can serve other computers and continue running until stopped through System Monitor or unloaded with the mod.

Use `Run-VfsSmoke.ps1 -Power` with the runtime, game path, and disposable-save arguments described in [development](development.md). It exercises shutdown, the on-screen power button, timed startup, leaving and returning, app closure, and restart, with actual CRT screenshots. Manually check Enter on the off screen and Escape from each state.
