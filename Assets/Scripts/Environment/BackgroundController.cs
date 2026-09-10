using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-layer parallax scroller for the endless-runner background.
///
/// Movement is computed from the <b>actual per-frame movement of the rendered
/// camera</b> (not an absolute <c>cameraX * k</c>), so it never fights the
/// camera-follow rig and stays correct under any camera damping. Each frame the
/// layer moves with the camera by <see cref="parallaxEffect"/> of the camera's
/// delta; the remaining <c>(1 - parallaxEffect)</c> is the leftward scroll seen
/// on screen. Runs after Cinemachine (high execution order) so it reads the
/// camera's final position for the frame.
///
/// <see cref="parallaxEffect"/>: 1 = moves with the camera (static, farthest);
/// 0 = locked to the world (scrolls fastest, nearest / gameplay plane).
///
/// On <see cref="Start"/> the layer builds a strip of identical copies wide
/// enough to always cover the camera (extra copies are cloned from the first
/// child, or from an existing scene child, as needed) and every copy is synced
/// to the parent's sprite / sorting so the layer's art only has to be set once.
/// The strip is re-centred on the camera by whole-tile snaps, which are
/// invisible because the copies are identical.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(10000)]
public class BackgroundController : MonoBehaviour
{
    [Tooltip("Transform to parallax against. Leave empty to use the rendered main camera.")]
    public Transform cam;

    [Range(0f, 1f)]
    [Tooltip("1 = moves with the camera (static, far). 0 = locked to the world (scrolls fastest, near).")]
    public float parallaxEffect = 0.3f;

    [Tooltip("Repeat this layer horizontally so it never runs out.")]
    public bool infiniteHorizontal = true;

    [Tooltip("World width the copy strip must always cover (generous margin around the view).")]
    public float coverWidth = 60f;

    SpriteRenderer spriteRenderer;
    float tileWidth;
    float baseY;
    float lastCamX;
    bool primed;

    void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

    void Start()
    {
        ResolveCam();
        baseY = transform.position.y;
        tileWidth = spriteRenderer.bounds.size.x;

        if (infiniteHorizontal && tileWidth > 0f)
            BuildStrip();

        Prime();
    }

    void OnEnable() => Prime();

    void ResolveCam()
    {
        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;
    }

    void Prime()
    {
        ResolveCam();
        if (cam != null)
        {
            lastCamX = cam.position.x;
            primed = true;
        }
    }

    /// <summary>
    /// Place identical copies at +/- 1..N tile-widths (N chosen from
    /// <see cref="coverWidth"/>), cloning more if the scene didn't provide enough,
    /// and sync each copy's sprite / sorting to the parent.
    /// </summary>
    void BuildStrip()
    {
        int perSide = Mathf.Max(1, Mathf.CeilToInt(coverWidth / tileWidth));
        int needed = perSide * 2;

        var copies = new List<Transform>();
        foreach (Transform c in transform) copies.Add(c);

        Transform template = copies.Count > 0 ? copies[0] : null;
        while (copies.Count < needed)
        {
            GameObject clone;
            if (template != null)
            {
                clone = Instantiate(template.gameObject, transform);
            }
            else
            {
                clone = new GameObject(name + "_copy");
                clone.transform.SetParent(transform, false);
                clone.AddComponent<SpriteRenderer>();
            }
            var strayBc = clone.GetComponent<BackgroundController>();
            if (strayBc != null) Destroy(strayBc);
            copies.Add(clone.transform);
        }

        int idx = 0;
        for (int i = -perSide; i <= perSide; i++)
        {
            if (i == 0) continue; // the parent itself sits at offset 0
            if (idx >= copies.Count) break;
            SyncCopy(copies[idx], i * tileWidth);
            idx++;
        }
        for (; idx < copies.Count; idx++)
            copies[idx].gameObject.SetActive(false);
    }

    void SyncCopy(Transform copy, float offsetX)
    {
        copy.position = new Vector3(transform.position.x + offsetX, transform.position.y, copy.position.z);
        copy.localScale = Vector3.one;
        copy.localRotation = Quaternion.identity;
        copy.gameObject.SetActive(true);

        var sr = copy.GetComponent<SpriteRenderer>();
        if (sr != null && spriteRenderer != null)
        {
            sr.sprite = spriteRenderer.sprite;
            sr.sortingLayerID = spriteRenderer.sortingLayerID;
            sr.sortingOrder = spriteRenderer.sortingOrder;
            sr.color = spriteRenderer.color;
            sr.flipX = spriteRenderer.flipX;
            sr.flipY = spriteRenderer.flipY;
            sr.drawMode = spriteRenderer.drawMode;
        }
    }

    void LateUpdate()
    {
        if (cam == null) { ResolveCam(); return; }
        if (!primed) { lastCamX = cam.position.x; primed = true; return; }

        float camX = cam.position.x;
        float delta = camX - lastCamX;
        lastCamX = camX;

        Vector3 p = transform.position;
        p.x += delta * parallaxEffect;
        p.y = baseY;
        transform.position = p;

        if (!infiniteHorizontal || tileWidth <= 0f) return;

        // Keep the strip centred on the camera; snap by whole tiles (invisible,
        // since the copies are identical).
        float rel = transform.position.x - camX;
        if (rel > 0.5f * tileWidth) transform.position += Vector3.left * tileWidth;
        else if (rel < -0.5f * tileWidth) transform.position += Vector3.right * tileWidth;
    }
}
