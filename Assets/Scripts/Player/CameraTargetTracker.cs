using UnityEngine;

/// <summary>
/// Drives a dummy transform that the Cinemachine camera follows. It tracks the
/// player's X (with a configurable look-ahead) but keeps a fixed Y, so jumping
/// never makes the camera bob vertically.
///
/// Runs early (before Cinemachine) so the camera reads a fresh target each frame.
/// </summary>
[DefaultExecutionOrder(-100)]
public class CameraTargetTracker : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float lookAhead = 2.5f;
    [SerializeField] float horizontalSmoothing = 10f;
    [SerializeField] bool lockYToStart = true;
    [SerializeField] float fixedY = 0f;

    void Start()
    {
        if (lockYToStart) fixedY = transform.position.y;

        if (target == null)
        {
            var knight = Object.FindFirstObjectByType<KnightController>();
            if (knight != null) target = knight.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        float desiredX = target.position.x + lookAhead;
        float t = 1f - Mathf.Exp(-horizontalSmoothing * Time.deltaTime);
        float x = Mathf.Lerp(transform.position.x, desiredX, t);

        transform.position = new Vector3(x, fixedY, transform.position.z);
    }

    public void SetTarget(Transform t) => target = t;
}
