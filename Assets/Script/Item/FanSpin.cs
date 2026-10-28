using UnityEngine;

namespace TeamC.FanAsset
{
    [DisallowMultipleComponent]
    public class FanSpin : MonoBehaviour
    {
        [Tooltip("Rotating blades. The housing stays still.")]
        public Transform Rotor;
        [Tooltip("Degrees per second. 720 = two turns per second. Set to 0 to stop.")]
        public float DegreesPerSecond = 720f;
        [Tooltip("Local shaft axis, detected from the imported blade mesh.")]
        public Vector3 LocalAxis = Vector3.forward;

        public void AssignRotor(Transform rotor)
        {
            Rotor = rotor;
            if (Rotor == null) return;
            MeshFilter filter = Rotor.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            Vector3 size = filter.sharedMesh.bounds.size;
            LocalAxis = size.x < size.y && size.x < size.z ? Vector3.right
                : size.y < size.z ? Vector3.up : Vector3.forward;
        }

        void Awake()
        {
            if (Rotor != null) return;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name == "Rotor") { AssignRotor(child); break; }
        }

        void Update()
        {
            if (Rotor != null && LocalAxis.sqrMagnitude > 0f)
                Rotor.Rotate(LocalAxis.normalized, DegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
