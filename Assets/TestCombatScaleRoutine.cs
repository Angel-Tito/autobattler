using UnityEngine;

public class TestCombatScaleRoutine : MonoBehaviour {
    void Start() {
        StartCoroutine(TestRoutine());
    }

    System.Collections.IEnumerator TestRoutine() {
        var cm = Object.FindObjectOfType<CombatManager>();
        var campeon = Object.FindObjectOfType<CampeonCombat>();
        if(cm == null || campeon == null) yield break;

        Debug.Log("PRE-COMBAT: Campeon Local=" + campeon.transform.localScale + " Lossy=" + campeon.transform.lossyScale);
        var anim = campeon.GetComponentInChildren<Animator>();
        if(anim != null) {
            Debug.Log("PRE-COMBAT: Animator Local=" + anim.transform.localScale + " Lossy=" + anim.transform.lossyScale);
        }

        cm.IniciarCombate();
        yield return new WaitForSeconds(2.0f); // wait for fade to finish

        Debug.Log("POST-COMBAT: Campeon Local=" + campeon.transform.localScale + " Lossy=" + campeon.transform.lossyScale);
        if(anim != null) {
            Debug.Log("POST-COMBAT: Animator Local=" + anim.transform.localScale + " Lossy=" + anim.transform.lossyScale);
            var renderers = anim.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach(var smr in renderers) {
                if(smr.rootBone) {
                    Debug.Log("POST-COMBAT: SMR rootBone " + smr.rootBone.name + " scale " + smr.rootBone.localScale + " lossy " + smr.rootBone.lossyScale);
                }
            }
        }
    }
}
