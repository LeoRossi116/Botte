using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Botte.UI
{
    // Drives a uGUI Image through simple frame-based animation states.
    //
    // Frames come from a single sliced sprite sheet per hero class, located at
    // Resources/Sprites/Heroes/<ClassName>_Sheet. The sheet's sub-sprites are named
    // "<state>_<index>" (e.g. "idle_0", "attacking_3") where state is one of
    // "idle", "attacking", "death", "victory". Setup() is called with the hero CLASS name
    // (Warrior/Mage/Rogue/Necro) since the art is per-class. If no frames exist for a state
    // the Image is left untouched (graceful placeholder fallback), so the component is safe
    // even with no art present.
    public class HeroSpriteAnimator : MonoBehaviour
    {
        public enum State { Idle, Attacking, Death, Victory }

        [Header("Config")]
        public string heroName; // hero CLASS name used to locate the sheet
        public float fps = 8f;
        [SerializeField] private Image image;

        private readonly Dictionary<State, Sprite[]> _frames = new Dictionary<State, Sprite[]>();
        private State _current = State.Idle;
        private int _frameIndex;
        private float _timer;
        private bool _setup;

        // True if any state has at least one loaded frame.
        public bool HasArt
        {
            get
            {
                foreach (var kv in _frames)
                    if (kv.Value != null && kv.Value.Length > 0) return true;
                return false;
            }
        }

        public State CurrentState => _current;

        private void Awake()
        {
            if (image == null) image = GetComponent<Image>();
        }

        // Loads all state frames for the given hero from Resources. Safe to call repeatedly.
        public void Setup(string hero)
        {
            heroName = hero;
            _frames.Clear();
            LoadState(State.Idle, "idle");
            LoadState(State.Attacking, "attacking");
            LoadState(State.Death, "death");
            LoadState(State.Victory, "victory");
            _setup = true;
            SetState(State.Idle);
        }

        private void LoadState(State s, string stateName)
        {
            var list = new List<Sprite>();
            if (!string.IsNullOrEmpty(heroName))
            {
                string basePath = $"Sprites/{heroName}/{stateName}";

                // 1) A sliced sprite sheet returns multiple sub-sprites via LoadAll.
                Sprite[] sheet = Resources.LoadAll<Sprite>(basePath);
                if (sheet != null && sheet.Length > 1)
                {
                    list.AddRange(sheet);
                }
                else
                {
                    // 2) Numbered single sprites: state_0, state_1, ...
                    int i = 0;
                    while (true)
                    {
                        Sprite frame = Resources.Load<Sprite>($"{basePath}_{i}");
                        if (frame == null) break;
                        list.Add(frame);
                        i++;
                    }
                    // 3) Fallback to a single sprite named exactly the state.
                    if (list.Count == 0)
                    {
                        Sprite single = Resources.Load<Sprite>(basePath);
                        if (single != null) list.Add(single);
                        else if (sheet != null && sheet.Length == 1) list.Add(sheet[0]);
                    }
                }
            }
            _frames[s] = list.ToArray();
        }

        public void SetState(State s)
        {
            _current = s;
            _frameIndex = 0;
            _timer = 0f;
            ApplyFrame();
        }

        private Sprite[] CurrentFrames => _frames.TryGetValue(_current, out var f) ? f : null;

        private void ApplyFrame()
        {
            var frames = CurrentFrames;
            if (image == null || frames == null || frames.Length == 0) return; // graceful fallback
            _frameIndex = Mathf.Clamp(_frameIndex, 0, frames.Length - 1);
            image.sprite = frames[_frameIndex];
        }

        private void Update()
        {
            if (!_setup || fps <= 0f) return;
            var frames = CurrentFrames;
            if (image == null || frames == null || frames.Length <= 1) return;

            _timer += Time.deltaTime;
            if (_timer < 1f / fps) return;
            _timer = 0f;
            AdvanceFrame(frames.Length);
            ApplyFrame();
        }

        private void AdvanceFrame(int count)
        {
            if (_frameIndex < count - 1)
            {
                _frameIndex++;
                return;
            }

            // Reached the last frame — behaviour depends on the state.
            switch (_current)
            {
                case State.Idle:
                case State.Victory:
                    _frameIndex = 0; // loop
                    break;
                case State.Attacking:
                    SetState(State.Idle); // play once, then return to idle
                    break;
                case State.Death:
                    _frameIndex = count - 1; // hold final slumped frame
                    break;
            }
        }
    }
}
