using System;
using UnityEngine;

public class AudioController : MonoBehaviour
{

    [SerializeField] AudioSource audioSource;
    [SerializeField] bool isMusicSource;
    [SerializeField] bool isSFXSource;

    void Awake()
    {
        if(audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
  
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (isMusicSource)
        {
            GameEvents.OnMusicVolumeChanged += UpdateVolume;
        }
        if (isSFXSource)
        {
            GameEvents.OnSFXVolumeChanged += UpdateVolume;
        }
    }

    private void UpdateVolume()
    {
        if(isMusicSource)
        {
           audioSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        }
        if (isSFXSource)
        {
           audioSource.volume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        }
    }

    void OnDestroy()
        {
            if (isMusicSource)
            {
                GameEvents.OnMusicVolumeChanged -= UpdateVolume;
            }
            else if (isSFXSource)
            {
                GameEvents.OnSFXVolumeChanged -= UpdateVolume;
            }
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
