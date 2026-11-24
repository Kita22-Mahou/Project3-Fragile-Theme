using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class StoryBgm : MonoBehaviour
{
    [Header("ไฟล์เพลง BGM ของหน้านี้")]
    [SerializeField] private AudioClip bgmClip;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnStart = true;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        audioSource.volume = volume;
        audioSource.clip = bgmClip;
    }

    private void Start()
    {
        if (playOnStart && bgmClip != null)
        {
            audioSource.Play();
        }
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (audioSource != null)
            audioSource.volume = volume;
    }

    public void StopBgm()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }
}