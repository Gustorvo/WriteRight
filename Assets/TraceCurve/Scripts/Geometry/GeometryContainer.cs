using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace TraceCurve
{
	public class GeometryContainer : MonoBehaviour
	{
		[SerializeField] public List<Geometry> Objects = new List<Geometry>();
		public List<GeometryData> SegmentsData = new List<GeometryData>();
		public bool UpdateSegmentsData = true;

		[ Button]
		private void RemoveAll()
		{
			SegmentsData = new List<GeometryData>();
		}
		private void OnEnable()
		{
			UpdateSegments();
		}

		private void Awake()
		{
			UpdateSegmentsData = true;
		}
		
		private IEnumerator Start()
		{
			yield return null;
			UpdateSegments();
		}
		
		private void Update()
		{
			if (UpdateSegmentsData)
			{
				UpdateSegmentsData = false;
				UpdateSegments();
			}
		}

		public void UpdateSegments()
		{
			var rotation = transform.rotation;
			var scale = MathHelper.GetNonZeroVector(transform.localScale);
			var position = transform.position;
			var segmentsData = new List<GeometryData>();
			foreach (var connector in Objects)
			{
				if (connector == null)
					continue;

				if (connector.CurveType == Geometry.Type.Curve)
				{
					var curve = connector as Curve;
					var startTransformed = MathHelper.TransformPoint(curve.Start, rotation, scale, position);
					var endTransformed = MathHelper.TransformPoint(curve.End, rotation, scale, position);
					var startTangentTransformed = MathHelper.TransformPoint(curve.StartTangent, rotation, scale, position);
					var endTangentTransformed = MathHelper.TransformPoint(curve.EndTangent, rotation, scale, position);
					var linesCount = MathHelper.GetCurveSegmentsCount(curve);
					var points = new Vector3[linesCount + 1];
					for (var j = 0; j <= linesCount; j++)
					{
						var t = j / (float)linesCount;
						var point = MathHelper.CalculateCubicBezierPoint(t, startTransformed, startTangentTransformed, endTangentTransformed, endTransformed);
						points[j] = point;
					}
					segmentsData.Add(new GeometryData {Object = curve, Points = points});
				}
				else if (connector.CurveType == Geometry.Type.Line)
				{
					var line = connector as Line;
					var startTransformed = MathHelper.TransformPoint(line.Start, rotation, scale, position);
					var endTransformed = MathHelper.TransformPoint(line.End, rotation, scale, position);
					var points = new[] {startTransformed, endTransformed};
					segmentsData.Add(new GeometryData {Object = line, Points = points});
				}
				else if (connector.CurveType == Geometry.Type.Dot)
				{
					var line = connector as Dot;
					var startTransformed = MathHelper.TransformPoint(line.Start, rotation, scale, position);
					var endTransformed = MathHelper.TransformPoint(line.End, rotation, scale, position);
					var points = new[] {startTransformed, endTransformed};
					segmentsData.Add(new GeometryData {Object = line, Points = points});
				}
			}
			SegmentsData = segmentsData;
		}
	}
}