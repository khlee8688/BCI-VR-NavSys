using UnityEngine;

public class EquirectangularUVFromDirection : MonoBehaviour
{
    public Transform sphere;

    void Update()
    {
        Vector3 worldForward = Camera.main.transform.forward;

        Vector3 dirInSphereSpace = Quaternion.Inverse(sphere.transform.rotation) * worldForward;
        dirInSphereSpace.Normalize();

        float longitude = Mathf.Atan2(dirInSphereSpace.z, dirInSphereSpace.x);
        float latitude = Mathf.Asin(dirInSphereSpace.y);

        float u = 1f - ((longitude + Mathf.PI) / (2f * Mathf.PI));
        float v = 1f - ((latitude + Mathf.PI / 2f) / Mathf.PI);

        Debug.Log($"UV Coordinates: U = {u:F4}, V = {v:F4}");
    }
}
