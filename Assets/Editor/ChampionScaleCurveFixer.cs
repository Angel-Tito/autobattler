// Assets/Editor/ChampionScaleCurveFixer.cs
using UnityEngine;
using UnityEditor;

public class ChampionScaleCurveFixer : AssetPostprocessor
{

    void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        int eliminadas = 0;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.propertyName.StartsWith("m_LocalScale"))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
                eliminadas++;
            }
        }

        if (eliminadas > 0)
            Debug.Log($"[ScaleFixer] {clip.name}: {eliminadas} curvas de escala eliminadas.");
    }
}