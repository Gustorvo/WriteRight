using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TraceCurve
{
	[ExecuteInEditMode]
	public class TraceCanvasScaler : MonoBehaviour
	{
		public Canvas Canvas;
		public GeometryContainer Container;
		public TraceInput Input;
		public TraceFiller Filler;
		public float PixelsPerUnit = 100f;
		
		private Vector3 previousCanvasScale = Vector3.zero;
		private Vector3 previousGeometryPosition = Vector3.zero;

		private void OnEnable()
		{
			previousCanvasScale = Vector3.zero;
			previousGeometryPosition = Vector3.zero;
			UpdateScaler();
		}

		private void OnDisable()
		{
			previousCanvasScale = Vector3.zero;
			previousGeometryPosition = Vector3.zero;
		}

		private IEnumerator Start()
		{
			yield return null;
			UpdateScaler();
		}

		private void Update()
		{
			if (Canvas != null && Container != null && (previousCanvasScale != Canvas.transform.localScale || 
			                                            previousGeometryPosition != Container.transform.position))
			{
				UpdateScaler();
			}
		}

		private void UpdateScaler()
		{
			previousCanvasScale = Canvas.transform.localScale;
			previousGeometryPosition = Container.transform.position;
			transform.localScale = Canvas.transform.localScale * PixelsPerUnit;
			Container.UpdateSegmentsData = false;
			Container.UpdateSegments();
			if (Application.isPlaying)
			{
				if (Filler != null)
				{
					Filler.Init();
				}
				else
				{
					Debug.LogWarning("Can't find TraceFiller Component!");
				}

				if (Input != null)
				{
					Input.SetBrushPosition();
				}
				else
				{
					Debug.LogWarning("Can't find TraceInput Component!");
				}
			}
		}
	}
}