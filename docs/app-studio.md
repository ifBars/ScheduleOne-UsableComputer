# App Studio

App Studio edits plain Lua source with a synchronized line-number gutter and a scrollbar. Wrapped lines keep one source line number; resizing the window recalculates the gutter. Source is limited to 65,536 characters.

**Templates** offers the blank starter and every bundled example. Loading a template opens its draft, preserving edits in other drafts. Use **<** and **>** beside the filename to switch drafts. An asterisk marks modifications. Drafts are shared across computers in the current game process and survive closing the editor; they are discarded when the mod shuts down. Save your work before quitting.

**Save & run**, **Ctrl+S**, and **Ctrl+Enter** validate the source, save it under its app ID, and open the app. Rerunning an app replaces its previous session. Saves use a temporary file and atomic replacement so a failed write does not truncate the previous script.

Errors appear below the editor. Syntax errors include a source line; **Go to error** focuses that line. Save failures leave the editor open with the draft intact. Source stays plain text: syntax colouring is deferred to avoid changing editing, wrapping, or diagnostic offsets.

`Run-VfsSmoke.ps1 -Studio` checks 150-line source with wrapping, rendered gutter alignment and clipping, normal/maximized windows, diagnostics and caret navigation, template switching without losing edits, all bundled template renders, and real save/rerun using unique temporary scripts. Seed uses light theme; reload uses dark. Physical keyboard shortcuts still need a manual input pass.
