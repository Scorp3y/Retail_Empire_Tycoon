using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaySound : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioSource musicSource;

    private const string MusicVolumeKey = "MusicVolume"; 
    private const string SoundVolumeKey = "SoundVolume"; 

    void Start()
    {
        float savedMusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.2f); 
        float savedSoundVolume = PlayerPrefs.GetFloat(SoundVolumeKey, 0.2f); 

        musicSource.volume = savedMusicVolume;
        audioSource.volume = savedSoundVolume;

        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlayClickSound()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    public void MuteClickMusic()
    {
        musicSource.Stop();
        SaveMusicVolume(0f); 
    }

    public void UnMuteClickMusic()
    {
        musicSource.Play();
        SaveMusicVolume(0.2f); 
    }

    private void SaveMusicVolume(float volume)
    {
        musicSource.volume = volume;
        PlayerPrefs.SetFloat(MusicVolumeKey, volume);
        PlayerPrefs.Save();
    }

    public void SaveSoundVolume(float volume)
    {
        audioSource.volume = volume;
        PlayerPrefs.SetFloat(SoundVolumeKey, volume);
        PlayerPrefs.Save();
    }
}
