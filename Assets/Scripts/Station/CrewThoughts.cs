
namespace DeepStation
{
    /// <summary>
    /// What crew think about in their thought bubbles. Hints come only from things already
    /// visible on screen (a meter, a rattle, a full bed), so a thought never reveals a hidden
    /// system; it just points at what's wrong in the crew member's own words. Edit freely.
    /// </summary>
    public static class CrewThoughts
    {
        static readonly string[] Idle =
        {
            "miss the sun",
            "need coffee",
            "was that a jellyfish?",
            "so quiet down here",
            "I miss rain",
            "the hum is kind of soothing",
            "who ate my snack?",
            "wonder what's for dinner",
            "something glowed out there",
            "my ears just popped",
        };

        static readonly string[] Tireless = { "I could do this all day", "sleep is overrated", "one more shift, easy" };
        static readonly string[] Tinkerer = { "I could tune this thing", "hear that? perfectly balanced", "where's my good wrench?" };
        static readonly string[] QuickNapper = { "power nap later", "I dreamt of pancakes", "five minutes is plenty" };
        static readonly string[] Resting = { "zzz", "five more minutes…", "zzz… fish…" };

        /// <summary>A hint about something wrong that this crew member would notice, or null.</summary>
        public static string Hint(Station station, CrewMember crew)
        {
            var room = crew.Room;
            if (station.SuffocationTime > 0f) return "can't… breathe…";
            if (station.VentingTank != null) return "emergency air? not good";
            if (!crew.IsMoving && room.Hazard == RoomHazard.PressureLeak) return "water's coming in!";
            if (!crew.IsMoving && room.Hazard == RoomHazard.HeatVent) return "it's roasting in here!";
            if (!crew.IsMoving && room.Hazard == RoomHazard.Creature) return "something's at the window…";
            if (!crew.IsMoving && room.Broken && !crew.IsRepairing && crew.RepairCooldown > 0f) return "need a sec before I fix it again";
            if (!crew.IsMoving && room.Broken && !crew.IsRepairing) return "need a second pair of hands!";
            if (!crew.IsMoving && room.Rattling) return "that rattle doesn't sound good";
            if (station.PowerSupply < 0.98f) return "why are the lights flickering?";
            if (station.OxygenLow) return "air feels thin…";
            if (crew.WaitingForBed) return "beds are full… so sleepy";
            if (crew.IsBuilding) return "almost built…";
            if (crew.KeepWorking && !crew.IsResting) return "pushing through!";
            if (station.PowerLow) return "power's running low";
            if (!crew.IsResting && crew.Stamina < station.tuning.tiredStamina) return "need coffee. now.";
            return null;
        }

        /// <summary>Small talk: their trait, resting, or something about life down here.</summary>
        public static string Flavor(CrewMember crew, System.Random rng)
        {
            if (crew.IsResting && crew.Stamina < Station.MaxStamina - 0.01f)
                return Resting[rng.Next(Resting.Length)];
            if (rng.NextDouble() < 0.35)
            {
                var own = crew.Trait == CrewTrait.Tireless ? Tireless : crew.Trait == CrewTrait.Tinkerer ? Tinkerer : QuickNapper;
                return own[rng.Next(own.Length)];
            }
            return Idle[rng.Next(Idle.Length)];
        }

        static readonly string[] Fixed = { "good as new!", "nailed it", "fixed!", "and stay fixed" };

        public static string Repaired(System.Random rng) => Fixed[rng.Next(Fixed.Length)];
    }
}
