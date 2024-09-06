using System;
using UnityEngine;

namespace TraceCurve
{
	[Serializable]
	public class Geometry : MonoBehaviour
	{
		public enum Type
		{
			None,
			Line,
			Curve,
			Dot
		}

		public virtual Type CurveType
		{
			get { return Type.None; }
		}

		public string Name;

		public bool ShouldContinue;
		public Vector3 Start = Vector3.left;
		public Vector3 End = Vector3.right;
	}
}