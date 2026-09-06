using UnityEngine;

/// <summary>
/// Resets the player to a safe spawn point if they fall below the playable world.
/// </summary>
public sealed class VoidRespawn : MonoBehaviour
{
    [SerializeField] private float voidHeight = -10f;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 spawnEulerAngles = Vector3.zero;

    private CharacterController characterController;
    private FirstPersonController firstPersonController;
    private float nextRespawnTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        firstPersonController = GetComponent<FirstPersonController>();
    }

    private void Update()
    {
        if (Time.time < nextRespawnTime || transform.position.y >= voidHeight)
        {
            return;
        }

        Respawn();
    }

    public void Respawn()
    {
        nextRespawnTime = Time.time + 0.25f;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(spawnEulerAngles));

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (firstPersonController != null)
        {
            firstPersonController.ResetVerticalVelocity();
        }
    }
}
