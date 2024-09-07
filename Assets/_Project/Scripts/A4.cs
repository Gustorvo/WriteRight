using System;
using UnityEngine;

namespace _Project.Scripts
{
	public class A4 : MonoBehaviour
	{
		private TextureEraser textureEraser;
		private TipPosition tipPosition;

		private float heightOffset;

		private void Awake()
		{
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
		}

		private void OnDestroy()
		{
			tipPosition.OnTipCollision -= OnTipCollision;
		}

		private void OnTipCollision(Vector3 obj)
		{
			// check the Y pos of tip and move our object if necessary

			float deltaY = Mathf.Abs(transform.position.y - obj.y );
			var uv = tipPosition.Get();
			textureEraser.Write(uv);
		}
	}
}