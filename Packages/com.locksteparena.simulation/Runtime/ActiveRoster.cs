using System;
using System.Collections.Generic;

namespace LockstepArena.Simulation
{
    public sealed class ActiveRoster
    {
        private readonly PlayerId[] _playerIds;

        public ActiveRoster(IReadOnlyList<PlayerId> playerIdsInSlotOrder)
        {
            if (playerIdsInSlotOrder is null)
            {
                throw new ArgumentNullException(nameof(playerIdsInSlotOrder));
            }

            if (playerIdsInSlotOrder.Count == 0)
            {
                throw new ArgumentException("Active roster must contain at least one player.", nameof(playerIdsInSlotOrder));
            }

            _playerIds = new PlayerId[playerIdsInSlotOrder.Count];
            for (int index = 0; index < playerIdsInSlotOrder.Count; index++)
            {
                PlayerId candidate = playerIdsInSlotOrder[index];
                for (int previous = 0; previous < index; previous++)
                {
                    if (_playerIds[previous] == candidate)
                    {
                        throw new ArgumentException(
                            "Active roster cannot contain duplicate PlayerIds.",
                            nameof(playerIdsInSlotOrder));
                    }
                }

                _playerIds[index] = candidate;
            }
        }

        public int Count => _playerIds.Length;

        public PlayerId GetPlayerId(PlayerSlot slot)
        {
            ValidateSlot(slot);
            return _playerIds[slot.Value];
        }

        public bool TryGetSlot(PlayerId playerId, out PlayerSlot slot)
        {
            for (int index = 0; index < _playerIds.Length; index++)
            {
                if (_playerIds[index] == playerId)
                {
                    slot = new PlayerSlot(index);
                    return true;
                }
            }

            slot = default;
            return false;
        }

        public bool HasSameStructure(ActiveRoster other)
        {
            if (other is null || Count != other.Count)
            {
                return false;
            }

            for (int index = 0; index < _playerIds.Length; index++)
            {
                if (_playerIds[index] != other._playerIds[index])
                {
                    return false;
                }
            }

            return true;
        }

        private void ValidateSlot(PlayerSlot slot)
        {
            if (slot.Value >= _playerIds.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "Player slot is outside the active roster.");
            }
        }
    }
}
