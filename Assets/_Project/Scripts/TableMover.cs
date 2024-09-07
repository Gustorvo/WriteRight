using System;
using UnityEngine;

namespace _Project.Scripts
{
    public class TableMover : MonoBehaviour
    {
        [SerializeField] float offsetY = 0.0f;
        private TipPosition tipPosition;

        private void Awake()
        {
            tipPosition = FindObjectOfType<TipPosition>();
            tipPosition.OnTipCollision += OnTipCollision;
        }
        private void OnDestroy()
        {
            tipPosition.OnTipCollision -= OnTipCollision;
        }
        
        private void OnTipCollision(Vector3 pos)
        {
            // check the Y pos of tip and move our object if necessary

            float deltaY = Mathf.Abs(transform.position.y - pos.y + offsetY);
           
            if (deltaY > 0.001f)
            {
                transform.position = new Vector3(transform.position.x, pos.y + offsetY, transform.position.z);
            }
        }
    }
}
