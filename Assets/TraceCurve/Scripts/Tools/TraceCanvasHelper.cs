using UnityEngine;

namespace TraceCurve
{
	public class TraceCanvasHelper : MonoBehaviour
	{
		void Awake()
		{
			var canvas = GetComponent<Canvas>();
			if (canvas != null && canvas.worldCamera == null)
			{
				canvas.worldCamera = Camera.main;
			}
		}
	}
}