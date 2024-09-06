using System;
using UnityEngine;

namespace TraceCurve
{
	[Serializable]
	public class Curve : Geometry
	{
		public override Type CurveType
		{
			get { return Type.Curve; }
		}

		public Vector3 StartTangent = Vector3.down;
		public Vector3 EndTangent = Vector3.up;
	}
}