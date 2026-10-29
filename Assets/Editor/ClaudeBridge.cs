using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using UnityEditor;
using UnityEditor.Compilation;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon;

// Runs the commands in Temp/ClaudeBridge/run.txt and appends the results to
// Temp/ClaudeBridge/output.txt. ClaudeBridge.md lists the commands.
// The queue is kept in SessionState, so a run continues after the domain reloads that
// play mode and script compiles cause.
[InitializeOnLoad]
public static class ClaudeBridge
{
    private const string QueueKey = "ClaudeBridge.Queue";
    private const string IndexKey = "ClaudeBridge.Index";
    private const string WaitKey = "ClaudeBridge.Wait";
    private const string WaitUntilKey = "ClaudeBridge.WaitUntil";
    private const string NotBeforeKey = "ClaudeBridge.NotBefore";
    private const string WaitFileKey = "ClaudeBridge.WaitFile";

    private const string WaitTime = "time";
    private const string WaitPlay = "play";
    private const string WaitStop = "stop";
    private const string WaitCompile = "compile";
    private const string WaitFile = "file";
    private const string WaitClientSimStart = "clientsimstart";
    private const string WaitMenuClose = "menuclose";
    private const string WaitClick = "click";

    private const double ClientSimStartSeconds = 1.0;
    private const double MenuCloseTimeout = 30.0;
    private const double ClickDelaySeconds = 0.2;

    private const double StateChangeTimeout = 120.0;
    private const double CompileTimeout = 600.0;
    private const float MaxRayDistance = 100f;
    private const int MaxHitsWritten = 20;
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const double ScreenshotTimeout = 10.0;
    private const int ImageWidth = 1280;
    private const int ImageHeight = 720;

