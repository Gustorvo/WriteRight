using System;
using UnityEngine;

namespace _Project.Scripts
{
	public class A4 : MonoBehaviour
	{
		[SerializeField] TextureEraser textureEraser;
		[SerializeField] TipPosition tipPosition;

		private float heightOffset;

		private void Awake()
		{
			tipPosition.OnTipCollision += OnTipCollision;
			heightOffset = transform.lossyScale.y * 0.5f;
		}

		private void OnDestroy()
		{
			tipPosition.OnTipCollision -= OnTipCollision;
		}

		private void OnTipCollision(Vector3 obj)
		{
			// check the Y pos of tip and move our object if necessary

			float deltaY = Mathf.Abs(transform.position.y - (obj.y - heightOffset));
			if (deltaY > 0.001f)
			{
				transform.position = new Vector3(transform.position.x, obj.y - heightOffset, transform.position.z);
			}

			var uv = tipPosition.Get();
			textureEraser.Write(uv);
		}
	}
}