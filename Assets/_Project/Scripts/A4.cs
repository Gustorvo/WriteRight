using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Project.Scripts
{
	[Serializable]
	public class Waypoints
	{
		public Transform a;
		public Transform b;

		public Waypoints(Vector3 posA, Vector3 posB, Transform parent)
		{
			a = new GameObject("A").transform;
			a.position = posA;
			b = new GameObject("B").transform;
			b.position = posB;
			//a.SetParent(parent, false);
			//b.SetParent(parent, false);
		}

		public void SetA(Vector3 posA)
		{
			a.position = posA;
		}

		public void SetB(Vector3 posB)
		{
			b.position = posB;
		}

		public void DestroyObjects()
		{
			Object.Destroy(a.gameObject);
			Object.Destroy(b.gameObject);
		}
	}

	public enum CurrentPair
	{
		First,
		Second,
		Third
	}

	public class A4 : MonoBehaviour
	{
		[SerializeField] private Transform traicingSphere;
		[SerializeField] private float maxDelta = 0.003f;
		[SerializeField] private Waypoints firstPair, secondPair, thirdPair;
		[SerializeField] private Transform waipointsParent;
		private TextureEraser textureEraser;
		private TipPosition tipPosition;

		private CurrentPair currentPair = CurrentPair.First;
		private Waypoints currentWaypoint;

		private float heightOffset;

		private void Awake()
		{
			currentWaypoint = new Waypoints(Vector3.zero, Vector3.zero, waipointsParent);
			textureEraser = FindObjectOfType<TextureEraser>();
			tipPosition = FindObjectOfType<TipPosition>();
			if (textureEraser != null)
			{
				textureEraser.InitMeshRenderer(this.transform);
			}

			if (tipPosition != null)
			{
				tipPosition.InitMeshCollider(GetComponent<MeshCollider>());
			}

			tipPosition.OnTipCollision += OnTipCollision;
			heightOffset = transform.lossyScale.y * 0.5f;

			tipPosition.OnTipCollisionStart += OnTipCollisionStart;
			tipPosition.OnTipCollisionEnd += OnTipCollisionEnd;
		}


		private void OnTipCollisionStart(Vector3 pos)
		{
			ResetPositions();
			currentWaypoint.SetA(pos);
		}

		private void OnTipCollisionEnd(Vector3 pos)
		{
			currentWaypoint.SetB(pos);

			// get the distance between waypoints
			Waypoints target = currentPair == CurrentPair.First ? firstPair :
				currentPair == CurrentPair.Second ? secondPair : thirdPair;

			float a2a = GetXZDistance(currentWaypoint.a.position, target.a.position);
			float b2b = GetXZDistance(currentWaypoint.b.position, target.b.position);
			Debug.Log("a2a: " + a2a + " b2b: " + b2b);
			if (a2a > maxDelta || b2b > maxDelta)
			{
				textureEraser.ResetTexture();
				currentPair = CurrentPair.First;
			}
			else
			{
				currentPair = CurrentPair.First == currentPair ? CurrentPair.Second : CurrentPair.Third;
			}
		}

		private void ResetPositions()
		{
			currentWaypoint.SetA(default);
			currentWaypoint.SetB(default);
		}

		private void OnDestroy()
		{
			tipPosition.OnTipCollision -= OnTipCollision;
			tipPosition.OnTipCollisionStart -= OnTipCollisionStart;
			tipPosition.OnTipCollisionEnd -= OnTipCollisionEnd;
		}

		private void OnTipCollision(Vector3 obj)
		{
			// check the Y pos of tip and move our object if necessary

			float deltaY = Mathf.Abs(transform.position.y - obj.y);
			var uv = tipPosition.Get();
			textureEraser.Write(uv);
		}

		float GetXZDistance(Vector3 a, Vector3 b)
		{
			return Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
		}
	}
}