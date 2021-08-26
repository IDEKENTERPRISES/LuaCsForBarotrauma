﻿using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma.Abilities
{
    class CharacterAbilityBountyHunter : CharacterAbility
    {
        private float vitalityPercentage;

        public CharacterAbilityBountyHunter(CharacterAbilityGroup characterAbilityGroup, XElement abilityElement) : base(characterAbilityGroup, abilityElement)
        {
            vitalityPercentage = abilityElement.GetAttributeFloat("vitalitypercentage", 0f);
        }

        protected override void ApplyEffect(object abilityData)
        {
            if (abilityData is Character character)
            {
                Character.GiveMoney((int)(vitalityPercentage * character.MaxVitality));
            }
        }
    }
}
