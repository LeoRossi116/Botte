using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

namespace Botte.UI
{
    // Richiede un componente Button o un elemento cliccabile per funzionare
    [RequireComponent(typeof(Image))]
    public class UIClickEffect : MonoBehaviour, IPointerDownHandler
    {
        [Header("Audio (Supporta MP3)")]
        [Tooltip("L'effetto sonoro del click da riprodurre.")]
        public AudioClip clickSFX;
        
        [Tooltip("L'AudioSource globale da usare. Se lasciato vuoto, proverà a usare quello dell'AudioManager.")]
        public AudioSource customAudioSource;

        [Header("Animazione Visiva (Rimbalzo)")]
        [Tooltip("La dimensione minima raggiunta dal bottone durante la pressione.")]
        public Vector3 pressedScale = new Vector3(0.9f, 0.9f, 0.9f);
        
        [Tooltip("Durata dell'effetto di rimbalzo.")]
        public float animationDuration = 0.1f;

        private Vector3 originalScale;
        private Coroutine scaleCoroutine;

        private void Awake()
        {
            originalScale = transform.localScale;
        }

        // Questo metodo intercetta il click (pressione del mouse/touch) sull'elemento UI
        public void OnPointerDown(PointerEventData eventData)
        {
            // 1. Riproduci l'audio in modo sovrapponibile
            PlayClickSound();

            // 2. Avvia l'animazione visiva
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }
            scaleCoroutine = StartCoroutine(AnimateClick());
        }

        private void PlayClickSound()
        {
            if (clickSFX == null) return;

            // Cerca l'AudioSource da utilizzare
            AudioSource sourceToUse = customAudioSource;

            // Se non ne è stato assegnato uno specifico, prova a usare quello del BattleAudioManager
            if (sourceToUse == null && Botte.Audio.BattleAudioManager.Instance != null)
            {
                sourceToUse = Botte.Audio.BattleAudioManager.Instance.GetComponent<AudioSource>();
            }

            // Se ancora non ne trova uno, crea temporaneamente un AudioSource locale di backup
            if (sourceToUse == null)
            {
                sourceToUse = gameObject.GetComponent<AudioSource>();
                if (sourceToUse == null)
                {
                    sourceToUse = gameObject.AddComponent<AudioSource>();
                }
            }

            // RIPRODUZIONE SOVRAPPOSTA (PlayOneShot)
            // Questo metodo permette a più click ravvicinati di suonare insieme senza interrompersi!
            sourceToUse.PlayOneShot(clickSFX);
        }

        private IEnumerator AnimateClick()
        {
            float elapsed = 0f;

            // Fase 1: Rimpicciolimento (Pressione)
            while (elapsed < animationDuration / 2f)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(originalScale, pressedScale, elapsed / (animationDuration / 2f));
                yield return null;
            }

            elapsed = 0f;

            // Fase 2: Ritorno alla dimensione originale
            while (elapsed < animationDuration / 2f)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(pressedScale, originalScale, elapsed / (animationDuration / 2f));
                yield return null;
            }

            transform.localScale = originalScale;
            scaleCoroutine = null;
        }

        private void OnDisable()
        {
            // Ripristina la scala originale se l'oggetto viene disattivato durante l'animazione
            transform.localScale = originalScale;
        }
    }
}