using System.Collections.Generic;
using UnityEngine;

namespace NullFrontier.UI
{
    /// <summary>
    /// Simple UI screen stack. Screens are GameObjects registered by name; only the
    /// top of the stack is visible. Esc/back pops. Works with any canvas layout.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        private readonly Dictionary<string, GameObject> _screens = new();
        private readonly Stack<string> _stack = new();

        private void Awake()
        {
            Instance = this;
            foreach (Transform child in transform)
            {
                _screens[child.name] = child.gameObject;
                child.gameObject.SetActive(false);
            }
        }

        public void Show(string name)
        {
            if (!Find(name)) return;

            if (_stack.Count > 0 && _screens.TryGetValue(_stack.Peek(), out var current))
            {
                current.SetActive(false);
            }
            _stack.Push(name);
            _screens[name].SetActive(true);
        }

        public void Pop()
        {
            if (_stack.Count == 0) return;

            string top = _stack.Pop();
            if (_screens.TryGetValue(top, out var screen)) screen.SetActive(false);

            while (_stack.Count > 0)
            {
                string next = _stack.Peek();
                if (_screens.TryGetValue(next, out var nextScreen))
                {
                    nextScreen.SetActive(true);
                    return;
                }
                _stack.Pop();
            }
        }

        public void PopAll()
        {
            while (_stack.Count > 0) Pop();
        }

        public bool IsOpen(string name) => _stack.Count > 0 && _stack.Peek() == name;

        public bool HasScreen(string name) => Find(name);

        /// <summary>
        /// Screens built by the scene generator are parented after this component's Awake,
        /// so an unknown name triggers one rescan instead of silently failing to open.
        /// </summary>
        private bool Find(string name)
        {
            if (_screens.ContainsKey(name)) return true;

            foreach (Transform child in transform)
            {
                if (_screens.ContainsKey(child.name)) continue;
                _screens[child.name] = child.gameObject;
                child.gameObject.SetActive(false);
            }
            return _screens.ContainsKey(name);
        }
    }
}
