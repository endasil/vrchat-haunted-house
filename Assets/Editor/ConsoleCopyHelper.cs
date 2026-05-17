using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class ConsoleCopyHelper : EditorWindow
{
    static readonly Type _consoleWindowType;
    static readonly FieldInfo _activeEntryIndexField;
    static readonly FieldInfo _activeTextField;
    static readonly string _initError;

    static ConsoleCopyHelper()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            if (_consoleWindowType == null)
                _consoleWindowType = asm.GetType("UnityEditor.ConsoleWindow");

        _activeEntryIndexField = _consoleWindowType?.GetField("m_LastActiveEntryIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        _activeTextField       = _consoleWindowType?.GetField("m_ActiveText",           BindingFlags.Instance | BindingFlags.NonPublic);

        if (_consoleWindowType == null || _activeEntryIndexField == null || _activeTextField == null)
        {
            _initError = "ConsoleCopyHelper: reflection failed — missing: " +
                (_consoleWindowType == null      ? "ConsoleWindow(type) " : "") +
                (_activeEntryIndexField == null  ? "m_LastActiveEntryIndex " : "") +
                (_activeTextField == null        ? "m_ActiveText" : "");
            Debug.LogError(_initError);
        }
    }

    [MenuItem("Window/Console Copy Helper")]
    static void Open() => GetWindow<ConsoleCopyHelper>("Copy Log");

    Vector2 _scroll;

    void OnGUI()
    {
        if (_initError != null)
        {
            EditorGUILayout.HelpBox(_initError, MessageType.Error);
            return;
        }

        var consoleWindow = GetWindow(_consoleWindowType, false, "Console", false);
        if (consoleWindow == null)
        {
            EditorGUILayout.LabelField("Console window not open.");
            return;
        }

        int row = (int)_activeEntryIndexField.GetValue(consoleWindow);
        if (row < 0)
        {
            EditorGUILayout.LabelField("No entry selected in Console.");
            return;
        }

        string text = (string)_activeTextField.GetValue(consoleWindow);

        if (GUILayout.Button("Copy to Clipboard"))
            EditorGUIUtility.systemCopyBuffer = text;

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.TextArea(text, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void OnInspectorUpdate() => Repaint();
}
