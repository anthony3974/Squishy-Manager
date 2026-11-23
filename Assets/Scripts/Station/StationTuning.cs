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
        // ---------------------------------------------------------------- pacing
        // The numbers that set how fast the station runs. Casual pass: everything here is about
        // 2.5x slower than Phase 2 (old values in brackets), with production slowed by the same
        // amount as drains so the balance between rooms is unchanged. Target: with the starting
        // jobs (2 on the Generator, 1 on the Scrubber) the station stays healthy untouched, so a
        // check-in every couple of minutes is plenty.

        [Header("Pacing: drains")]
        [Tooltip("Oxygen each crew member breathes per second, wherever they are. [0.8]")]
        public float breathePerCrew = 0.3f;
        [Tooltip("Stamina (0-100) lost per second while working a Generator or Scrubber. [2.4]")]
        public float workStaminaDrain = 1f;
        [Tooltip("Stamina (0-100) regained per second while resting in Quarters. [5]")]
        public float restStaminaGain = 2.5f;

        [Header("Pacing: breakdowns")]
        [Tooltip("Shortest running time (seconds) before a Generator or Scrubber breaks. Machines only wear while crew work them. Applies from the next repair. [60]")]
        public float breakdownMinSeconds = 85f;
        [Tooltip("Longest running time (seconds) before a Generator or Scrubber breaks. [100]")]
        public float breakdownMaxSeconds = 145f;

        [Header("Pacing: production")]
        [Tooltip("Power per second for each crew member working the Generator. [3]")]
        public float generatorPower = 1.2f;
        [Tooltip("Oxygen per second for each crew member working the Scrubber. [4]")]
        public float scrubberOxygen = 1.7f;
        [Tooltip("Power per second each crew member working the Scrubber draws. [2.5]")]
        public float scrubberPower = 1f;
        [Tooltip("Power each resting crew member draws per second in Quarters. [0.5]")]
        public float quartersPowerPerCrew = 0.2f;
        [Tooltip("Oxygen per second drawn from the air to refill emergency tanks while oxygen is high. [1]")]
        public float tankRefillRate = 0.4f;

        // ---------------------------------------------------------------- everything else

        [Header("Resources")]
        public float maxOxygen = 100f;
        public float maxPower = 100f;
        public float startOxygen = 80f;
        public float startPower = 60f;

        [Header("Crew")]
        [Tooltip("Output multiplier for a crew member at 0 stamina. They keep working, just slower.")]
        [Range(0f, 1f)] public float exhaustedEfficiency = 0.5f;
        [Tooltip("World units per second walking along a floor.")]
        public float walkSpeed = 2.4f;
        [Tooltip("World units per second climbing the ladder.")]
        public float climbSpeed = 1.8f;

        [Header("Crew traits")]
        [Tooltip("Tireless (Mara): stamina drain multiplier while working or repairing.")]
        public float tirelessDrainFactor = 0.75f;
        [Tooltip("Tinkerer (Theo): repair speed multiplier.")]
        public float tinkererRepairFactor = 1.5f;
        [Tooltip("Quick napper (Juno): rest speed multiplier.")]
        public float quickNapperRestFactor = 1.4f;

        [Header("Auto-rest")]
        [Tooltip("Stamina (0-100) at which crew leave their job on their own to rest in Quarters. Crew finish a repair first unless exhausted.")]
        public float autoRestAt = 20f;
        [Tooltip("Stamina (0-100) at which resting crew walk back to their job.")]
        public float autoReturnAt = 100f;

        [Header("Rooms")]
        [Tooltip("Rest speed multiplier in Quarters when there is no power.")]
        [Range(0f, 1f)] public float unpoweredRestFactor = 0.5f;

        [Header("Building")]
        [Tooltip("Power paid up front to build a room in an empty spot.")]
        public float roomPowerCost = 30f;
        [Tooltip("Extra power to dig out a new floor, on top of the room cost.")]
        public float newFloorPowerCost = 20f;
        [Tooltip("Seconds for one rested crew member to build a room. Two crew take half as long; Tinkerer builds faster.")]
        public float buildSeconds = 30f;

        [Header("Breakdowns")]
        [Tooltip("Seconds of warning before a breakdown: the machine rattles and smokes.")]
        public float rattleSeconds = 10f;
        [Tooltip("Crew required in a broken room at the same time to repair it.")]
        [Range(1, 2)] public int repairCrewRequired = 2;
        [Tooltip("Seconds for one rested crew member to repair a machine. Two crew take half as long; exhausted crew work at half speed.")]
        public float repairSeconds = 8f;
        [Tooltip("Stamina drain while repairing, as a multiple of the normal work drain.")]
        public float repairStaminaFactor = 1.25f;
        [Tooltip("Seconds after finishing a repair before that crew member can repair again.")]
        public float repairCooldown = 12f;

        [Header("Depth hazards")]
        [Tooltip("Shortest wait (seconds) between depth hazards once you have rooms on floor 3 or deeper.")]
        public float hazardMinSeconds = 45f;
        [Tooltip("Longest wait (seconds) between depth hazards.")]
        public float hazardMaxSeconds = 80f;
        [Tooltip("Maximum simultaneous hazards across the station.")]
        public int maxActiveHazards = 2;
        [Tooltip("Seconds for one rested crew member to seal a pressure leak.")]
        public float leakFixSeconds = 10f;
        [Tooltip("Oxygen per second lost to an active pressure leak.")]
        public float leakOxygenDrain = 0.8f;
        [Tooltip("Seconds for one rested crew member to vent and cool a heat vent.")]
        public float heatVentFixSeconds = 11f;
        [Tooltip("Stamina drain multiplier for crew inside a room with an active heat vent.")]
        public float heatVentStaminaFactor = 2.5f;
        [Tooltip("Extra wear rate on machines inside a room with an active heat vent.")]
        public float heatVentWearFactor = 2f;
        [Tooltip("Seconds for one rested crew member to drive off a light-attracted creature.")]
        public float creatureFixSeconds = 9f;
        [Tooltip("Power per second drained while a creature clings to a room.")]
        public float creaturePowerDrain = 0.9f;

        [Header("Emergency tanks")]
        [Tooltip("Reserve oxygen canisters under the station. Applies on the next dive.")]
        public int emergencyTanks = 2;
        [Tooltip("Oxygen in one full tank.")]
        public float tankOxygen = 40f;
        [Tooltip("Seconds a tank takes to vent once oxygen hits zero.")]
        public float tankVentSeconds = 8f;
        [Tooltip("Tanks start refilling when oxygen is above this fraction of max, and stop 10% below it.")]
        [Range(0.2f, 1f)] public float tankRefillAbove = 0.9f;

        [Header("Warnings and failure")]
        [Tooltip("Fraction of max at which a resource counts as low and rooms start flashing red.")]
        [Range(0f, 1f)] public float lowWarning = 0.25f;
        [Tooltip("Stamina (0-100) below which crew show as tired.")]
        public float tiredStamina = 25f;
        [Tooltip("Seconds at zero oxygen, with every emergency tank empty, before the crew faint. Also the grace period they get after waking.")]
        public float suffocationSeconds = 20f;
        [Tooltip("Seconds fainted crew stay down before coming round on their own. They can't work meanwhile, but keep their job and stamina.")]
        public float faintSeconds = 30f;
    }
}
