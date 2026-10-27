using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource _seAudioSource;
    [SerializeField] private AudioSource _bgmAudioSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip[] _seClips;
    [SerializeField] private AudioClip[] _bgmClips;

    public void PlaySE(int index)
    {
        if (_seAudioSource == null || !TryGetClip(_seClips, index, out AudioClip clip))
        {
            return;
        }

        _seAudioSource.PlayOneShot(clip);
    }

    public void PlayBGM(int index)
    {
        if (_bgmAudioSource == null || !TryGetClip(_bgmClips, index, out AudioClip clip))
        {
            return;
        }

        _bgmAudioSource.clip = clip;
        _bgmAudioSource.Play();
    }

    public void StopBGM()
    {
        if (_bgmAudioSource == null)
        {
            return;
        }

        _bgmAudioSource.Stop();
    }

    private static bool TryGetClip(AudioClip[] clips, int index, out AudioClip clip)
    {
        clip = null;

        if (clips == null || index < 0 || index >= clips.Length)
        {
            Debug.LogWarning($"Sound index {index} is out of range.");
            return false;
        }

        clip = clips[index];
        if (clip == null)
        {
            Debug.LogWarning($"AudioClip at index {index} is not assigned.");
            return false;
        }

        return true;
    }
}
