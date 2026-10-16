using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

[CustomEditor(typeof(SlidePlayer))]
public class SlidePlayerEditor : Editor
{
    private const int MaxSlides = 5000;
    private const int FirstSlideNumber = 1;

    private bool _showUrls;

    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
        {
            return;
        }

        SlidePlayer player = (SlidePlayer)target;

        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script", "slides");

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        string problem = Validate(player);
        if (problem != null)
        {
            EditorGUILayout.HelpBox(problem, MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(problem != null))
        {
            if (GUILayout.Button("Generate URLs", GUILayout.Height(28)))
            {
                Generate(player);
            }
        }

        EditorGUILayout.Space();
        DrawCurrentUrls(player);
    }

    private static string Validate(SlidePlayer player)
    {
        if (string.IsNullOrWhiteSpace(player.baseUrl))
        {
            return "Set a base URL first, for example https://example.com/slides";
        }

        if (!player.baseUrl.StartsWith("http://") && !player.baseUrl.StartsWith("https://"))
        {
            return "The base URL has to start with http:// or https://";
        }

        if (player.slideCount < 1)
        {
            return "Slide count has to be at least 1.";
        }

        if (player.slideCount > MaxSlides)
        {
            return "Slide count is capped at " + MaxSlides + ".";
        }

        return null;
    }

    private static void Generate(SlidePlayer player)
    {
        string folder = player.baseUrl.Trim().TrimEnd('/');
        string prefix = player.filePrefix == null ? "" : player.filePrefix.Trim();
        string extension = player.fileExtension;

        VRCUrl[] urls = new VRCUrl[player.slideCount];
        for (int i = 0; i < urls.Length; i++)
        {
            urls[i] = new VRCUrl(folder + "/" + prefix + (FirstSlideNumber + i) + extension);
        }

        Undo.RecordObject(player, "Generate slide URLs");
        player.slides = urls;
        EditorUtility.SetDirty(player);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
    }

    private void DrawCurrentUrls(SlidePlayer player)
    {
        int count = player.slides == null ? 0 : player.slides.Length;

        if (count == 0)
        {
            EditorGUILayout.LabelField("No URLs generated yet.");
            return;
        }

        EditorGUILayout.LabelField(count + " URLs generated");
        EditorGUILayout.LabelField("First", player.slides[0].Get());
        EditorGUILayout.LabelField("Last", player.slides[count - 1].Get());

        _showUrls = EditorGUILayout.Foldout(_showUrls, "Show all URLs");
        if (!_showUrls)
        {
            return;
        }

        EditorGUILayout.HelpBox("Read only. Pressing Generate URLs replaces the whole list.", MessageType.None);
        using (new EditorGUI.DisabledScope(true))
        {
            for (int i = 0; i < count; i++)
            {
                EditorGUILayout.TextField((FirstSlideNumber + i).ToString(), player.slides[i].Get());
            }
        }
    }
}
