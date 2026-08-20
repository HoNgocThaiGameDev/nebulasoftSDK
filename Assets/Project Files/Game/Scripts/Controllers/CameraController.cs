using UnityEngine;

namespace NebulaSoft
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [SerializeField] Vector3 offset;
        [SerializeField] float sideOffset = 0.5f;
        [SerializeField] float yOffset = 12;

        public void Reposition(Vector3 targetPosition, Vector3 levelSize)
        {
            Camera camera = GetComponent<Camera>();

            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
            float frustumHeight = 2.0f * distanceToTarget * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float frustumWidth = frustumHeight * camera.aspect;

            float levelWidth = levelSize.x + sideOffset;
            float levelHeight = levelSize.z + yOffset;

            float distanceMultiplier = Mathf.Max(levelWidth / frustumWidth, levelHeight / frustumHeight);

            transform.position = targetPosition + (offset * distanceMultiplier);
        }
    }
}