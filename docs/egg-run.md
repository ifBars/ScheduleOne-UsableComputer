# Egg Run

Open **Egg Run** from the desktop or Start menu. Press Space or Up, or click **Jump**, to start a round and jump. Hold Down or S while airborne, or click **Drop**, to descend faster. Avoid obstacles and try to beat the saved TV high score.

Egg Run uses the installed game's native icon, graphics, obstacle prefabs, spawners, scrolling ground, and character animation. It owns a separate playfield and never opens the TV interface or changes an existing TV app. Switching windows, minimizing, or leaving the computer pauses the round. Closing the window or shutting down discards it.

The desktop reads the native `RunGameHighScore` variable. A new best is saved through that variable only when the local game is the host or owns the player variable. Other clients can play, but their best score for this session does not alter the host's saved state. The desktop does not call the TV app's scene-state, camera, or navigation methods.

The app checks the live TV prefab before opening. If a game update removes required content, it displays an unavailable message and logs the reason. No extracted game assets or implementation files are distributed.
