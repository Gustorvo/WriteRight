using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts
{
	public class TipPosition : MonoBehaviour
	{
		[SerializeField] Transform tipTransform;
		[SerializeField] private VrStylusHandler stylusHandler;
		[SerializeField] MeshCollider meshCollider;

		private LayerMask layerMask; // Layer mask to filter specific layers in the Inspector
		private Ray ray;
		private RaycastHit hit;
		private Vector2 uvPosition;
		private Vector3 lastHitNormal;
		private Vector3 meshNormal;
		private Collider[] hitColliders = new Collider[3] { null, null, null };
		private bool init;
		private bool wasCollidingLastFrame;
		public Vector2 Get() => TryMeshCastBidirectional(out uvPosition) ? uvPosition : default;
		public event Action<Vector3> OnTipCollision;
		public event Action<Vector3> OnTipCollisionStart;
		public event Action<Vector3> OnTipCollisionEnd;
		private Vector3 heightOffset => Vector3.up * 0.01f;

		private bool startAdding;

		public void InitMeshCollider(MeshCollider collider)
		{
			meshCollider = collider;
		}


		private void FixedUpdate()
		{
			// check if tip is touching a surface
			float tip = stylusHandler.CurrentState.tip_value;
			if (tip > 0.0001f)
			{
				startAdding = false;
				if (!wasCollidingLastFrame)
					OnTipCollisionStart?.Invoke(tipTransform.position);
				OnTipCollision?.Invoke(tipTransform.position);
				wasCollidingLastFrame = true;
			}
			else
			{
				if (wasCollidingLastFrame)
				{
					OnTipCollisionEnd?.Invoke(tipTransform.position);
				}

				wasCollidingLastFrame = false;
			}
		}

		private bool TryMeshCastBidirectional(out Vector2 uvPosition)
		{
			uvPosition = default;
			Vector3 rayOrigin = transform.position + heightOffset;
			ray = new Ray(rayOrigin, Vector3.down);
			if (meshCollider.Raycast(ray, out hit, 0.02f))
			{
				uvPosition = hit.textureCoord;
				return true;
			}

			return false;
		}
	}
}