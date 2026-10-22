#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Reflection;

public class Iamres_Flags_Editor : ShaderGUI
{
    private bool showIntensityGroup = false;
    private bool showPupilGroup = false;
    private string[] typeOptions = { "USA", "UA", "PL", "RU", "JP", "CN", "UK", "DE", "SW", "BG"};
    private int selectedType = 0; 
    private Texture2D MakeTex(int width, int height, Color color) {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result; }
    
    void DrawBanner(string title, string buttonText, string url)
{
    Color originalColor = GUI.color;

    Color bannerColor = new Color(0.15f, 0.15f, 0.15f);         
    Color borderColor = new Color(0.05f, 0.05f, 0.05f);         
    GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
    {
        alignment = TextAnchor.MiddleCenter,
        fontSize = 12,
        fixedHeight = 25,
        fontStyle = FontStyle.Bold,
        normal = { textColor = Color.white }
    };
    GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButton)
    {
        fontStyle = FontStyle.Bold,
        fontSize = 12,
        alignment = TextAnchor.MiddleRight,
        fixedHeight = 21,
        margin = new RectOffset(0, 0, 4, 0),
        padding = new RectOffset(5, 5, 5, 5)
    };
    Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
    {
        float borderWidth = 2f;
        EditorGUI.DrawRect(rect, bannerColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, borderWidth), borderColor);                            
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - borderWidth, rect.width, borderWidth), borderColor);          
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, borderWidth, rect.height), borderColor);                          
        EditorGUI.DrawRect(new Rect(rect.xMax - borderWidth, rect.y, borderWidth, rect.height), borderColor);         
        GUILayout.BeginHorizontal();
        GUILayout.Label(title.ToUpper(), titleStyle);
        GUILayout.EndHorizontal();
        if (GUILayout.Button(buttonText.ToUpper(), buttonStyle, GUILayout.Width(110)))
        {
            Application.OpenURL(url);
        }
    }
    EditorGUILayout.EndHorizontal();
    GUI.color = originalColor;
}
    
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material material = (Material)materialEditor.target;
        Shader shader = material.shader;
        GUIStyle boxStyle1 = new GUIStyle(GUI.skin.box);
        boxStyle1.border = new RectOffset(5, 5, 5, 5); 
        boxStyle1.margin = new RectOffset(10, 10, 10, 10); 
        boxStyle1.padding = new RectOffset(5, 5, 5, 10); 
        boxStyle1.normal.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f)); 
        GUIStyle boxStyle2 = new GUIStyle(GUI.skin.box);
        boxStyle2.border = new RectOffset(5, 5, 5, 5); 
        boxStyle2.margin = new RectOffset(10, 10, 10, 10); 
        boxStyle2.padding = new RectOffset(20, 5, 5, 10); 
        boxStyle2.normal.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f)); 
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);      
        headerStyle.alignment = TextAnchor.MiddleCenter; 
        headerStyle.fontSize = 13; 
        string bannerText1 = "IAMRES'S FLAGS EYE SHADER"; 
        string bannerText2 = "GUMROAD"; 
        string bannerText3 = "BOOTH"; 
        DrawBanner(bannerText1, "Open Discord", "https://discord.gg/j63fMfSxN5"); 
        DrawBanner(bannerText2, "Open Gumroad", "https://iamres.gumroad.com");
        DrawBanner(bannerText3, "Open Booth", "https://iamres.booth.pm");
        Rect rect1 = EditorGUILayout.BeginVertical(boxStyle1); 
        Handles.DrawSolidRectangleWithOutline(rect1, new Color(0, 0, 0, 0), Color.black); 
        GUILayout.Label("MAIN SETTINGS", headerStyle); 
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.Space(15); 
        materialEditor.ShaderProperty(FindProperty("Vignette", properties), "Vignette");
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Space(15); 
        materialEditor.ShaderProperty(FindProperty("Alpha", properties), "Alpha");
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Space(15);
        showPupilGroup = EditorGUILayout.Foldout(showPupilGroup, "Pupil Settings", true);
        GUILayout.EndHorizontal();
        if (showPupilGroup) {
            EditorGUI.indentLevel++;
            GUILayout.Space(5);
            materialEditor.ShaderProperty(FindProperty("Pupil_Size", properties), "Pupil Size");
            GUILayout.Space(5);
            materialEditor.ShaderProperty(FindProperty("Pupil_Form", properties), "Pupil Form");
            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mono Pupil", GUILayout.Width(100));
            MaterialProperty shakeProp = FindProperty("Mono_Pupil", properties);
            bool isShakeEnabled = shakeProp.floatValue == 1;
            GUILayout.FlexibleSpace(); 
            if (GUILayout.Button(isShakeEnabled ? "ON" : "OFF", GUILayout.Width(50))) { shakeProp.floatValue = isShakeEnabled ? 0 : 1; } 
            GUILayout.FlexibleSpace(); 
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--; }
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Space(15); 
        materialEditor.ShaderProperty(FindProperty("Textile_Size", properties), "Textile Size");
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Space(15); 
        materialEditor.ShaderProperty(FindProperty("Wind_Speed", properties), "Wind Speed");
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        EditorGUILayout.EndVertical();
        Rect rect2 = EditorGUILayout.BeginVertical(boxStyle1); 
        Handles.DrawSolidRectangleWithOutline(rect2, new Color(0, 0, 0, 0), Color.black);
        GUILayout.Label("COUNTRY", headerStyle); 
        EditorGUI.indentLevel++;
        MaterialProperty typeProperty = FindProperty("Type", properties);
        selectedType = (int)typeProperty.floatValue;
        int columns = 5; 
        selectedType = GUILayout.SelectionGrid(selectedType, typeOptions, columns, GUILayout.Height(50));
        typeProperty.floatValue = selectedType;
        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        Rect rect3 = EditorGUILayout.BeginVertical(boxStyle2);
        Handles.DrawSolidRectangleWithOutline(rect3, new Color(0, 0, 0, 0), Color.black); 
        GUILayout.Label("OTHER SETTINGS", headerStyle); 
        GUILayout.Space(10);
        materialEditor.ShaderProperty(FindProperty("TileOffset", properties), "TileOffset");
        GUILayout.Space(-15);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Reflection", GUILayout.Width(100));
        MaterialProperty ReflectionProp = FindProperty("Reflection", properties);
        bool RefONOFF = ReflectionProp.floatValue == 1;
        GUILayout.FlexibleSpace(); 
        if (GUILayout.Button(RefONOFF ? "ON" : "OFF", GUILayout.Width(50))) { ReflectionProp.floatValue = RefONOFF ? 0 : 1; }
        GUILayout.FlexibleSpace(); 
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(2);
        materialEditor.ShaderProperty(FindProperty("Alpha_Ref", properties), "Reflection Alpha ");   
        GUILayout.Space(5);
        materialEditor.ShaderProperty(FindProperty("Ref_Col", properties), "Reflection Color");
        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal(); 
        EditorGUILayout.LabelField("Improve Parallax", GUILayout.Width(100));
        MaterialProperty ParProp = FindProperty("Improved_Parallax", properties);
        bool Par = ParProp.floatValue == 1;
        GUILayout.FlexibleSpace(); 
        if (GUILayout.Button(Par ? "ON" : "OFF", GUILayout.Width(50))) { ParProp.floatValue = Par ? 0 : 1; }
        GUILayout.FlexibleSpace(); 
        EditorGUILayout.EndHorizontal();
        showIntensityGroup = EditorGUILayout.Foldout(showIntensityGroup, "Parallax Settings", true);
        if (showIntensityGroup) {
            EditorGUI.indentLevel++;
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.Space(-15); 
            materialEditor.ShaderProperty(FindProperty("Intensity_Front", properties), "Intensity Front");
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.Space(-15); 
            materialEditor.ShaderProperty(FindProperty("Intensity_Back", properties), "Intensity Back");
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.Space(-15); 
            materialEditor.ShaderProperty(FindProperty("Intensity_BackFront", properties), "Intensity BackFront");
            GUILayout.EndHorizontal();
            EditorGUI.indentLevel--; }
        GUILayout.Space(10);
        GUILayout.Label("Only for shaders used for ball eyes", EditorStyles.boldLabel);
        GUILayout.Space(5);
        materialEditor.ShaderProperty(FindProperty("Ambient_Col", properties), "Ambient_Col");
        GUILayout.Space(5);
        EditorGUILayout.EndVertical();
        Rect rect4 = EditorGUILayout.BeginVertical(boxStyle2); 
        Handles.DrawSolidRectangleWithOutline(rect4, new Color(0, 0, 0, 0), Color.black); 
        GUILayout.Label("INFO", headerStyle); 
        GUILayout.Label("Shader by: Iamres", EditorStyles.boldLabel);
        GUILayout.Label("", EditorStyles.boldLabel);
        GUILayout.Label("In my opinion, the optimal value for:", EditorStyles.boldLabel);
        GUILayout.Label("Intensity Front - [0.6 ± 0.2]", EditorStyles.boldLabel);
        GUILayout.Label("Intensity Back  - [0.6 ± 0.2]", EditorStyles.boldLabel);
        GUILayout.Label("", EditorStyles.boldLabel);
        GUILayout.Label("If you have any issues with shaders or have any requests", EditorStyles.boldLabel);
        GUILayout.Label("or suggestions, you can write to me on Discord", EditorStyles.boldLabel);
        GUILayout.Label("", EditorStyles.boldLabel);
        GUILayout.Label("If parallax effect doesn't work, you must open 'model.fbx'", EditorStyles.boldLabel);
        GUILayout.Label("(for exemple 'Assets/Iamres/Models/model.fbx')", EditorStyles.boldLabel);
        GUILayout.Label("and find Normals and Tangents properties, then set value", EditorStyles.boldLabel);
        GUILayout.Label("Normals to 'Calculate'", EditorStyles.boldLabel);
        GUILayout.Label("Tangents to 'Calculate Legacy' or other", EditorStyles.boldLabel);
        GUILayout.Label("", EditorStyles.boldLabel);
        GUILayout.Label("Thanks for the help from my mentor - Dopestuff", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        Rect rect5 = GUILayoutUtility.GetRect(new GUIContent("Default Values"), GUI.skin.button); 
        Handles.DrawSolidRectangleWithOutline(rect5, new Color(0, 0, 0, 0), Color.red);
        if (GUI.Button(rect5, "Default Values")) { 
            material.SetFloat("Vignette", 0.3f);
            material.SetFloat("Alpha", 1f);
                material.SetFloat("Pupil_Size", 0.35f);
                material.SetFloat("Pupil_Form", 0.545f);
                material.SetFloat("Mono_Pupil", 1f);
            material.SetFloat("Textile_Size", 0.55f);
            material.SetFloat("Wind_Speed", 3.0f);
            material.SetVector("TileOffset", new Vector4(1f, 1f, 0f, 0f));
            material.SetFloat("Reflection", 0f);
            material.SetFloat("Alpha_Ref", 0.5f);
            material.SetColor("Ref_Col", new Color(1f, 1f, 1f, 0f));
                material.SetFloat("Intensity_Back", 1.0f);
                material.SetFloat("Intensity_Front", 1.0f);
                material.SetFloat("Intensity_BackFront", 1.0f);
            material.SetColor("Ambient_Col", new Color(0f, 0f, 0f, 0f));
            EditorUtility.SetDirty(material); }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }   
}
#endif