using System;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Project.Scripts
{
	[Serializable]
	public class Waypoints
	{
		public Transform a;
		public Transform b;
		public float Distance => Vector3.Distance(a.position, b.position);

		public Waypoints(Vector3 posA, Vector3 posB, Transform parent)
		{
			a = new GameObject("A").transform;
			a.position = posA;
			b = new GameObject("B").transform;
			b.position = posB;
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

	public enum PairNumber
	{
		First,
		Second,
		Third
	}

	public class A4 : MonoBehaviour
	{
		[SerializeField] AnimationCurve tracingCurve;
		[SerializeField] private Transform traicingSphere;
		[SerializeField] private float maxDelta = 0.003f;
		[SerializeField] private Waypoints firstPair, secondPair, thirdPair;
		[SerializeField] private Transform waipointsParent;

		public event Action OnComplete;
		private TextureEraser textureEraser;
		private TipPosition tipPosition;

		private PairNumber currentPair = PairNumber.First;
		private Waypoints dynamicWaypoint;

		private float heightOffset;
		private Tween tracingTween;

		private void Awake()
		{
			dynamicWaypoint = new Waypoints(Vector3.zero, Vector3.zero, waipointsParent);
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

		private void Start()
		{
			currentPair = PairNumber.First;
			// start tweening the traicingSphere between A and B
			StartTracingTween();
			tracingTween.OnKill(() => traicingSphere.gameObject.SetActive(false));
		}

		private void StartTracingTween()
		{
			var target = GetCurrentWaypoint();
			traicingSphere.position = target.a.position;
			traicingSphere.gameObject.SetActive(true);
			tracingTween = traicingSphere.DOMove(target.b.position, 1.5f).SetLoops(-1, LoopType.Restart)
				.SetEase(tracingCurve);
		}


		private void OnTipCollisionStart(Vector3 pos)
		{
			tracingTween.Kill();
			traicingSphere.position = GetCurrentWaypoint().b.position;
			ResetPositions();
			dynamicWaypoint.SetA(pos);
		}

		private void OnTipCollisionEnd(Vector3 pos)
		{
			dynamicWaypoint.SetB(pos);

			var target = GetCurrentWaypoint();

			float a2a = GetXZDistance(dynamicWaypoint.a.position, target.a.position);
			float b2b = GetXZDistance(dynamicWaypoint.b.position, target.b.position);
			float a2b = GetXZDistance(dynamicWaypoint.a.position, dynamicWaypoint.b.position);

			PairNumber nextPair = currentPair == PairNumber.First ? PairNumber.Second : PairNumber.Third;
			Debug.Log("a2a: " + a2a + " b2b: " + b2b);
			if (a2a > maxDelta || b2b > maxDelta)
			{
				textureEraser.ResetTexture();
				currentPair = PairNumber.First;
			}
			else
			{
				bool reachedTheEnd = nextPair == currentPair;
				if (reachedTheEnd)
				{
					OnComplete?.Invoke();
					traicingSphere.gameObject.SetActive(false);
					return;
				}

				currentPair = nextPair;
			}

			StartTracingTween();
		}

		private Waypoints GetCurrentWaypoint()
		{
			// get the distance between waypoints
			Waypoints target = currentPair == PairNumber.First ? firstPair :
				currentPair == PairNumber.Second ? secondPair : thirdPair;
			return target;
		}

		private void ResetPositions()
		{
			dynamicWaypoint.SetA(default);
			dynamicWaypoint.SetB(default);
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