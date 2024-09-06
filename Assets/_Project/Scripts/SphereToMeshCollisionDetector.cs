using System;
using UnityEngine;

namespace _Project.Scripts
{
	public struct BrushCollision
	{
		/// <summary>
		/// Point on the mesh in UV coordinates that was hit
		/// </summary>
		public Vector2 uv;

		/// <summary>
		/// a value between 0 and 1, where value close to 0 means very little penetration and 1 means full penetration
		/// </summary>
		public float penetrationValue;

		public BrushCollision(Vector2 hitTextureCoord, float penetrationValue)
		{
			uv = hitTextureCoord;
			this.penetrationValue = penetrationValue;
		}
	}

	public class SphereToMeshCollisionDetector : MonoBehaviour

	{
		[SerializeField] float radius = 0.03f;
		[SerializeField] MeshCollider meshCollider;


		private LayerMask layerMask; // Layer mask to filter specific layers in the Inspector
		private Ray ray;
		private RaycastHit hit;
		private float rayDistance => radius * 0.5f;

		private Vector3 lastHitNormal;
		private Vector3 meshNormal;
		private Collider[] hitColliders = new Collider[3] { null, null, null };
		private bool init;

		public event Action<BrushCollision> OnBrushCollision;

		private void Awake()
		{
			init = meshCollider != null;
		}

		private void FixedUpdate()
		{
			if (!init) return;
			if (IsOverlappingMesh())
				MeshCastBidirectional();
		}


		private bool IsOverlappingMesh()
		{
			return Physics.OverlapSphereNonAlloc(transform.position, radius, hitColliders, layerMask) > 0;
		}

		private void MeshCastBidirectional()
		{
			Debug.Log("MeshCastBidirectional");
			Vector3 rayOrigin = transform.position;

			Vector3 rayDirection = Vector3.Lerp(-meshNormal, -lastHitNormal, 0.5f).normalized;

			if (!MeshCastInDirection(rayDirection))
			{
				MeshCastInDirection(-rayDirection);
			}

			bool MeshCastInDirection(Vector3 direction = default)
			{
				ray = new Ray(rayOrigin, direction);
				if (meshCollider.Raycast(ray, out hit, rayDistance))
				{
					Vector2 uv = hit.textureCoord;
					float penetration = Mathf.InverseLerp(rayDistance, 0, hit.distance);
					OnBrushCollision?.Invoke(new BrushCollision(uv, penetration));
					lastHitNormal = hit.normal;
				}

				return false;
			}
		}

		public void SetCollider(Transform meshTransform)
		{
			meshCollider = !meshTransform.TryGetComponent(out meshCollider) ? meshTransform.gameObject.AddComponent<MeshCollider>() : meshTransform.GetComponent<MeshCollider>();

			meshNormal = meshCollider.transform.up;
			lastHitNormal = meshNormal;
			layerMask = 1 << meshCollider.gameObject.layer;
			init = true;
		}
	}
}