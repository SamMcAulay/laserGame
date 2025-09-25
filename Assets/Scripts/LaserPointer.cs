using UnityEngine;

namespace Scripts
{
    [RequireComponent(typeof(LineRenderer))]
    public class LaserPointer : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private GameObject hitEffect;
        [SerializeField] private ParticleSystem hitParticles;

        [Header("Laser Settings")]
        [SerializeField] private float maxDistance = 100f;
        [SerializeField] private LayerMask collisionLayerMask;

        [Header("Style & Animation")]
        [Tooltip("How fast the laser's texture should scroll.")]
        [SerializeField] private float textureScrollSpeed = 8f;

        private Material _laserMaterial;

        void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }
        
            // Get a reference to the Line Renderer's material to animate it later.
            _laserMaterial = lineRenderer.material;

            lineRenderer.useWorldSpace = false;
            ToggleLaser(false); // Start with laser off.
        }

        void LateUpdate()
        {
            if (!lineRenderer.enabled) return;
            
            AnimateLaser();

            lineRenderer.SetPosition(0, Vector3.zero);

            if (Physics.Raycast(transform.position, transform.forward, out var hit, maxDistance, collisionLayerMask))
            {
                // --- Activate Effects on Hit ---
                HandleHit(hit);
                lineRenderer.SetPosition(1, transform.InverseTransformPoint(hit.point));
            }
            else
            {
                // --- Deactivate Effects on Miss ---
                HandleMiss();
                lineRenderer.SetPosition(1, new Vector3(0, 0, maxDistance));
            }
        }

        private void AnimateLaser()
        {
            float offset = Time.time * textureScrollSpeed;
            _laserMaterial.mainTextureOffset = new Vector2(offset, 0);
        }

        private void HandleHit(RaycastHit hit)
        {
            if (hitEffect)
            {
                hitEffect.SetActive(true);
                hitEffect.transform.position = hit.point;
                hitEffect.transform.rotation = Quaternion.LookRotation(hit.normal);
            }

            if (hitParticles)
            {
                // Move the particle system to the hit point and align it.
                hitParticles.transform.position = hit.point;
                hitParticles.transform.rotation = Quaternion.LookRotation(hit.normal);

                // Ensure the particle system is playing.
                if (!hitParticles.isPlaying)
                {
                    hitParticles.Play();
                }
            }
        }

        private void HandleMiss()
        {
            if (hitEffect)
            {
                hitEffect.SetActive(false);
            }

            // If we are not hitting anything, stop the particle system.
            if (hitParticles && hitParticles.isPlaying)
            {
                hitParticles.Stop();
            }
        }

        public void ToggleLaser(bool isOn)
        {
            lineRenderer.enabled = isOn;

            if (!isOn)
            {
                HandleMiss(); // Ensure all effects are off when the laser is toggled off.
            }
        }
    }
}

