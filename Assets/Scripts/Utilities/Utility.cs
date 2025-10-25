using UnityEngine;


namespace UtilOfAi
{

    public class Utility : MonoBehaviour
    {
        // Safe singleton: will find an existing Utility in the scene or create one on demand.
        private static Utility _instance;
        public static Utility Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Use newer API to locate any existing Utility instance in the scene
                    _instance = Object.FindAnyObjectByType<Utility>();
                    if (_instance == null)
                    {
                        var go = new GameObject("_Utility_Singleton");
                        _instance = go.AddComponent<Utility>();
                        // Keep the utility object out of the scene hierarchy clutter at runtime
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        void Awake()
        {
            // Ensure singleton is set if this component is part of the scene.
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
        }

        public Vector3 RandomDirection()
        {
            return new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), 0f);
        }

        public bool WithInRadius(float radiusDetection, Transform objectA, Transform objectB)
        {
            if (Vector2.Distance(objectA.position, objectB.position) <= radiusDetection)
            {
                return true;
            }
            else{
                return false;
            }
        }
    }
}