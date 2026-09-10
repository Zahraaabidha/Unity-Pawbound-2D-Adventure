using UnityEngine;

/// <summary>
/// Snaps this object's X position to a target's X (with an optional offset),
/// leaving Y and Z untouched. Used to keep the ground collider centred under the
/// Knight so the run never falls off the end of a finite collider.
/// </summary>
public class FollowerX : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float offsetX = 0f;

    void Start()
    {
        if (target == null)
        {
            var knight = Object.FindFirstObjectByType<KnightController>();
            if (knight != null) target = knight.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;
        var p = transform.position;
        p.x = target.position.x + offsetX;
        transform.position = p;
    }

    public void SetTarget(Transform t) => target = t;
}
