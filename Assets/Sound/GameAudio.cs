
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    [Header("Hurt Sounds - 5 Clips")]
    [SerializeField] private AudioClip[] hurtSounds = new AudioClip[5];

    [Header("First Jump Sounds - 2 Clips")]
    [SerializeField] private AudioClip[] jumpSounds = new AudioClip[2];

    [Header("Death Sounds - 2 Clips")]
    [SerializeField] private AudioClip[] deathSounds = new AudioClip[2];

    [Header("Checkpoint Sound - 1 Clip")]
    [SerializeField] private AudioClip checkpointSound;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float hurtVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float jumpVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float deathVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float checkpointVolume = 1f;

    private AudioSource sfxSource;

    private int lastHurtIndex = -1;
    private int lastJumpIndex = -1;
    private int lastDeathIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        sfxSource = GetComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void PlayRandomSound(
        AudioClip[] clips,
        ref int lastIndex,
        float volume)
    {
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        int validCount = 0;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return;
        }

        int selectedIndex;

        if (validCount == 1)
        {
            selectedIndex = 0;

            while (clips[selectedIndex] == null)
            {
                selectedIndex++;
            }
        }
        else
        {
            do
            {
                selectedIndex = Random.Range(0, clips.Length);
            }
            while (clips[selectedIndex] == null ||
                   selectedIndex == lastIndex);
        }

        lastIndex = selectedIndex;

        sfxSource.PlayOneShot(
            clips[selectedIndex],
            masterVolume * volume
        );
    }

    private void PlaySingleSound(AudioClip clip, float volume)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(
            clip,
            masterVolume * volume
        );
    }

    // เรียกเมื่อผู้เล่นได้รับความเสียหาย
    public void PlayDamage()
    {
        PlayRandomSound(hurtSounds, ref lastHurtIndex, hurtVolume);
    }

    // ใช้กับการกระโดดครั้งแรกจากพื้นเท่านั้น
    public void PlayGroundJump()
    {
        PlayRandomSound(jumpSounds, ref lastJumpIndex, jumpVolume);
    }

    // เสียงตาย
    public void PlayDeath()
    {
        PlayRandomSound(deathSounds, ref lastDeathIndex, deathVolume);
    }

    // เสียงเมื่อแตะ Checkpoint สำเร็จ
    public void PlayCheckpoint()
    {
        PlaySingleSound(checkpointSound, checkpointVolume);
    }
}
