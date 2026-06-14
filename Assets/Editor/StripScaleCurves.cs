// Assets/Editor/StripScaleCurves.cs
// Menú: Tools > Strip Scale Curves from Champions

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class StripScaleCurves : EditorWindow
{
    private RuntimeAnimatorController _targetController;
    private Vector2 _scroll;
    private List<string> _log = new List<string>();

    [MenuItem("Tools/Strip Scale Curves from Champions")]
    static void Open() => GetWindow<StripScaleCurves>("Strip Scale Curves");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Elimina curvas m_LocalScale de los clips de animación", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();

        _targetController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
            "Animator Controller", _targetController, typeof(RuntimeAnimatorController), false);

        EditorGUILayout.Space();

        GUI.enabled = _targetController != null;
        if (GUILayout.Button("Procesar Controller seleccionado", GUILayout.Height(35)))
            ProcessController(_targetController);
        GUI.enabled = true;

        EditorGUILayout.Space();
        if (GUILayout.Button("Procesar TODOS los controllers del proyecto", GUILayout.Height(35)))
            ProcessAll();

        // Log
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Log:", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
        foreach (var line in _log)
            EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
    }

    void ProcessAll()
    {
        _log.Clear();
        string[] guids = AssetDatabase.FindAssets("t:AnimatorController");
        int totalClips = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            if (controller != null)
                totalClips += ProcessController(controller, silent: true);
        }

        AssetDatabase.SaveAssets();
        _log.Insert(0, $"✅ Listo. {totalClips} clips modificados en {guids.Length} controllers.");
        Repaint();
    }

    int ProcessController(RuntimeAnimatorController controller, bool silent = false)
    {
        if (controller == null) return 0;

        AnimationClip[] clips = controller.animationClips;
        int modified = 0;

        foreach (AnimationClip clip in clips)
        {
            if (clip == null || clip.isHumanMotion) continue; // Saltar Humanoid (usan muscle curves)

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            bool clipModified = false;

            foreach (var binding in bindings)
            {
                // Eliminar SOLO curvas de escala (LocalScale.x/y/z)
                if (binding.propertyName.StartsWith("m_LocalScale"))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                    _log.Add($"  ✂ [{clip.name}] eliminada curva: {binding.propertyName} en '{binding.path}'");
                    clipModified = true;
                }
            }

            if (clipModified)
            {
                EditorUtility.SetDirty(clip);
                modified++;
            }
        }

        if (!silent)
        {
            AssetDatabase.SaveAssets();
            _log.Add($"✅ {controller.name}: {modified} clips modificados.");
            Repaint();
        }

        return modified;
    }
}