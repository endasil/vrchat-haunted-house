# ClaudeBridge

`ClaudeBridge.cs` lets Claude Code drive the Unity Editor through files in `Temp/ClaudeBridge/`.

- Claude writes commands to `run.txt`. The bridge reads the file, deletes it and runs the commands in order.
- Each command and its result is appended to `output.txt`. The last line of a finished run is `== DONE ==`.
- Every `Debug.Log` message, with stack traces for errors, and every C# compile error goes to `console.log`.
- Images go to `<name>.png` in the same folder.

## Running commands from the shell

`tools/bridge.sh` writes `run.txt`, waits for `== DONE ==` and prints `output.txt`. After the output it prints the errors and exceptions that Unity logged during the run. It exits with 2 when a command wrote `ERROR`.

```
tools/bridge.sh 'play' 'teleport | -7.6 | 0.3 | -11.03 | 270' 'headcamera | bath' 'stop'
```

## Format

One command per line. Arguments are separated by `|`, so object paths can contain spaces. Lines that start with `#` are skipped.

A path is the full hierarchy path, for example `Level A/Room Finish/Room Finsh/Door Exit`. A path without `/` matches any object with that name. When more than one object matches, the bridge uses the first and writes a note. Add `#n` to the end of the path to use the n-th match, for example `Props/Mirror#2`.

## Commands

| Command | What it does |
|---|---|
| `play` | Enters play mode, waits until `Networking.IsNetworkSettled` is true and 1 s more (ClientSim has spawned the local player and started Udon), then closes the ClientSim menu with `ClientSimMenu.CloseMenu`. |
| `stop` | Leaves play mode and waits until edit mode is back. |
| `refresh` | Runs `AssetDatabase.Refresh` and waits for the C# compile and the UdonSharp compile. Timeout 600 s. |
| `click` | Clicks like the desktop player in ClientSim: a ray from the centre of the player camera. Writes every object the ray hits, nearest first, then which object ClientSim clicks. Play mode only. Aim with `teleport`: the camera is level, at the height that `headcamera` writes. |
| `trigger \| left/right [\| count]` | Presses and releases the trigger of that hand count times (default 1) through ClientSim's input, so Udon gets `InputUse` as from a real press. All presses happen in the same frame. A right press also clicks the object under the camera ray. Play mode only. |
| `raycast \| x \| y \| z \| dx \| dy \| dz` | Writes every collider the ray hits, nearest first, with distance, point, layer, trigger and UdonBehaviour. Works in edit mode. Use it to find where a wall or pillar surface is. |
| `near \| x \| y \| z \| radius` | Writes every renderer and collider whose bounds are within radius of the point, with the bounds. |
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
| `withcomponent \| TypeName` | Writes the path of every scene object with a component of that type, for example `VRCMirrorReflection`. |
| `fields \| path \| TypeName` | Writes every serialized field of the component, for example `fields \| Mirror \| MeshRenderer`. |
| `setfield \| path \| TypeName \| field \| value` | Sets one serialized field, as one Undo step. Colours and vectors are comma-separated numbers. A rotation takes Euler angles, for example `setfield \| Mirror \| Transform \| m_LocalRotation \| 0,180,0`. A reference takes an asset path (`Assets/...`), a shader name or `null`. Example: `setfield \| Mirror \| MeshRenderer \| m_Materials.Array.data[0] \| Assets/.../Dirty Mirror.mat`. |
| `call \| path \| TypeName \| method [\| arg ...]` | Calls a method on the component, also a private one, and writes the return value. An argument can be a number, `true`/`false`, comma-separated numbers for a vector or colour, an enum name, an object path, an asset path, or `maincamera`. |
| `material \| assetPath` | Writes the material's shader and every shader property value. |
| `setmaterial \| assetPath \| property \| value` | Sets one material property and saves the material to disk. The property `shader` takes a shader name. |
| `lights \| path` | Writes every light whose range reaches the object's renderer bounds, and the ambient light settings. |
| `probe \| path` | Writes the baked light probe light at the renderer's bounds centre, for 6 surface normal directions. |
| `tree \| path [\| depth]` | Writes the child hierarchy. Default depth 2. |
| `getvar \| path \| variable` | Writes an Udon program variable from every UdonBehaviour on the object that has it. Play mode only. |
| `event \| path \| eventName` | Calls `SendCustomEvent` on every UdonBehaviour on the object. Play mode only. |
| `teleport \| x \| y \| z [\| yaw]` | Teleports the local player. Play mode only. |
| `teleportfront \| path \| distance` | Teleports the local player that many metres in front of the object, facing it. Play mode only. "In front" is along the object's forward axis. A world-space canvas on a wall has its forward axis pointing into the wall, so the player lands behind the wall. Use `teleport` for wall signs. |
| `gettext \| path` | Writes the text of the TextMeshPro component on the object. |
| `setactive \| path \| on/off` | Turns the object on or off. In edit mode the scene changes, so use it in play mode. |
| `delete \| path` | Deletes the object and its children, as one Undo step. |
| `create \| name \| parent [\| primitive]` | Creates an empty object under the parent, as one Undo step. A primitive is `Cube`, `Sphere`, `Capsule`, `Cylinder`, `Plane` or `Quad`. The parent `root` puts the object at the top of the scene. |
| `instantiate \| prefab asset path \| parent` | Puts a prefab instance under the parent, as one Undo step. The parent `root` puts the object at the top of the scene. |
| `duplicate \| path` | Copies the object like Edit > Duplicate, as one Undo step. A copy of a prefab instance is also a prefab instance. Writes the path of the copy. |
| `addcomponent \| path \| TypeName` | Adds the component, as one Undo step. For an UdonSharp behaviour it also adds the UdonBehaviour. Use the full type name when two types have the same name. |
| `setparent \| path \| parent` | Moves the object under the parent and keeps its world position, as one Undo step. |
| `transform \| path \| x,y,z \| rx,ry,rz \| sx,sy,sz` | Sets the local position, rotation (Euler angles) and scale, as one Undo step. `-` keeps that value. |
| `rename \| path \| name` | Renames the object, as one Undo step. |
| `eyeheight \| metres` | Sets the local player's eye height. Play mode only. |
| `headcamera \| name [\| fov]` | Renders an image from the local player's head. Default fov 70. Play mode only. |
| `menu \| Menu/Path` | Runs an Editor menu item. |
| `select \| path` | Selects and pings the object in the Editor. |
| `clearlog` | Empties `console.log`. |
| `log \| text` | Writes the text to `console.log`, to mark a point in the log. |

