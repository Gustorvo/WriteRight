using UnityEngine;

namespace TraceCurve
{
	public static class MathHelper
	{
		private const float Eps = 0.000001f;

		public static Vector2[] GetPerpendiculars(Vector2 v1, Vector2 v2)
		{
			var first = v1;
			var second = v2;
			var newVec = first - second;
			var newVector = (Vector2) Vector3.Cross(newVec, Vector3.forward);
			var width = Vector2.Distance(v1, v2);
			var newPoint = width * newVector + first;
			var newPoint2 = -width * newVector + first;
			return new[] {newPoint, newPoint2};
		}

		public static bool HalfSpace(Vector2 v1, Vector2 v2, Vector2 v3)
		{
			return (v3.x - v2.x) * (v1.y - v2.y) - (v3.y - v2.y) * (v1.x - v2.x) > 0f;
		}

		public static float NearestPointOnLine(Vector3 linePoint, Vector3 lineDir, Vector3 point)
		{
			lineDir.Normalize();
			var v = point - linePoint;
			var d = Vector3.Dot(v, lineDir);
			return d;
		}

		public static Vector3 NearestPointOnFiniteLine(Vector3 start, Vector3 end, Vector3 point)
		{
			var line = end - start;
			var len = line.magnitude;
			line.Normalize();

			var v = point - start;
			var d = Vector3.Dot(v, line);
			d = Mathf.Clamp(d, 0f, len);
			return start + line * d;
		}
						
		public static Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
		{
			var u = 1 - t;
			var tt = t * t;
			var uu = u * u;
			var uuu = uu * u;
			var ttt = tt * t;
			var p = uuu * p0; 
			p += 3 * uu * t * p1; 
			p += 3 * u * tt * p2; 
			p += ttt * p3; 
			return p;
		}

		public static Vector3 GetNonZeroVector(Vector3 point)
		{
			return new Vector3(GetNonZeroValue(point.x), GetNonZeroValue(point.y), GetNonZeroValue(point.z));
		}

		private static float GetNonZeroValue(float value)
		{
			if (Mathf.Abs(value) <= Eps)
			{
				var sign = Mathf.Sign(value) == 1f;
				value = sign ? Eps : -Eps;
			}
			return value;
		}

		public static int GetCurveSegmentsCount(Curve curve)
		{
			var chord = (curve.StartTangent - curve.Start).sqrMagnitude;
			var startEndLength = (curve.Start - curve.End).sqrMagnitude;
			var startTangentEndLength = (curve.StartTangent - curve.End).sqrMagnitude;
			var endTangentStartTangentLength = (curve.EndTangent - curve.StartTangent).sqrMagnitude;
			var controlNet = startEndLength + startTangentEndLength + endTangentStartTangentLength;
			var length = (controlNet + chord) / 2f;
			return (int) Mathf.Clamp(length, 16f, 64f);
		}

		public static Vector3 TransformPoint(Vector3 point, Quaternion rotation, Vector3 scale, Vector3 position)
		{
			return rotation * Vector3.Scale(point, scale) + position;
		}
	}
}
