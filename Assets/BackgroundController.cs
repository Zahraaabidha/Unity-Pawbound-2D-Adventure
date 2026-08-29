using UnityEngine;

public class BackgroundController : MonoBehaviour
{
    public Transform cam;
    [Range(0f, 1f)]
    public float parallaxEffect = 0.3f;

    private float startPos;
    private float length;

    void Start()
    {
        startPos = transform.position.x;
        length = GetComponent<SpriteRenderer>().bounds.size.x;
    }

    void LateUpdate()
    {
        float camX = cam.position.x;
        float distance = camX * parallaxEffect;
        float movement = camX * (1 - parallaxEffect);

        transform.position = new Vector3(startPos + distance, transform.position.y, transform.position.z);

        if (movement > startPos + length)
        {
            startPos += length;
        }
        else if (movement < startPos - length)
        {
            startPos -= length;
        }
    }
}