    private static readonly string BridgeDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "ClaudeBridge");
    private static readonly string RunFile = Path.Combine(BridgeDir, "run.txt");
    private static readonly string OutputFile = Path.Combine(BridgeDir, "output.txt");
    private static readonly string ConsoleFile = Path.Combine(BridgeDir, "console.log");
    private static readonly object ConsoleLock = new object();

    static ClaudeBridge()
    {
        Directory.CreateDirectory(BridgeDir);
        EditorApplication.update += Tick;
        Application.logMessageReceivedThreaded += OnLog;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
    }

    private static void Tick()
    {
        if (SessionState.GetString(QueueKey, "") == "")
        {
            if (File.Exists(RunFile))
            {
                StartRun();
            }
            return;
        }

        if (!IsWaitOver()) return;

        RunCommandsUntilWaitOrEnd();
    }

    private static void StartRun()
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(RunFile);
            File.Delete(RunFile);
        }
        catch (IOException)
        {
            // The shell is still writing run.txt. The next Tick reads it again.
            return;
        }

        string[] commands = lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("#"))
            .ToArray();
        File.WriteAllText(OutputFile, "== START " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " ==\n");

        if (commands.Length == 0)
        {
            Append("== DONE ==");
            return;
        }

        SessionState.SetString(QueueKey, string.Join("\n", commands));
        SessionState.SetInt(IndexKey, 0);
        ClearWait();
        RunCommandsUntilWaitOrEnd();
    }

    private static void RunCommandsUntilWaitOrEnd()
    {
        string[] commands = SessionState.GetString(QueueKey, "").Split('\n');
        int index = SessionState.GetInt(IndexKey, 0);

        while (index < commands.Length)
        {
            string command = commands[index];
            index++;
            // Stored before the command runs, because play and refresh reload the domain
            // and the next command must not run twice.
            SessionState.SetInt(IndexKey, index);

            Append("> " + command);
            try
            {
                Execute(command.Split('|').Select(part => part.Trim()).ToArray());
            }
            catch (Exception e)
            {
                Append("ERROR " + e.Message);
            }

            if (SessionState.GetString(WaitKey, "") != "") return;
        }

        SessionState.EraseString(QueueKey);
        SessionState.EraseInt(IndexKey);
        Append("== DONE ==");
    }

    private static void Execute(string[] args)
    {
        switch (args[0])
        {
            case "play":
                EditorApplication.isPlaying = true;
                StartWait(WaitPlay, StateChangeTimeout);
                break;
            case "stop":
                EditorApplication.isPlaying = false;
                StartWait(WaitStop, StateChangeTimeout);
                break;
            case "refresh":
                AssetDatabase.Refresh();
                StartWait(WaitCompile, CompileTimeout);
                // isCompiling can still be false in the frame after Refresh, so the wait checks it from 1 s on.
                SessionState.SetString(NotBeforeKey, Now(1.0));
                break;
            case "wait":
                StartWait(WaitTime, ParseFloat(Arg(args, 1)));
                break;
            case "screenshot":
                TakeScreenshot(Arg(args, 1));
                break;
            case "camera":
                RenderCamera(
                    new Vector3(ParseFloat(Arg(args, 1)), ParseFloat(Arg(args, 2)), ParseFloat(Arg(args, 3))),
                    Quaternion.Euler(ParseFloat(Arg(args, 4)), ParseFloat(Arg(args, 5)), 0f),
                    args.Length > 7 ? ParseFloat(args[7]) : 60f,
                    Arg(args, 6));
                break;
            case "view":
                View(args);
                break;
            case "sceneview":
                MoveSceneView(args);
                break;
            case "scenegrab":
                GrabSceneView(Arg(args, 1));
                break;
            case "scenehide":
                HideInSceneView(Arg(args, 1));
                break;
            case "sceneshowall":
                SceneVisibilityManager.instance.ShowAll();
                break;
            case "gizmos":
                GetSceneView().drawGizmos = Arg(args, 1) == "on";
                SceneView.RepaintAll();
                break;
            case "navmeshview":
                SetNavMeshView(Arg(args, 1) == "on");
                break;
            case "probeview":
                SetProbeView(Arg(args, 1));
                break;
            case "maximize":
                GetSceneView().maximized = Arg(args, 1) == "on";
                break;
            case "scenerender":
                RenderSceneViewCamera(Arg(args, 1));
                break;
            case "probedots":
                SetProbeDots(Arg(args, 1) == "on", args.Length > 2 ? ParseFloat(args[2]) : 0.06f);
                break;
            case "occlusionview":
                StaticOcclusionCullingVisualization.showOcclusionCulling = Arg(args, 1) == "on";
                SceneView.RepaintAll();
                break;
            case "inspect":
                Inspect(FindObject(Arg(args, 1)));
                break;
            case "tree":
                GameObject root = FindObject(Arg(args, 1));
                WriteTree(root.transform, 0, args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 2);
                break;
            case "getvar":
                GetVariable(FindObject(Arg(args, 1)), Arg(args, 2));
                break;
            case "event":
                SendEvent(FindObject(Arg(args, 1)), Arg(args, 2));
                break;
            case "teleport":
                Teleport(args);
                break;
            case "teleportfront":
                TeleportInFront(FindObject(Arg(args, 1)), ParseFloat(Arg(args, 2)));
                break;
            case "gettext":
                GameObject textObject = FindObject(Arg(args, 1));
                TMPro.TMP_Text text = textObject.GetComponent<TMPro.TMP_Text>();
                if (text == null)
                {
                    throw new Exception("'" + textObject.name + "' has no TextMeshPro text");
                }
                Append("text: " + text.text);
                break;
            case "setactive":
                FindObject(Arg(args, 1)).SetActive(Arg(args, 2) == "on");
                break;
            case "delete":
                GameObject deleted = FindObject(Arg(args, 1));
                string deletedPath = GetPath(deleted.transform);
                Undo.DestroyObjectImmediate(deleted);
                Append("deleted " + deletedPath);
                break;
            case "eyeheight":
                RequirePlayMode();
                GetLocalPlayer().SetAvatarEyeHeightByMeters(ParseFloat(Arg(args, 1)));
                break;
            case "headcamera":
                RenderFromHead(Arg(args, 1), args.Length > 2 ? ParseFloat(args[2]) : 70f);
                break;
            case "menu":
                if (!EditorApplication.ExecuteMenuItem(Arg(args, 1)))
                {
                    throw new Exception("no menu item '" + args[1] + "'");
                }
                break;
            case "find":
                FindAll(Arg(args, 1));
                break;
            case "withcomponent":
                FindWithComponent(Arg(args, 1));
                break;
            case "fields":
                WriteFields(FindComponent(FindObject(Arg(args, 1)), Arg(args, 2)));
                break;
            case "setfield":
                SetField(FindComponent(FindObject(Arg(args, 1)), Arg(args, 2)), Arg(args, 3), Arg(args, 4));
                break;
            case "call":
                CallMethod(FindComponent(FindObject(Arg(args, 1)), Arg(args, 2)), Arg(args, 3), args.Skip(4).ToArray());
                break;
            case "material":
                WriteMaterial(LoadMaterial(Arg(args, 1)));
                break;
            case "setmaterial":
                SetMaterialValue(LoadMaterial(Arg(args, 1)), Arg(args, 2), Arg(args, 3));
                break;
            case "probe":
                WriteProbeLight(FindObject(Arg(args, 1)));
                break;
            case "lights":
                ListLightsReaching(FindObject(Arg(args, 1)));
                break;
            case "select":
                GameObject selected = FindObject(Arg(args, 1));
                Selection.activeGameObject = selected;
                EditorGUIUtility.PingObject(selected);
                break;
            case "clearlog":
                lock (ConsoleLock)
                {
                    File.WriteAllText(ConsoleFile, "");
                }
                break;
            case "log":
                WriteConsoleLine("[bridge] " + Arg(args, 1));
                break;
            case "click":
                StartClick();
                break;
            case "trigger":
                PressTrigger(Arg(args, 1), args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 1);
                break;
            case "raycast":
                WriteRaycast(
                    new Vector3(ParseFloat(Arg(args, 1)), ParseFloat(Arg(args, 2)), ParseFloat(Arg(args, 3))),
                    new Vector3(ParseFloat(Arg(args, 4)), ParseFloat(Arg(args, 5)), ParseFloat(Arg(args, 6))));
                break;
            case "near":
                WriteNear(
                    new Vector3(ParseFloat(Arg(args, 1)), ParseFloat(Arg(args, 2)), ParseFloat(Arg(args, 3))),
                    ParseFloat(Arg(args, 4)));
                break;
            default:
                throw new Exception("unknown command '" + args[0] + "'");
        }
    }

    private static void StartWait(string kind, double seconds)
    {
        SessionState.SetString(WaitKey, kind);
        SessionState.SetString(WaitUntilKey, Now(seconds));
        SessionState.SetString(NotBeforeKey, Now(0.0));
    }

    private static void ClearWait()
    {
        SessionState.EraseString(WaitKey);
        SessionState.EraseString(WaitUntilKey);
        SessionState.EraseString(NotBeforeKey);
        SessionState.EraseString(WaitFileKey);
    }

    private static bool IsWaitOver()
    {
        string kind = SessionState.GetString(WaitKey, "");
        if (kind == "") return true;

        double now = EditorApplication.timeSinceStartup;
        double until = double.Parse(SessionState.GetString(WaitUntilKey, "0"), CultureInfo.InvariantCulture);
        double notBefore = double.Parse(SessionState.GetString(NotBeforeKey, "0"), CultureInfo.InvariantCulture);

        if (kind == WaitTime || kind == WaitClientSimStart || kind == WaitClick)
        {
            if (now < until) return false;
            ClearWait();
            if (kind == WaitClientSimStart)
            {
                StartWait(WaitMenuClose, MenuCloseTimeout);
                return false;
            }
            if (kind == WaitClick)
            {
                FinishClick();
            }
            return true;
        }

        bool done;
        try
        {
            done = now >= notBefore && IsWaitConditionMet(kind);
        }
        catch (Exception e)
        {
            Append("ERROR " + e.Message);
            ClearWait();
            return true;
        }

        if (done)
        {
            ClearWait();
            if (kind == WaitPlay)
            {
                // IsNetworkSettled is true one frame before ClientSim spawns the player and starts Udon,
                // and the ClientSim menu shows its first page after that.
                StartWait(WaitClientSimStart, ClientSimStartSeconds);
                return false;
            }
            if (kind == WaitMenuClose)
            {
                Append("ClientSim started, ClientSim menu closed");
            }
            return true;
        }

        if (now < until) return false;

        Append("ERROR timed out waiting for " + kind);
        ClearWait();
        return true;
    }

    private static bool IsWaitConditionMet(string kind)
    {
        switch (kind)
        {
            case WaitPlay:
                // EditorApplication.isPlaying is true as soon as play is requested, before the scene starts.
                return Application.isPlaying && !EditorApplication.isCompiling && Networking.IsNetworkSettled;
            case WaitStop:
                return !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode;
            case WaitCompile:
                return !EditorApplication.isCompiling && !EditorApplication.isUpdating && !IsUdonSharpCompiling();
            case WaitFile:
                return File.Exists(SessionState.GetString(WaitFileKey, ""));
            case WaitMenuClose:
                return TryCloseClientSimMenu();
            default:
                throw new Exception("unknown wait kind '" + kind + "'");
        }
    }

    // The ClientSim menu opens when play mode starts and covers the head camera images.
    // While it is open, ClientSim raycasts clicks only against the UI layers.
    // CloseMenu does nothing until the ClientSim camera stack is ready, so the wait calls it every frame.
    private static bool TryCloseClientSimMenu()
    {
        Component menu = FindSceneComponentOrNull("ClientSimMenu");
        if (menu == null) return false;

        InvokeMethod(menu, "CloseMenu");
        return !(bool)GetFieldValue(menu, "_menuIsActive");
    }

    // UdonSharp compiles the program assets after the C# compile, and U# components cannot be added before it is done.
    private static bool IsUdonSharpCompiling()
    {
        Type compiler = FindType("UdonSharp.Compiler.UdonSharpCompilerV1");
        const BindingFlags anyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo currentJob = compiler.GetProperty("CurrentJob", anyStatic);
        FieldInfo compileQueued = compiler.GetField("_compileQueued", anyStatic);
        if (currentJob == null || compileQueued == null)
        {
            throw new Exception("UdonSharpCompilerV1 has no CurrentJob or _compileQueued in this UdonSharp version");
        }
        return currentJob.GetValue(null) != null || (bool)compileQueued.GetValue(null);
    }

    // Writes what ClientSim's click ray hits, nearest first, then clicks after ClickDelaySeconds.
    // The delay lets ClientSim update its hover target after a teleport.
    private static void StartClick()
    {
        RequirePlayMode();
        Component playerRaycaster = FindSceneComponent("ClientSimPlayerRaycaster");
        object handRaycaster = GetFieldValue(playerRaycaster, "_rightHandRaycaster");
        Ray ray = (Ray)InvokeMethod(GetFieldValue(handRaycaster, "_rayProvider"), "GetRay");
        LayerMask layers = (LayerMask)InvokeMethod(GetFieldValue(handRaycaster, "_interactiveLayerProvider"), "GetInteractiveLayers");

        Append("ray from " + Format(ray.origin) + " direction " + Format(ray.direction));
        WriteHits(Physics.RaycastAll(ray, MaxRayDistance, layers));
        StartWait(WaitClick, ClickDelaySeconds);
    }

    private static void FinishClick()
    {
        try
        {
            Component playerRaycaster = FindSceneComponent("ClientSimPlayerRaycaster");
            object hover = GetFieldValue(playerRaycaster, "_hoverRight");
            if (hover == null || GetFieldValue(hover, "interactable") == null)
            {
                Append("ClientSim has no interactable under the ray, so the click does nothing");
                return;
            }

            Append("ClientSim clicks " + GetPath(((GameObject)GetFieldValue(hover, "hitObject")).transform));
            MethodInfo useInput = FindMethod(playerRaycaster, "UseInput", 2);
            Type handType = useInput.GetParameters()[1].ParameterType;
            useInput.Invoke(playerRaycaster, new[] { true, Enum.Parse(handType, "RIGHT") });
        }
        catch (Exception e)
        {
            Append("ERROR " + e.Message);
        }
    }

    // Sends press and release through ClientSim's input, so Udon gets InputUse as from a real trigger.
    // ClientSim also turns a right press into a click on the collider that the player camera ray hits.
    private static void PressTrigger(string hand, int count)
    {
        RequirePlayMode();
        Component playerRaycaster = FindSceneComponent("ClientSimPlayerRaycaster");
        object input = GetFieldValue(playerRaycaster, "_input");
        MethodInfo sendUse = FindMethod(input, "SendUseEvent", 2);
        object handType = Enum.Parse(sendUse.GetParameters()[1].ParameterType, hand.ToUpperInvariant());
        for (int i = 0; i < count; i++)
        {
            sendUse.Invoke(input, new[] { true, handType });
            sendUse.Invoke(input, new[] { false, handType });
        }
        Append("pressed the " + hand + " trigger " + count + " times");
    }

    private static void WriteRaycast(Vector3 origin, Vector3 direction)
    {
        Physics.SyncTransforms();
        WriteHits(Physics.RaycastAll(origin, direction.normalized, MaxRayDistance, Physics.AllLayers, QueryTriggerInteraction.Collide));
    }

    private static void WriteHits(RaycastHit[] hits)
    {
        if (hits.Length == 0)
        {
            Append("the ray hits nothing");
            return;
        }

        foreach (RaycastHit hit in hits.OrderBy(h => h.distance).Take(MaxHitsWritten))
        {
            Collider collider = hit.collider;
            Append(hit.distance.ToString("0.000", CultureInfo.InvariantCulture) + " m at " + Format(hit.point) + ": "
                + GetPath(collider.transform) + " (" + collider.GetType().Name
                + (collider.isTrigger ? ", trigger" : "")
                + ", layer " + LayerMask.LayerToName(collider.gameObject.layer)
                + (collider.GetComponent<UdonBehaviour>() != null ? ", UdonBehaviour" : "") + ")");
        }
        if (hits.Length > MaxHitsWritten)
        {
            Append((hits.Length - MaxHitsWritten) + " more hits");
        }
    }

    // Renderer and collider bounds within radius of the point. Bounds are boxes around the whole mesh,
    // so for a wall they include the trim in front of the wall face. Use raycast to find a visible surface.
    private static void WriteNear(Vector3 point, float radius)
    {
        Physics.SyncTransforms();
        float radiusSquared = radius * radius;
        int count = 0;
        foreach (Component component in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (!(component is Renderer) && !(component is Collider)) continue;
            if (EditorUtility.IsPersistent(component) || !component.gameObject.scene.IsValid()) continue;

            Bounds bounds = component is Renderer renderer ? renderer.bounds : ((Collider)component).bounds;
            if (bounds.SqrDistance(point) > radiusSquared) continue;

            Append(GetPath(component.transform) + " " + component.GetType().Name
                + ": centre " + Format(bounds.center) + ", size " + Format(bounds.size)
                + (component.gameObject.activeInHierarchy ? "" : " [inactive]"));
            count++;
        }
        Append(count + " renderers and colliders");
    }

    private static Component FindSceneComponent(string typeName)
    {
        Component component = FindSceneComponentOrNull(typeName);
        if (component == null)
        {
            throw new Exception("no " + typeName + " in the scene");
        }
        return component;
    }

    private static Component FindSceneComponentOrNull(string typeName)
    {
        return Resources.FindObjectsOfTypeAll<Component>().FirstOrDefault(c =>
            c != null && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid() && c.GetType().Name == typeName);
    }

    private static Type FindType(string fullName)
    {
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null) return type;
        }
        throw new Exception("no type " + fullName);
    }

    private static object GetFieldValue(object target, string fieldName)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, AnyInstance | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(target);
        }
        throw new Exception(target.GetType().Name + " has no field '" + fieldName + "'");
    }

    private static MethodInfo FindMethod(object target, string methodName, int parameterCount)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            MethodInfo method = type.GetMethods(AnyInstance | BindingFlags.DeclaredOnly)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == parameterCount);
            if (method != null) return method;
        }
        throw new Exception(target.GetType().Name + " has no method '" + methodName + "' with " + parameterCount + " arguments");
    }

    private static object InvokeMethod(object target, string methodName)
    {
        return FindMethod(target, methodName, 0).Invoke(target, null);
    }

    private static void TakeScreenshot(string name)
    {
        RequirePlayMode();
        string path = ImagePath(name);
        File.Delete(path);
        // CaptureScreenshot writes the file at the end of a later frame, so the run waits for it.
        ScreenCapture.CaptureScreenshot(path);
        StartWait(WaitFile, ScreenshotTimeout);
        SessionState.SetString(WaitFileKey, path);
        Append(path);
    }

    private static void View(string[] args)
    {
        GameObject target = FindObject(Arg(args, 1));
        Vector3 centre = GetBounds(target, out bool hasRenderers).center;
        if (!hasRenderers)
        {
            Append("note: no renderers under the object, using its position");
        }

        Vector3 position = centre + new Vector3(ParseFloat(Arg(args, 2)), ParseFloat(Arg(args, 3)), ParseFloat(Arg(args, 4)));
        RenderCamera(position, Quaternion.LookRotation(centre - position), args.Length > 6 ? ParseFloat(args[6]) : 60f, Arg(args, 5));
    }

    private static void RenderCamera(Vector3 position, Quaternion rotation, float fieldOfView, string name)
    {
        var cameraObject = new GameObject("ClaudeBridgeCamera") { hideFlags = HideFlags.HideAndDontSave };
        var renderTexture = new RenderTexture(ImageWidth, ImageHeight, 24);
        var image = new Texture2D(ImageWidth, ImageHeight, TextureFormat.RGB24, false);
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 500f;
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0, 0, ImageWidth, ImageHeight), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            string path = ImagePath(name);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Append(path + " from " + Format(position) + " looking " + Format(rotation.eulerAngles));
        }
        finally
        {
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static void MoveSceneView(string[] args)
    {
        SceneView sceneView = GetSceneView();
        var pose = new GameObject("ClaudeBridgePose") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            pose.transform.SetPositionAndRotation(
                new Vector3(ParseFloat(Arg(args, 1)), ParseFloat(Arg(args, 2)), ParseFloat(Arg(args, 3))),
                Quaternion.Euler(ParseFloat(Arg(args, 4)), ParseFloat(Arg(args, 5)), 0f));
            sceneView.cameraSettings.fieldOfView = args.Length > 6 ? ParseFloat(args[6]) : 60f;
            sceneView.drawGizmos = true;
            sceneView.AlignViewToObject(pose.transform);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pose);
        }
        sceneView.Focus();
        sceneView.Repaint();
    }

    // Reads the Scene view from the screen, so gizmos and overlays are in the image.
    // The Scene view must be visible on the screen and not covered by another window.
    private static void GrabSceneView(string name)
    {
        SceneView sceneView = GetSceneView();
        Rect window = sceneView.position;
        int width = Mathf.RoundToInt(window.width);
        int height = Mathf.RoundToInt(window.height);
        Color[] pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(window.position, width, height);

        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            image.SetPixels(pixels);
            image.Apply();
            string path = ImagePath(name);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Append(path + " " + width + "x" + height);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    // NavMeshVisualizationSettings is internal, so it is set through reflection.
    private static void SetNavMeshView(bool on)
    {
        Type settings = typeof(Editor).Assembly.GetType("UnityEditor.AI.NavMeshVisualizationSettings");
        var property = settings?.GetProperty("showNavigation", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (property == null)
        {
            throw new Exception("NavMeshVisualizationSettings.showNavigation is not in this Unity version");
        }
        property.SetValue(null, on ? 1 : 0);
        SceneView.RepaintAll();
    }

    // Renders the Scene view camera offscreen. Gizmos are not in the image.
    private static void RenderSceneViewCamera(string name)
    {
        Camera camera = GetSceneView().camera;
        RenderTexture previousTarget = camera.targetTexture;
        var renderTexture = new RenderTexture(ImageWidth, ImageHeight, 24);
        var image = new Texture2D(ImageWidth, ImageHeight, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0, 0, ImageWidth, ImageHeight), 0, 0);
            image.Apply();
            string path = ImagePath(name);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Append(path);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = null;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private const string ProbeDotsName = "ClaudeBridgeProbeDots";

    // Puts a yellow sphere on each baked light probe. The spheres are not saved with the scene.
    private static void SetProbeDots(bool on, float radius)
    {
        foreach (GameObject old in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == ProbeDotsName).ToArray())
        {
            UnityEngine.Object.DestroyImmediate(old);
        }
        if (!on) return;

        LightProbes probes = LightmapSettings.lightProbes;
        if (probes == null)
        {
            throw new Exception("the scene has no baked light probes");
        }

        var root = new GameObject(ProbeDotsName) { hideFlags = HideFlags.HideAndDontSave };
        var material = new Material(Shader.Find("Standard")) { color = new Color(1f, 0.85f, 0.2f), hideFlags = HideFlags.HideAndDontSave };
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(0.45f, 0.36f, 0.05f));
        material.SetFloat("_Glossiness", 0.7f);
        Mesh sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        foreach (Vector3 position in probes.positions)
        {
            var dot = new GameObject("dot") { hideFlags = HideFlags.HideAndDontSave };
            dot.transform.SetParent(root.transform);
            dot.transform.position = position;
            dot.transform.localScale = Vector3.one * radius * 2f;
            dot.AddComponent<MeshFilter>().sharedMesh = sphere;
            MeshRenderer renderer = dot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Append(probes.positions.Length + " probe dots");
    }

    // LightProbeVisualization is internal, so it is set through reflection.
    // Writes the mode it had, so a later probeview can set it back.
    private static void SetProbeView(string mode)
    {
        Type visualization = typeof(Editor).Assembly.GetType("UnityEditor.LightProbeVisualization");
        var property = visualization?.GetProperty("lightProbeVisualizationMode", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (property == null)
        {
            throw new Exception("LightProbeVisualization.lightProbeVisualizationMode is not in this Unity version");
        }
        Append("previous mode: " + property.GetValue(null));
        property.SetValue(null, Enum.Parse(property.PropertyType, mode));
        SceneView.RepaintAll();
    }

    private static void HideInSceneView(string name)
    {
        var matches = new List<GameObject>();
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (EditorUtility.IsPersistent(t) || !t.gameObject.scene.IsValid()) continue;
            if (t.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;

            matches.Add(t.gameObject);
        }
        SceneVisibilityManager.instance.Hide(matches.ToArray(), true);
        Append("hid " + matches.Count + " objects in the Scene view");
    }

    private static SceneView GetSceneView()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            throw new Exception("no Scene view is open");
        }
        return sceneView;
    }

    private static void Inspect(GameObject target)
    {
        Transform t = target.transform;
        Append("path: " + GetPath(t));
        Append("activeSelf: " + target.activeSelf + ", activeInHierarchy: " + target.activeInHierarchy + ", layer: " + LayerMask.LayerToName(target.layer));
        Append("world position: " + Format(t.position) + ", rotation: " + Format(t.eulerAngles) + ", lossyScale: " + Format(t.lossyScale));
        Append("local position: " + Format(t.localPosition) + ", rotation: " + Format(t.localEulerAngles) + ", scale: " + Format(t.localScale));

        Bounds bounds = GetBounds(target, out bool hasRenderers);
        if (hasRenderers)
        {
            Append("renderer bounds: centre " + Format(bounds.center) + ", size " + Format(bounds.size));
        }

        foreach (Component component in target.GetComponents<Component>())
        {
            if (component == null)
            {
                Append("component: missing script");
                continue;
            }

            string line = "component: " + component.GetType().Name;
            if (component is UdonBehaviour udon && udon.programSource != null)
            {
                line += " (" + udon.programSource.name + ")";
            }
            if (component is Behaviour behaviour && !behaviour.enabled)
            {
                line += " [disabled]";
            }
            if (component is Collider collider && !collider.enabled)
            {
                line += " [disabled]";
            }
            if (component is Renderer renderer)
            {
                line += renderer.enabled ? "" : " [disabled]";
                line += " materials: " + string.Join(", ", renderer.sharedMaterials.Select(m => m == null ? "null" : AssetDatabase.GetAssetPath(m)));
            }
            Append(line);
            if (component is MeshRenderer meshRenderer)
            {
                Append("  lightmapIndex: " + meshRenderer.lightmapIndex
                    + ", lightmapScaleOffset: " + meshRenderer.lightmapScaleOffset
                    + ", receiveGI: " + meshRenderer.receiveGI
                    + ", scaleInLightmap: " + new SerializedObject(meshRenderer).FindProperty("m_ScaleInLightmap").floatValue
                    + ", shadowCasting: " + meshRenderer.shadowCastingMode
                    + ", lightProbeUsage: " + meshRenderer.lightProbeUsage);
                Append("  static flags: " + GameObjectUtility.GetStaticEditorFlags(target));
            }
        }
    }

    private static void WriteTree(Transform t, int level, int maxDepth)
    {
        Append(new string(' ', level * 2) + t.name + (t.gameObject.activeSelf ? "" : " [inactive]"));
        if (level >= maxDepth) return;

        foreach (Transform child in t)
        {
            WriteTree(child, level + 1, maxDepth);
        }
    }

    private static void GetVariable(GameObject target, string variable)
    {
        RequirePlayMode();
        bool found = false;
        foreach (UdonBehaviour udon in target.GetComponents<UdonBehaviour>())
        {
            if (udon.GetProgramVariableType(variable) == null) continue;

            found = true;
            string program = udon.programSource == null ? "?" : udon.programSource.name;
            Append(program + "." + variable + " = " + FormatValue(udon.GetProgramVariable(variable)));
        }

        if (!found)
        {
            throw new Exception("no UdonBehaviour on '" + target.name + "' has a variable '" + variable + "'");
        }
    }

    private static void SendEvent(GameObject target, string eventName)
    {
        RequirePlayMode();
        UdonBehaviour[] behaviours = target.GetComponents<UdonBehaviour>();
        if (behaviours.Length == 0)
        {
            throw new Exception("no UdonBehaviour on '" + target.name + "'");
        }

        foreach (UdonBehaviour udon in behaviours)
        {
            udon.SendCustomEvent(eventName);
        }
        Append("sent to " + behaviours.Length + " UdonBehaviour(s)");
    }

    private static void Teleport(string[] args)
    {
        RequirePlayMode();
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null)
        {
            throw new Exception("there is no local player yet");
        }

        var position = new Vector3(ParseFloat(Arg(args, 1)), ParseFloat(Arg(args, 2)), ParseFloat(Arg(args, 3)));
        float yaw = args.Length > 4 ? ParseFloat(args[4]) : player.GetRotation().eulerAngles.y;
        player.TeleportTo(position, Quaternion.Euler(0f, yaw, 0f));
    }

    // Puts the local player that many metres in front of the object, facing it.
    private static void TeleportInFront(GameObject target, float distance)
    {
        RequirePlayMode();
        VRCPlayerApi player = GetLocalPlayer();
        Vector3 forward = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
        Vector3 position = target.transform.position + forward * distance;
        player.TeleportTo(position, Quaternion.LookRotation(-forward));
        Append("teleported to " + Format(position));
    }

    private static void RenderFromHead(string name, float fieldOfView)
    {
        RequirePlayMode();
        VRCPlayerApi.TrackingData head = GetLocalPlayer().GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        RenderCamera(head.position, head.rotation, fieldOfView, name);
    }

    private static VRCPlayerApi GetLocalPlayer()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null)
        {
            throw new Exception("there is no local player yet");
        }
        return player;
    }

    private static void WriteProbeLight(GameObject target)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            throw new Exception("'" + target.name + "' has no Renderer");
        }

        LightProbes.GetInterpolatedProbe(renderer.bounds.center, renderer, out UnityEngine.Rendering.SphericalHarmonicsL2 probe);
        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        string[] names = { "up", "down", "forward", "back", "left", "right" };
        var colors = new Color[1];
        for (int i = 0; i < directions.Length; i++)
        {
            probe.Evaluate(new[] { directions[i] }, colors);
            Append("light from probes toward " + names[i] + ": " + colors[0]);
        }
    }

    // A directional light has no range, so it reaches every object.
    private static void ListLightsReaching(GameObject target)
    {
        Bounds bounds = GetBounds(target, out bool _);
        foreach (Light light in UnityEngine.Object.FindObjectsOfType<Light>())
        {
            float distance = Mathf.Sqrt(bounds.SqrDistance(light.transform.position));
            if (light.type != LightType.Directional && distance > light.range) continue;

            Append(GetPath(light.transform) + ": " + light.type + ", " + light.lightmapBakeType
                + ", intensity " + light.intensity + ", range " + light.range
                + ", colour " + light.color + ", distance " + distance.ToString("0.00", CultureInfo.InvariantCulture)
                + ", shadows " + light.shadows + (light.isActiveAndEnabled ? "" : " [off]"));
        }
        Append("ambient: " + RenderSettings.ambientMode + ", " + RenderSettings.ambientLight + ", intensity " + RenderSettings.ambientIntensity);
    }

    private static void FindAll(string name)
    {
        int count = 0;
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (EditorUtility.IsPersistent(t) || !t.gameObject.scene.IsValid()) continue;
            if (t.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;

            Append(GetPath(t));
            count++;
        }
        Append(count + " objects");
    }

    private static void FindWithComponent(string typeName)
    {
        int count = 0;
        foreach (Component component in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (component == null || EditorUtility.IsPersistent(component) || !component.gameObject.scene.IsValid()) continue;
            if (component.GetType().Name != typeName) continue;

            Append(GetPath(component.transform));
            count++;
        }
        Append(count + " objects");
    }

    private static Component FindComponent(GameObject target, string typeName)
    {
        Component component = target.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == typeName);
        if (component == null)
        {
            throw new Exception("'" + target.name + "' has no " + typeName);
        }
        return component;
    }

    private static void WriteFields(UnityEngine.Object target)
    {
        SerializedProperty property = new SerializedObject(target).GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = property.propertyType == SerializedPropertyType.Generic;
            Append(property.propertyPath + " = " + FormatProperty(property));
        }
    }

    // Applied through SerializedObject, so the change is one Undo step.
    private static void SetField(UnityEngine.Object target, string propertyPath, string value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyPath);
        if (property == null)
        {
            throw new Exception("'" + target.name + "' has no property '" + propertyPath + "'");
        }

        string before = FormatProperty(property);
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
                property.intValue = int.Parse(value, CultureInfo.InvariantCulture);
                break;
            case SerializedPropertyType.Float:
                property.floatValue = ParseFloat(value);
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = bool.Parse(value);
                break;
            case SerializedPropertyType.String:
                property.stringValue = value;
                break;
            case SerializedPropertyType.Enum:
                int index = Array.IndexOf(property.enumNames, value);
                if (index < 0)
                {
                    throw new Exception("'" + value + "' is not one of " + string.Join(", ", property.enumNames));
                }
                property.enumValueIndex = index;
                break;
            case SerializedPropertyType.Color:
                property.colorValue = ParseColor(value);
                break;
            case SerializedPropertyType.Vector3:
                property.vector3Value = ParseVector3(value);
                break;
            case SerializedPropertyType.Quaternion:
                property.quaternionValue = Quaternion.Euler(ParseVector3(value));
                break;
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = LoadReference(value);
                break;
            default:
                throw new Exception("setting a " + property.propertyType + " property is not supported");
        }
        serialized.ApplyModifiedProperties();
        Append(propertyPath + ": " + before + " -> " + FormatProperty(property));
    }

    // Finds the method by name and argument count, also private methods and methods of base classes.
    private static void CallMethod(Component target, string methodName, string[] values)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        System.Reflection.MethodInfo method = null;
        for (Type type = target.GetType(); type != null && method == null; type = type.BaseType)
        {
            method = type.GetMethods(flags | System.Reflection.BindingFlags.DeclaredOnly)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == values.Length);
        }
        if (method == null)
        {
            throw new Exception(target.GetType().Name + " has no method '" + methodName + "' with " + values.Length + " arguments");
        }

        System.Reflection.ParameterInfo[] parameters = method.GetParameters();
        var arguments = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            arguments[i] = ParseArgument(values[i], parameters[i].ParameterType);
        }

        object result;
        try
        {
            result = method.Invoke(target, arguments);
        }
        catch (System.Reflection.TargetInvocationException e)
        {
            throw new Exception(methodName + " threw " + e.InnerException);
        }
        Append(method.ReturnType == typeof(void) ? methodName + " done" : methodName + " -> " + FormatValue(result));
    }

    // The value "maincamera" gives the camera that renders the Game view.
    private static object ParseArgument(string value, Type type)
    {
        if (type == typeof(string)) return value;
        if (type == typeof(float)) return ParseFloat(value);
        if (type == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(bool)) return bool.Parse(value);
        if (type == typeof(Vector3)) return ParseVector3(value);
        if (type == typeof(Color)) return ParseColor(value);
        if (type.IsEnum) return Enum.Parse(type, value);
        if (type == typeof(Camera))
        {
            if (value != "maincamera")
            {
                throw new Exception("a Camera argument takes 'maincamera'");
            }
            return Camera.main;
        }
        if (typeof(GameObject).IsAssignableFrom(type)) return FindObject(value);
        if (typeof(Component).IsAssignableFrom(type)) return FindObject(value).GetComponent(type);
        if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return LoadReference(value);
        throw new Exception("an argument of type " + type.Name + " is not supported");
    }

    // A value that starts with Assets/ or Packages/ is an asset path. Any other value is a shader name.
    private static UnityEngine.Object LoadReference(string value)
    {
        if (value == "null") return null;

        UnityEngine.Object reference = value.StartsWith("Assets/") || value.StartsWith("Packages/")
            ? AssetDatabase.LoadMainAssetAtPath(value)
            : Shader.Find(value);
        if (reference == null)
        {
            throw new Exception("no asset or shader '" + value + "'");
        }
        return reference;
    }

    private static Material LoadMaterial(string assetPath)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            throw new Exception("no material at '" + assetPath + "'");
        }
        return material;
    }

    private static void WriteMaterial(Material material)
    {
        Shader shader = material.shader;
        Append("shader: " + shader.name + (EditorUtility.IsDirty(material) ? " [unsaved changes]" : ""));
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string name = shader.GetPropertyName(i);
            Append(name + " = " + FormatMaterialValue(material, name, shader.GetPropertyType(i)));
        }
    }

    // The property "shader" takes a shader name. The material is saved to disk afterwards.
    private static void SetMaterialValue(Material material, string property, string value)
    {
        Undo.RecordObject(material, "ClaudeBridge setmaterial");
        if (property == "shader")
        {
            Shader shader = Shader.Find(value);
            if (shader == null)
            {
                throw new Exception("no shader '" + value + "'");
            }
            Append("shader: " + material.shader.name + " -> " + shader.name);
            material.shader = shader;
        }
        else
        {
            if (!material.HasProperty(property))
            {
                throw new Exception(material.shader.name + " has no property '" + property + "'");
            }
            int index = material.shader.FindPropertyIndex(property);
            UnityEngine.Rendering.ShaderPropertyType type = material.shader.GetPropertyType(index);
            string before = FormatMaterialValue(material, property, type);
            switch (type)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    material.SetColor(property, ParseColor(value));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    material.SetTexture(property, (Texture)LoadReference(value));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    material.SetVector(property, ParseVector4(value));
                    break;
                default:
                    material.SetFloat(property, ParseFloat(value));
                    break;
            }
            Append(property + ": " + before + " -> " + FormatMaterialValue(material, property, type));
        }
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
    }

    private static string FormatMaterialValue(Material material, string name, UnityEngine.Rendering.ShaderPropertyType type)
    {
        switch (type)
        {
            case UnityEngine.Rendering.ShaderPropertyType.Color:
                return material.GetColor(name).ToString();
            case UnityEngine.Rendering.ShaderPropertyType.Vector:
                return material.GetVector(name).ToString();
            case UnityEngine.Rendering.ShaderPropertyType.Texture:
                Texture texture = material.GetTexture(name);
                return texture == null ? "null" : AssetDatabase.GetAssetPath(texture);
            default:
                return material.GetFloat(name).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string FormatProperty(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
                return property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Float:
                return property.floatValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean:
                return property.boolValue.ToString();
            case SerializedPropertyType.String:
                return property.stringValue;
            case SerializedPropertyType.Enum:
                return property.enumValueIndex >= 0 && property.enumValueIndex < property.enumNames.Length
                    ? property.enumNames[property.enumValueIndex]
                    : property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Color:
                return property.colorValue.ToString();
            case SerializedPropertyType.Vector3:
                return Format(property.vector3Value);
            case SerializedPropertyType.Quaternion:
                return Format(property.quaternionValue.eulerAngles);
            case SerializedPropertyType.ObjectReference:
                UnityEngine.Object reference = property.objectReferenceValue;
                if (reference == null) return "null";
                string assetPath = AssetDatabase.GetAssetPath(reference);
                return reference.name + (assetPath == "" ? "" : " (" + assetPath + ")");
            case SerializedPropertyType.ArraySize:
                return property.intValue.ToString(CultureInfo.InvariantCulture);
            default:
                return "(" + property.propertyType + ")";
        }
    }

    private static float[] ParseFloats(string text, int count)
    {
        float[] values = text.Split(',').Select(part => ParseFloat(part.Trim())).ToArray();
        if (values.Length != count)
        {
            throw new Exception("'" + text + "' needs " + count + " comma-separated numbers");
        }
        return values;
    }

    private static Color ParseColor(string text)
    {
        float[] v = ParseFloats(text, 4);
        return new Color(v[0], v[1], v[2], v[3]);
    }

    private static Vector3 ParseVector3(string text)
    {
        float[] v = ParseFloats(text, 3);
        return new Vector3(v[0], v[1], v[2]);
    }

    private static Vector4 ParseVector4(string text)
    {
        float[] v = ParseFloats(text, 4);
        return new Vector4(v[0], v[1], v[2], v[3]);
    }

    // A path that ends with #n uses the n-th object with that path, counted from 1.
    private static GameObject FindObject(string path)
    {
        int matchNumber = 1;
        int hash = path.LastIndexOf('#');
        if (hash >= 0 && int.TryParse(path.Substring(hash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            matchNumber = parsed;
            path = path.Substring(0, hash);
        }

        bool byName = path.IndexOf('/') < 0;
        var matches = new List<GameObject>();
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (EditorUtility.IsPersistent(t) || !t.gameObject.scene.IsValid()) continue;

            if (byName ? t.name == path : GetPath(t) == path)
            {
                matches.Add(t.gameObject);
            }
        }

        if (matches.Count == 0)
        {
            throw new Exception("no object at '" + path + "'");
        }
        if (matchNumber > matches.Count)
        {
            throw new Exception("only " + matches.Count + " objects match '" + path + "'");
        }
        if (matches.Count > 1)
        {
            Append("note: " + matches.Count + " objects match '" + path + "', using number " + matchNumber + " (add #n to choose)");
        }
        return matches[matchNumber - 1];
    }

    private static Bounds GetBounds(GameObject target, out bool hasRenderers)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        hasRenderers = renderers.Length > 0;
        if (!hasRenderers) return new Bounds(target.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private static void RequirePlayMode()
    {
        if (!Application.isPlaying)
        {
            throw new Exception("this command needs play mode");
        }
    }

    private static string Arg(string[] args, int index)
    {
        if (index >= args.Length)
        {
            throw new Exception("'" + args[0] + "' needs argument " + index);
        }
        return args[index];
    }

    // The machine uses a Swedish locale, where float.Parse and ToString use a decimal comma.
    private static float ParseFloat(string text)
    {
        return float.Parse(text, CultureInfo.InvariantCulture);
    }

    private static string Format(Vector3 v)
    {
        return string.Format(CultureInfo.InvariantCulture, "({0:F3}, {1:F3}, {2:F3})", v.x, v.y, v.z);
    }

    private static string FormatValue(object value)
    {
        if (value == null) return "null";
        if (value is UnityEngine.Object unityObject) return unityObject == null ? "null (destroyed)" : unityObject.name + " (" + unityObject.GetType().Name + ")";
        if (value is Vector3 vector) return Format(vector);
        if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
        if (value is Array array)
        {
            var parts = new List<string>();
            foreach (object element in array)
            {
                parts.Add(FormatValue(element));
            }
            return "[" + string.Join(", ", parts) + "]";
        }
        return value.ToString();
    }

    private static string Now(double offsetSeconds)
    {
        return (EditorApplication.timeSinceStartup + offsetSeconds).ToString("R", CultureInfo.InvariantCulture);
    }

    private static string ImagePath(string name)
    {
        return Path.Combine(BridgeDir, name + ".png");
    }

    private static void Append(string text)
    {
        File.AppendAllText(OutputFile, text + "\n");
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        var line = new StringBuilder("[" + type + "] " + condition);
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            line.Append("\n" + stackTrace.TrimEnd());
        }
        WriteConsoleLine(line.ToString());
    }

    private static void OnAssemblyCompiled(string assembly, CompilerMessage[] messages)
    {
        foreach (CompilerMessage message in messages)
        {
            if (message.type != CompilerMessageType.Error) continue;
            WriteConsoleLine("[compile error] " + message.file + "(" + message.line + "): " + message.message);
        }
    }

    private static void WriteConsoleLine(string text)
    {
        string stamped = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + text + "\n";
        lock (ConsoleLock)
        {
            File.AppendAllText(ConsoleFile, stamped);
        }
    }
}
