using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace TraceCurve
{
    public class EventSystemHelper : MonoBehaviour
    {
        [SerializeField] private EventSystem eventSystem;
        
        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            var standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneInputModule != null)
            {
                Destroy(standaloneInputModule);
            }
            var inputSystemUIModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputSystemUIModule == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            var inputSystemUIModule = eventSystem.GetComponent("InputSystemUIInputModule");
            if (inputSystemUIModule != null)
            {
                Destroy(inputSystemUIModule);
            }
            var standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneInputModule == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }
    }
}