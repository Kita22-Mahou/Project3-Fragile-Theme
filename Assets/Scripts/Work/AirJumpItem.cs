using UnityEngine;
using System.Collections;

public class AirJumpItem : MonoBehaviour
{
    [SerializeField] private float respawnTime = 5f;

    private bool collected;

    private Collider2D[] itemColliders;
    private Renderer[] itemRenderers;

    private bool[] colliderStates;
    private bool[] rendererStates;

    private void Awake()
    {
        itemColliders = GetComponentsInChildren<Collider2D>(true);
        itemRenderers = GetComponentsInChildren<Renderer>(true);

        colliderStates = new bool[itemColliders.Length];
        rendererStates = new bool[itemRenderers.Length];

        for (int i = 0; i < itemColliders.Length; i++)
        {
            colliderStates[i] = itemColliders[i].enabled;
        }

        for (int i = 0; i < itemRenderers.Length; i++)
        {
            rendererStates[i] = itemRenderers[i].enabled;
        }
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
            return;

        PlayerController player =
            other.GetComponentInParent<PlayerController>();

        if (player == null)
            return;

        bool restored = player.RestoreAirJump();

        if (!restored)
            return;

        collected = true;
        GameAudio.Instance?.PlayItemPickup();
        SetItemVisible(false);

        StartCoroutine(RespawnItem());
    }

    private IEnumerator RespawnItem()
    {
        yield return new WaitForSecondsRealtime(respawnTime);

        collected = false;

        SetItemVisible(true);
    }

    private void SetItemVisible(bool visible)
    {
        for (int i = 0; i < itemColliders.Length; i++)
        {
            if (itemColliders[i] != null)
            {
                itemColliders[i].enabled =
                    visible && colliderStates[i];
            }
        }

        for (int i = 0; i < itemRenderers.Length; i++)
        {
            if (itemRenderers[i] != null)
            {
                itemRenderers[i].enabled =
                    visible && rendererStates[i];
            }
        }
    }
}