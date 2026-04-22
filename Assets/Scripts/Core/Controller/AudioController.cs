using System;
using UnityEngine;
//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            Debug.LogWarning("[AudioController] Missing AudioSource reference.", this);

        if (!isMusicSource && !isSFXSource)
            Debug.LogWarning("[AudioController] Neither Music nor SFX source flag is enabled.", this);

        if (isMusicSource && isSFXSource)
            Debug.LogWarning("[AudioController] Both Music and SFX flags are enabled; this source will react to both volume channels.", this);
    }
#endif
}
