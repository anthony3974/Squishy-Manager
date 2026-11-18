using System;
using UnityEngine;

namespace DeepStation
{
    /// <summary>
    /// Every balance number in one place. Select "Deep-Sea Station" in the Hierarchy during Play
    /// to tweak these live; values are read every tick.
    /// </summary>
    [Serializable]
    public class StationTuning
    {
        [Header("Resources")]
        public float maxOxygen = 100f;
        public float maxPower = 100f;
        public float startOxygen = 80f;
        public float startPower = 60f;

        [Header("Crew")]
        [Tooltip("Oxygen each crew member breathes per second, wherever they are.")]
        public float breathePerCrew = 0.8f;
        [Tooltip("Stamina (0-100) lost per second while working a Generator or Scrubber.")]
        public float workStaminaDrain = 3f;
        [Tooltip("Stamina (0-100) regained per second while resting in Quarters.")]
        public float restStaminaGain = 4f;
        [Tooltip("Output multiplier for a crew member at 0 stamina. They keep working, just slower.")]
        [Range(0f, 1f)] public float exhaustedEfficiency = 0.5f;
        [Tooltip("World units per second walking along a floor.")]
        public float walkSpeed = 2.4f;
        [Tooltip("World units per second climbing the ladder.")]
        public float climbSpeed = 1.8f;

        [Header("Rooms")]
        [Tooltip("Power per second for each crew member working the Generator.")]
        public float generatorPower = 3f;
        [Tooltip("Oxygen per second for each crew member working the Scrubber.")]
        public float scrubberOxygen = 4f;
        [Tooltip("Power per second each crew member working the Scrubber draws.")]
        public float scrubberPower = 2.5f;
        [Tooltip("Power each resting crew member draws per second in Quarters.")]
        public float quartersPowerPerCrew = 0.5f;
        [Tooltip("Rest speed multiplier in Quarters when there is no power.")]
        [Range(0f, 1f)] public float unpoweredRestFactor = 0.5f;

        [Header("Warnings and failure")]
        [Tooltip("Fraction of max at which a resource counts as low and rooms start flashing red.")]
        [Range(0f, 1f)] public float lowWarning = 0.25f;
        [Tooltip("Stamina (0-100) below which crew show as tired.")]
        public float tiredStamina = 25f;
        [Tooltip("Seconds at zero oxygen before the station is lost.")]
        public float suffocationSeconds = 20f;
        [Tooltip("Seconds the 'station lost' message shows before a fresh dive starts.")]
        public float restartDelay = 4f;
    }
}
