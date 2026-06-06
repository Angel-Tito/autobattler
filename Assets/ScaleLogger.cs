using UnityEngine;

public class ScaleLogger : MonoBehaviour {
    public Transform target;
    void LateUpdate() {
        if(target != null) {
            Debug.Log("SCALE [" + target.name + "]: Local=" + target.localScale + " Lossy=" + target.lossyScale);
        }
    }
}
