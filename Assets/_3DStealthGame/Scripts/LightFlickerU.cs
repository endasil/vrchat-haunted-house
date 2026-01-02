using UnityEngine;
using UdonSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace StealthGame
{
    public enum FlickerMode
    {
        Random,
        AnimationCurve
    }

    public class LightFlickerU : UdonSharpBehaviour
    {
       
        public Light flickeringLight;
        public Renderer flickeringRenderer;
        public FlickerMode flickerMode;
        public float lightIntensityMin = 1.25f;
        public float lightIntensityMax = 2.25f;
        public float flickerDuration = 0.075f;
        public AnimationCurve intensityCurve;

        Material m_FlickeringMaterial;
        Color m_EmissionColor;
        float m_Timer;
        float m_FlickerLightIntensity;
        // Removed static here since VR Chat does not support it. Slightly increases memory usage and a theoretical
        // increase in load time since it has to be once for every flickering light instead of just once for the whole
        // world, but should not have any meaningful impact.
        readonly int k_EmissionColorID = Shader.PropertyToID(k_EmissiveColorName);

        const string k_EmissiveColorName = "_EmissionColor";
        const string k_EmissionName = "_Emission";
        const float k_LightIntensityToEmission = 2f / 3f;
        public float curveDuration = 0;
        public float curveStartTime = 0f; 

        void Start()
        {
            m_FlickeringMaterial = flickeringRenderer.material;
            m_FlickeringMaterial.EnableKeyword(k_EmissionName);
            m_EmissionColor = m_FlickeringMaterial.GetColor(k_EmissionColorID);
        }

        // Disabled because flickering lights are annoying.
        //void Update()
        //{
        //    m_Timer += Time.deltaTime;

        //    if (flickerMode == FlickerMode.Random)
        //    {
        //        if (m_Timer >= flickerDuration)
        //        {
        //            ChangeRandomFlickerLightIntensity();
        //        }
        //    }
        //    else if (flickerMode == FlickerMode.AnimationCurve)
        //    {
        //        ChangeAnimatedFlickerLightIntensity();
        //    }

        //    flickeringLight.intensity = m_FlickerLightIntensity;
        //    m_FlickeringMaterial.SetColor(k_EmissionColorID, m_EmissionColor * m_FlickerLightIntensity * k_LightIntensityToEmission);
        //}

        void ChangeRandomFlickerLightIntensity()
        {
            m_FlickerLightIntensity = Random.Range(lightIntensityMin, lightIntensityMax);

            m_Timer = 0f;
        }

        void ChangeAnimatedFlickerLightIntensity()
        {
            m_FlickerLightIntensity = intensityCurve.Evaluate(m_Timer);

            // VRChat does not support getting time information from different keyframes, so workaround was
            // to fetch the duration and startTime in the editor code whenever the inspector is accessed 
            // and update it then. This will give the correct values as long as the animation curve is updated
            // from the editor and not some code.
            if (m_Timer >= curveDuration)
                m_Timer = curveStartTime;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(LightFlickerU))]
    public class LightFlickerEditor : Editor
    {
        SerializedProperty m_ScriptProp;
        SerializedProperty m_FlickeringLightProp;
        SerializedProperty m_FlickeringRendererProp;
        SerializedProperty m_FlickerModeProp;
        SerializedProperty m_LightIntensityMinProp;
        SerializedProperty m_LightIntensityMaxProp;
        SerializedProperty m_FlickerDurationProp;
        SerializedProperty m_IntensityCurveProp;
        SerializedProperty m_CurveStartTimeProp;
        SerializedProperty m_CurveDurationProp;


        void OnEnable()
        {
            m_ScriptProp = serializedObject.FindProperty("m_Script");
            m_FlickeringLightProp = serializedObject.FindProperty("flickeringLight");
            m_FlickeringRendererProp = serializedObject.FindProperty("flickeringRenderer");
            m_FlickerModeProp = serializedObject.FindProperty("flickerMode");
            m_LightIntensityMinProp = serializedObject.FindProperty("lightIntensityMin");
            m_LightIntensityMaxProp = serializedObject.FindProperty("lightIntensityMax");
            m_FlickerDurationProp = serializedObject.FindProperty("flickerDuration");
            m_IntensityCurveProp = serializedObject.FindProperty("intensityCurve");
            m_CurveStartTimeProp = serializedObject.FindProperty("curveStartTime");
            m_CurveDurationProp = serializedObject.FindProperty("curveDuration");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.PropertyField(m_ScriptProp);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(m_FlickeringLightProp);
            EditorGUILayout.PropertyField(m_FlickeringRendererProp);
            EditorGUILayout.PropertyField(m_FlickerModeProp);

            if ((FlickerMode)m_FlickerModeProp.enumValueIndex == FlickerMode.Random)
            {
                EditorGUILayout.PropertyField(m_LightIntensityMinProp);
                EditorGUILayout.PropertyField(m_LightIntensityMaxProp);
                EditorGUILayout.PropertyField(m_FlickerDurationProp);

            }

            else if ((FlickerMode)m_FlickerModeProp.enumValueIndex == FlickerMode.AnimationCurve)
            {
                EditorGUILayout.PropertyField(m_IntensityCurveProp);
                AnimationCurve curve = m_IntensityCurveProp.animationCurveValue;
                Debug.Log("AnimationCurve");

                if (curve != null && curve.length > 0)
                {

                    m_CurveStartTimeProp.floatValue = curve.keys[0].time;
                    m_CurveDurationProp.floatValue = curve.keys[curve.length - 1].time;
                    Debug.Log($"curve len = {curve.length} start: {m_CurveStartTimeProp.floatValue} end: {m_CurveDurationProp.floatValue}");

                }
            }

            serializedObject.ApplyModifiedProperties();
        }

    }
#endif
}
