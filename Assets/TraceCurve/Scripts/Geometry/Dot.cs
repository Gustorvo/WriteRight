using System;

namespace TraceCurve
{
    [Serializable]
    public class Dot : Geometry
    {
        public override Type CurveType
        {
            get { return Type.Dot; }
        }
    }
}