using Mirror;
using UnityEngine;

/// <summary>
/// Destroys a NetworkObject after a delay. Attach at runtime on the server.
/// </summary>
public class TimedNetworkDestroy : MonoBehaviour
{
    public float delay = 2f;

    private void Start()
    {
        if (NetworkServer.active)
            Invoke(nameof(DoDestroy), delay);
        else
            Destroy(gameObject, delay);
    }

    private void DoDestroy()
    {
        NetworkServer.Destroy(gameObject);
    }
}
