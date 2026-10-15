
using UnityEngine;

public class Simple2DMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector3 movement = new Vector3(x, y, 0).normalized;

        transform.position += movement * moveSpeed * Time.deltaTime;
    }
}
