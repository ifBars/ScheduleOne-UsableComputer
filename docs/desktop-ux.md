# Desktop and Files

Click an icon to select it; double-click to open it. Right-click a desktop icon, an Explorer item, or the empty background for the relevant menu. Menus dismiss on an outside click or Escape. Naming dialogs keep validation errors visible without discarding the entered name.

Drag a window by its title bar. Windows stay above the taskbar. Double-click a title bar to maximize or restore it. Clicking a covered window focuses it; clicking its active taskbar button minimizes it.

Drag desktop icons to arrange them. Positions survive reopening the computer and restarting the game. **Arrange Icons** in the desktop context menu returns to automatic placement. Drag a file or folder onto a folder to move it, including between Explorer and the desktop. The drag label previews the operation; invalid moves show an error and preserve the original item.

Files provides Back, Forward, Up, an editable virtual address, a filter for the current folder, and a collapsible task pane. File and Edit menus share the desktop clipboard. Copies receive new identities and preserve text and nested folders; failed copies leave the disk unchanged.

Keyboard commands apply to the focused file surface and do not intercept text entry:

- Enter: open the selected item.
- F2: rename; Delete: request deletion with confirmation.
- Ctrl+C / Ctrl+X / Ctrl+V: copy, cut, paste.
- Ctrl+Shift+N: create a folder; F5: refresh.
- Alt+Left / Alt+Right: Explorer history; Backspace: parent folder.

This is a small virtual desktop, not a Windows emulator. Operations currently select one item at a time. Deletion is permanent and requires confirmation; non-empty folders must be emptied first. App shortcuts are managed by installed apps and can be moved or renamed. The virtual filesystem never accesses arbitrary host paths.

## Validation

Run the filesystem verifier, build Mono and Il2cpp, then use `tests/Run-VfsSmoke.ps1 -Runtime Mono -DesktopUx ...` and the corresponding Il2cpp command with a completed source save. The runner uses a disposable save, restores mods and preferences, and checks native UI events for dragging, double-clicks, context menus, copy/paste, navigation, and icon placement across process reload. It captures the actual in-world display. The existing display smoke also checks the shared interaction scenario without changing icon preferences.
