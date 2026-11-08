using System;
using System.Collections.Generic;
using NullFrontier.Player;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class WeaponLoadout : MonoBehaviour
    {
        [SerializeField] private List<Weapon> weapons = new List<Weapon>();
        [SerializeField] private int startIndex;

        public Weapon CurrentWeapon { get; private set; }
        public int CurrentIndex => _currentIndex;

        public event Action<Weapon> OnWeaponChanged;

        private int _currentIndex = -1;
        private IInputProvider _provider;
        private readonly PlayerInputState _input = new PlayerInputState();

        private void Start()
        {
            _provider = FindInputProvider();

            if (weapons.Count == 0)
                weapons.AddRange(GetComponentsInChildren<Weapon>(true));

            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null)
                    weapons[i].gameObject.SetActive(false);
            }

            Equip(Mathf.Clamp(startIndex, 0, Mathf.Max(0, weapons.Count - 1)));
        }

        private void Update()
        {
            _input.Reset();
            _provider?.Poll(_input);

            if (_input.WeaponSwitchPressed && weapons.Count > 0)
                Equip((_currentIndex + 1) % weapons.Count);
        }

        public void Equip(int index)
        {
            if (weapons.Count == 0)
            {
                CurrentWeapon = null;
                _currentIndex = -1;
                OnWeaponChanged?.Invoke(null);
                return;
            }

            index = ((index % weapons.Count) + weapons.Count) % weapons.Count;

            if (CurrentWeapon != null)
                CurrentWeapon.gameObject.SetActive(false);

            _currentIndex = index;
            CurrentWeapon = weapons[index];
            if (CurrentWeapon != null)
                CurrentWeapon.gameObject.SetActive(true);

            OnWeaponChanged?.Invoke(CurrentWeapon);
        }

        private IInputProvider FindInputProvider()
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IInputProvider provider && provider.IsActive)
                    return provider;
            }
            return GetComponent<IInputProvider>();
        }
    }
}
