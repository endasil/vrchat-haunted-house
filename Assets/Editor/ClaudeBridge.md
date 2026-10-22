# ClaudeBridge

`ClaudeBridge.cs` lets Claude Code drive the Unity Editor through files in `Temp/ClaudeBridge/`.

- Claude writes commands to `run.txt`. The bridge reads the file, deletes it and runs the commands in order.
- Each command and its result is appended to `output.txt`. The last line of a finished run is `== DONE ==`.
- Every `Debug.Log` message, with stack traces for errors, and every C# compile error goes to `console.log`.
- Images go to `<name>.png` in the same folder.

## Format

One command per line. Arguments are separated by `|`, so object paths can contain spaces. Lines that start with `#` are skipped.

A path is the full hierarchy path, for example `Level A/Room Finish/Room Finsh/Door Exit`. A path without `/` matches any object with that name. When more than one object matches, the bridge uses the first and writes a note.

## Commands

| Command | What it does |
|---|---|
| `play` | Enters play mode and waits until it runs. |
| `stop` | Leaves play mode and waits until edit mode is back. |
| `refresh` | Runs `AssetDatabase.Refresh` and waits for the script compile. |
| `wait \| seconds` | Waits that many seconds. |
| `screenshot \| name` | Saves the Game view to `name.png`. Play mode only. |
| `camera \| x \| y \| z \| pitch \| yaw \| name [\| fov]` | Renders a 1280x720 image from that position and angle. |
| `view \| path \| dx \| dy \| dz \| name [\| fov]` | Renders an image from the object's renderer bounds centre + (dx, dy, dz), looking at the centre. |
| `sceneview \| x \| y \| z \| pitch \| yaw [\| fov]` | Moves the Scene view camera to that position and angle, with gizmos on. |
| `scenegrab \| name` | Saves the Scene view window, with gizmos, to `name.png`. The window must be visible on the screen. Put `wait \| 1` after `sceneview` first. |
| `gizmos \| on/off` | Turns gizmos in the Scene view on or off. |
| `navmeshview \| on/off` | Turns the NavMesh display in the Scene view on or off. |
| `probeview \| mode` | Sets the light probe display in the Scene view, for example `None` or `OnlyProbesUsedBySelection`. Writes the mode it had before. |
| `maximize \| on/off` | Maximizes the Scene view or sets it back. |
| `scenerender \| name` | Renders the Scene view camera offscreen to `name.png`, without gizmos or occlusion visualization. |
| `probedots \| on/off [\| radius]` | Puts a yellow sphere on each baked light probe, for `camera` and `view` images. The spheres are not saved. |
| `scenehide \| text` | Hides every object whose name contains the text in the Scene view only. The scene does not change. |
| `sceneshowall` | Shows every object hidden in the Scene view again. |
| `occlusionview \| on/off` | Turns the occlusion culling visualization in the Scene view on or off. |
| `inspect \| path` | Writes active state, layer, world and local transform, bounds and components. |
| `find \| text` | Writes the path of every scene object whose name contains the text (case is ignored). |
| `lights \| path` | Writes every light whose range reaches the object's renderer bounds, and the ambient light settings. |
| `probe \| path` | Writes the baked light probe light at the renderer's bounds centre, for 6 surface normal directions. |
| `tree \| path [\| depth]` | Writes the child hierarchy. Default depth 2. |
| `getvar \| path \| variable` | Writes an Udon program variable from every UdonBehaviour on the object that has it. Play mode only. |
| `event \| path \| eventName` | Calls `SendCustomEvent` on every UdonBehaviour on the object. Play mode only. |
| `teleport \| x \| y \| z [\| yaw]` | Teleports the local player. Play mode only. |
| `teleportfront \| path \| distance` | Teleports the local player that many metres in front of the object, facing it. Play mode only. |
| `gettext \| path` | Writes the text of the TextMeshPro component on the object. |
| `setactive \| path \| on/off` | Turns the object on or off. In edit mode the scene changes, so use it in play mode. |
| `eyeheight \| metres` | Sets the local player's eye height. Play mode only. |
| `headcamera \| name [\| fov]` | Renders an image from the local player's head. Default fov 70. Play mode only. |
| `menu \| Menu/Path` | Runs an Editor menu item. |
| `select \| path` | Selects and pings the object in the Editor. |
| `clearlog` | Empties `console.log`. |
| `log \| text` | Writes the text to `console.log`, to mark a point in the log. |

## Notes

- `refresh` compiles scripts that changed on disk, so the Editor does not need focus for that.
- Play mode keeps running while the Editor is in the background because `Run In Background` is on in Player Settings.
- `play` returns when the scene runs, but ClientSim starts the Udon behaviours about 10 s later. Put `wait | 12` after `play` before `event` or `getvar`.
- `screenshot` needs an open Game view. `camera` and `view` work in edit mode and play mode.
