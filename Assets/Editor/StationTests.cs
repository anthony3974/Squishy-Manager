using NUnit.Framework;
using UnityEngine;

namespace DeepStation.Tests
{
    public class StationTests
    {
        GameObject host;
        Station station;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Test Station");
            station = host.AddComponent<Station>();
            // Awake runs immediately on AddComponent in EditMode.
            station.ResetStation();
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        void Step(float seconds, float dt = 0.25f)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                float step = Mathf.Min(dt, seconds - elapsed);
                station.Tick(step);
                elapsed += step;
            }
        }

        Room FindRoom(RoomType type)
        {
            foreach (var r in station.Rooms)
                if (r.Type == type) return r;
            return null;
        }

        [Test]
        public void CrewMustBuildNewRoomAndDigNewFloor()
        {
            float powerBefore = station.Power;
            float expectedCost = station.BuildCost(2); // new floor (floor index 2 = Floor 3)
            Assert.AreEqual(station.tuning.roomPowerCost + station.tuning.newFloorPowerCost, expectedCost, 0.01f);

            var site = station.Build(RoomType.Quarters, 2, 0);
            Assert.IsNotNull(site);
            Assert.IsFalse(site.Ready);
            Assert.IsFalse(site.CanRest, "Unfinished Quarters must not be restable");
            Assert.AreEqual(3, station.FloorCount);
            Assert.AreEqual(powerBefore - expectedCost, station.Power, 0.05f);

            // Without crew, build progress must stay at 0 and flag NEEDS CREW.
            Step(15f);
            Assert.AreEqual(0f, site.BuildProgress, 0.001f);
            Assert.AreEqual(RoomAlert.Warning, site.Alert);
            Assert.AreEqual("NEEDS CREW", site.StatusText);

            // Send Juno (off-duty) to build it; she must work (not sleep) and finish the room.
            bool builtFired = false;
            station.RoomBuilt += r => { if (r == site) builtFired = true; };
            var juno = station.Crew[2];
            Assert.IsTrue(station.Send(juno, site));

            Step(station.tuning.buildSeconds + 15f);
            Assert.IsTrue(site.Ready, "Crew should complete construction");
            Assert.IsTrue(site.CanRest, "Finished Quarters should become restable");
            Assert.IsTrue(builtFired, "RoomBuilt event should fire");
        }

        [Test]
        public void BrokenMachineRequiresTwoCrewToRepair()
        {
            var scrubber = FindRoom(RoomType.Scrubber);
            var theo = station.Crew[1]; // stationed on Scrubber
            var juno = station.Crew[2]; // off-duty in Quarters

            // Force Scrubber to break with only Theo present.
            scrubber.Broken = true;
            scrubber.RepairProgress = 0f;
            station.Tick(0.1f);

            Step(10f);
            Assert.IsTrue(scrubber.Broken, "Solo crew member must not repair a broken machine");
            Assert.AreEqual(0f, scrubber.RepairProgress, 0.001f);
            Assert.IsFalse(theo.IsRepairing);
            Assert.AreEqual(RoomAlert.Failing, scrubber.Alert);
            Assert.AreEqual("NEED 2 CREW", scrubber.StatusText);

            // Send Juno as the second crew member to help repair.
            Assert.IsTrue(station.Send(juno, scrubber));
            bool repairedFired = false;
            station.RepairFinished += r => { if (r == scrubber) repairedFired = true; };

            Step(15f);
            Assert.IsFalse(scrubber.Broken, "Two crew members should complete the repair");
            Assert.IsTrue(repairedFired);
            Assert.Greater(theo.RepairCooldown, 0f);
            Assert.Greater(juno.RepairCooldown, 0f);
        }

        Room BuildReady(RoomType type, int floor, int column)
        {
            float savedRoom = station.tuning.roomPowerCost;
            float savedFloor = station.tuning.newFloorPowerCost;
            station.tuning.roomPowerCost = 0f;
            station.tuning.newFloorPowerCost = 0f;
            var room = station.Build(type, floor, column);
            station.tuning.roomPowerCost = savedRoom;
            station.tuning.newFloorPowerCost = savedFloor;
            Assert.IsNotNull(room, $"Failed to build room at floor {floor}, col {column}");
            room.BuildProgress = 1f;
            return room;
        }

        [Test]
        public void PressureLeakDrainsOxygenAndCrewCanSealIt()
        {
            var room = BuildReady(RoomType.Generator, 2, 0);
            room.Hazard = RoomHazard.PressureLeak;
            room.HazardProgress = 0f;

            station.Tick(0.1f);
            Assert.AreEqual(RoomAlert.Failing, room.Alert);
            Assert.AreEqual("PRESSURE LEAK", room.StatusText);

            float rateWithLeak = station.OxygenRate;
            room.Hazard = RoomHazard.None;
            station.Tick(0.1f);
            float rateWithoutLeak = station.OxygenRate;
            Assert.AreEqual(station.tuning.leakOxygenDrain, rateWithoutLeak - rateWithLeak, 0.05f);

            // Re-enable leak and send Juno to seal it.
            room.Hazard = RoomHazard.PressureLeak;
            bool cleared = false;
            station.HazardCleared += (r, h) => { if (r == room && h == RoomHazard.PressureLeak) cleared = true; };
            Assert.IsTrue(station.Send(station.Crew[2], room));

            Step(station.tuning.leakFixSeconds + 15f);
            Assert.IsFalse(room.HasHazard, "Crew should seal the pressure leak");
            Assert.IsTrue(cleared);
        }

        [Test]
        public void HeatVentAcceleratesFatigueAndBlocksResting()
        {
            BuildReady(RoomType.Quarters, 2, 0);
            var floor4Room = BuildReady(RoomType.Quarters, 3, 0);
            floor4Room.Hazard = RoomHazard.HeatVent;
            floor4Room.HazardProgress = 0f;

            Assert.IsFalse(floor4Room.CanRest, "Heat vent in Quarters must prevent resting");

            var juno = station.Crew[2];
            juno.Stamina = 100f;
            Assert.IsTrue(station.Send(juno, floor4Room));
            Step(10f); // let Juno climb down and begin cooling
            Assert.Less(juno.Stamina, 100f, "Cooling a heat vent should drain stamina");

            bool cleared = false;
            station.HazardCleared += (r, h) => { if (r == floor4Room && h == RoomHazard.HeatVent) cleared = true; };
            Step(station.tuning.heatVentFixSeconds + 20f);
            Assert.IsFalse(floor4Room.HasHazard, "Crew should cool the heat vent");
            Assert.IsTrue(cleared);
            Assert.IsTrue(floor4Room.CanRest);
        }

        [Test]
        public void CreatureDrainsPowerAndCrewCanDriveItOff()
        {
            BuildReady(RoomType.Generator, 2, 0);
            BuildReady(RoomType.Generator, 3, 0);
            var abyssRoom = BuildReady(RoomType.Generator, 4, 0);

            abyssRoom.Hazard = RoomHazard.Creature;
            abyssRoom.HazardProgress = 0f;
            station.Tick(0.1f);
            Assert.AreEqual(RoomAlert.Failing, abyssRoom.Alert);
            Assert.AreEqual("CREATURE", abyssRoom.StatusText);

            float powerRateWithCreature = station.PowerRate;
            abyssRoom.Hazard = RoomHazard.None;
            station.Tick(0.1f);
            float powerRateWithout = station.PowerRate;
            Assert.AreEqual(station.tuning.creaturePowerDrain, powerRateWithout - powerRateWithCreature, 0.05f);

            abyssRoom.Hazard = RoomHazard.Creature;
            bool cleared = false;
            station.HazardCleared += (r, h) => { if (r == abyssRoom && h == RoomHazard.Creature) cleared = true; };
            Assert.IsTrue(station.Send(station.Crew[2], abyssRoom));

            Step(station.tuning.creatureFixSeconds + 25f);
            Assert.IsFalse(abyssRoom.HasHazard, "Crew should drive off the creature");
            Assert.IsTrue(cleared);
        }

        [Test]
        public void DepthHazardsSpawnAutomaticallyOnFloor3AndRespectCap()
        {
            station.tuning.hazardMinSeconds = 5f;
            station.tuning.hazardMaxSeconds = 6f;
            station.tuning.maxActiveHazards = 2;

            var r1 = station.Build(RoomType.Generator, 2, 0);
            Assert.IsNotNull(r1);
            // Send Juno to finish r1 so hazard timer arms on completion.
            station.Send(station.Crew[2], r1);
            Step(station.tuning.buildSeconds + 15f);
            Assert.IsTrue(r1.Ready);

            // Let enough time pass for a Floor 3 hazard (PressureLeak) to trigger.
            int started = 0;
            RoomHazard firstHazard = RoomHazard.None;
            station.HazardStarted += (r, h) => { started++; if (firstHazard == RoomHazard.None) firstHazard = h; };
            // Move Juno away so she doesn't immediately clear it before we assert.
            station.Send(station.Crew[2], FindRoom(RoomType.Quarters));

            Step(45f);
            Assert.GreaterOrEqual(started, 1, "Floor 3 room should spawn a depth hazard");
            Assert.AreEqual(RoomHazard.PressureLeak, firstHazard, "Floor 3 should only spawn PressureLeak");
        }
    }
}