## Notes

- `refresh` compiles scripts that changed on disk, so the Editor does not need focus for that.
- Play mode keeps running while the Editor is in the background because `Run In Background` is on in Player Settings.
- `play` returns when ClientSim has spawned the local player and started Udon, so `teleport`, `headcamera`, `event` and `getvar` work right after it.
- `screenshot` needs an open Game view. `camera` and `view` work in edit mode and play mode.
- Test anything pressable with `click`. `event | path | _interact` calls Interact directly and skips the raycast, so it passes when a collider in front blocks the click.
- While the ClientSim menu is open, ClientSim raycasts clicks only against the UI layers. `ClientSimMenu.CloseMenu` does nothing until the ClientSim camera stack is ready, so `play` calls it every frame until the menu is closed.
- Renderer bounds of walls, pillars and wall ends include trim and base parts. The visible surface can be several cm behind the bounds, and a collider can be in front of it. Measure with `raycast` before you place something on a surface.
- Mirrors show no reflection in `camera` and `view` images in edit mode. Test them in play mode with `headcamera`.
- Build scene objects with `create`, `instantiate`, `duplicate`, `addcomponent`, `transform` and `setfield`. Do not write an editor menu script to build an object. Such a script stays in the project after the object exists.
- After `refresh`, `addcomponent` for an UdonSharp behaviour works only when the UdonSharp compile is done. `refresh` waits for it.
- A `play`, `stop` or `refresh` stops a VRChat SDK build or upload. Look at the end of `Editor.log` for `BuildAndUploadMultiPlatform` or upload lines first.
