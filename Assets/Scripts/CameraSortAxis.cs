using UnityEngine;

public class CameraSortAxis : MonoBehaviour
{
    void Awake()
    {
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = new Vector3(0, -1, 0);
            Debug.Log("CameraSortAxis: set to Custom Y axis");
        }
    }
}
