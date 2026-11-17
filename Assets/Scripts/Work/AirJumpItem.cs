using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AirJumpItem : MonoBehaviour
{
    private bool collected = false;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player == null)
            return;

        bool restored = player.RestoreAirJump();

        if (restored)
        {
            collected = true;

            Destroy(gameObject);
        }
    }
}