using System;

namespace TraceCurve
{
	[Serializable]
	public class Line : Geometry
	{
		public override Type CurveType
		{
			get { return Type.Line; }
		}
	}
}