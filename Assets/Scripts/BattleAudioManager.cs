using UnityEngine;
using System.Collections;
using Botte.UI;

namespace Botte.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class BattleAudioManager : MonoBehaviour
    {
        public static BattleAudioManager Instance { get; private set; }

        [Header("Riferimento UI")]
        public BattleUI battleUI;

        [Header("Tracce Musicali")]
        public AudioClip menuMusic;
        public AudioClip battleMusic;

        [Header("Impostazioni Transizione")]
        [Range(0.1f, 3f)] public float fadeDuration = 1.0f;

        private AudioSource audioSource;
        private bool isPlayingBattleMusic = false;
        private Coroutine fadeCoroutine;

        private float currentMusicVolume = 0.5f;
        public float MusicVolume
        {
            get => currentMusicVolume;
            set
            {
                currentMusicVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("MusicVolume", currentMusicVolume);
                PlayerPrefs.Save();
                if (fadeCoroutine == null && audioSource != null)
                {
                    audioSource.volume = currentMusicVolume;
                }
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Opzionale: Se la tua scena di gioco viene ricaricata interamente ad ogni partita,
                // scommenta la riga sotto per non distruggere l'audio player al caricamento.
                // DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            audioSource = GetComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.volume = 0f;

            currentMusicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        }

        private void Start()
        {
            if (battleUI == null)
            {
                battleUI = FindFirstObjectByType<BattleUI>();
            }

            ResetMusicBasedOnScreen();
        }

        private void Update()
        {
            if (battleUI == null || battleUI.battleScreen == null) return;

            bool isBattleActive = battleUI.battleScreen.activeInHierarchy;

            // Cambia traccia dinamicamente se lo stato del BattleScreen cambia durante il gioco
            if (isBattleActive != isPlayingBattleMusic)
            {
                isPlayingBattleMusic = isBattleActive;
                AudioClip nextClip = isPlayingBattleMusic ? battleMusic : menuMusic;
                TransitionToTrack(nextClip);
            }
        }

        /// <summary>
        /// Forza il ripristino della musica del menu. 
        /// Utile quando si esce dalla partita o si resetta lo stato del gioco.
        /// </summary>
        public void ForceMenuMusic()
        {
            isPlayingBattleMusic = false;
            TransitionToTrack(menuMusic);
        }

        private void ResetMusicBasedOnScreen()
        {
            if (battleUI != null && battleUI.battleScreen != null)
            {
                isPlayingBattleMusic = battleUI.battleScreen.activeInHierarchy;
                AudioClip initialClip = isPlayingBattleMusic ? battleMusic : menuMusic;
                
                if (initialClip != null)
                {
                    audioSource.clip = initialClip;
                    audioSource.Play();
                    audioSource.volume = currentMusicVolume;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void TransitionToTrack(AudioClip newClip)
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeToTrackCoroutine(newClip));
        }

        private IEnumerator FadeToTrackCoroutine(AudioClip newClip)
        {
            if (audioSource.isPlaying && audioSource.clip != newClip)
            {
                yield return StartCoroutine(FadeAudio(0f, fadeDuration / 2f));
            }

            if (audioSource.clip != newClip)
            {
                audioSource.clip = newClip;
                if (newClip != null)
                {
                    audioSource.Play();
                    yield return StartCoroutine(FadeAudio(currentMusicVolume, fadeDuration / 2f));
                }
                else
                {
                    audioSource.Stop();
                }
            }
            else
            {
                // Se la clip è già quella giusta (es. era già impostato il menu), si assicura solo che il volume sia corretto
                yield return StartCoroutine(FadeAudio(currentMusicVolume, fadeDuration / 2f));
            }
            
            fadeCoroutine = null;
        }

        private IEnumerator FadeAudio(float targetVolume, float duration)
        {
            float startVolume = audioSource.volume;
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float actualTarget = Mathf.Min(targetVolume, currentMusicVolume);
                audioSource.volume = Mathf.Lerp(startVolume, actualTarget, time / duration);
                yield return null;
            }

            audioSource.volume = Mathf.Min(targetVolume, currentMusicVolume);
        }
    }
}