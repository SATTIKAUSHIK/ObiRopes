using UnityEngine;

public class RotationConstraint : MonoBehaviour
{
    void Update()
    {
        // Constrain movement to the current position
        Vector3 position = transform.position;
        transform.position = new Vector3(position.x, position.y, position.z);

        // Constrain rotation to only affect the Z axis
        Quaternion rotation = transform.rotation;
        transform.rotation = Quaternion.Euler(-90, 0, rotation.eulerAngles.z);
    }
}
