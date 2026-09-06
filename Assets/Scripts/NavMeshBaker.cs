using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Bakes the NavMeshSurface as the scene starts.
///
/// Baking at runtime rather than in the editor keeps the generated sandbox self-contained:
/// there is no NavMesh asset to save, lose, or regenerate by hand after a rebuild. Runs early
/// so <see cref="TeacherAI"/> finds a mesh under it on the first frame.
/// </summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(NavMeshSurface))]
public sealed class NavMeshBaker : MonoBehaviour
{
    [SerializeField] private NavMeshSurface surface;
    [SerializeField] private bool bakeOnAwake = true;
    [SerializeField] private bool logBakeTime = true;

    private void Awake()
    {
        if (surface == null)
        {
            surface = GetComponent<NavMeshSurface>();
        }

        if (bakeOnAwake)
        {
            Bake();
        }
    }

    public void Bake()
    {
        if (surface == null)
        {
            Debug.LogWarning("NavMeshBaker has no NavMeshSurface to bake.", this);
            return;
        }

        float startedAt = Time.realtimeSinceStartup;
        surface.BuildNavMesh();

        if (logBakeTime)
        {
            float elapsedMilliseconds = (Time.realtimeSinceStartup - startedAt) * 1000f;
            Debug.Log($"NavMesh baked in {elapsedMilliseconds:F0} ms.", this);
        }
    }
}
