using UnityEngine;

public class Wiper : MonoBehaviour
{
    public float speed = 120f;
    public float maxAngle = 45f;

    private bool reverse;

    void Update()
    {
        float z = transform.localEulerAngles.z;

        if (z > 180)
            z -= 360;

        if (!reverse)
        {
            z += speed * Time.deltaTime;

            if (z >= maxAngle)
            {
                z = maxAngle;
                reverse = true;
            }
        }
        else
        {
            z -= speed * Time.deltaTime;

            if (z <= -maxAngle)
            {
                z = -maxAngle;
                reverse = false;
            }
        }

        transform.localRotation = Quaternion.Euler(0, 0, z);
    }
}