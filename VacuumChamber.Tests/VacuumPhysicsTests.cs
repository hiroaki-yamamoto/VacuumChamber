using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VacuumChamber.Tests;

/// <summary>
/// Unit test suite for <see cref="VacuumPhysics"/> and <see cref="ChamberState"/>.
/// </summary>
[TestClass]
public class VacuumPhysicsTests
{
  private static void AssertClose(double actual, double expected, double tol, string message)
  {
    Assert.IsTrue(
      Math.Abs(actual - expected) <= tol,
      $"{message} (expected {expected:G9}, actual {actual:G9}, tol {tol:G3})");
  }

  [TestMethod]
  public void TestRoomVolume()
  {
    AssertClose(VacuumPhysics.ComputeRoomVolumeM3(20), 50.0, 1e-9, "20 tiles * 2.5 m = 50 m³");
    AssertClose(VacuumPhysics.ComputeRoomVolumeM3(1), 2.5, 1e-9, "1 tile * 2.5 m = 2.5 m³");
    AssertClose(VacuumPhysics.ComputeRoomVolumeM3(0), 0.0, 1e-9, "0 tiles = 0 m³");
    AssertClose(VacuumPhysics.ComputeRoomVolumeM3(-5), 0.0, 1e-9, "negative tiles = 0 m³");
    AssertClose(VacuumPhysics.ComputeRoomVolumeM3(20, double.NaN), 0.0, 1e-9, "NaN ceiling height = 0 m³");
    AssertClose(VacuumPhysics.ComputeRoomVolumeM3(20, double.PositiveInfinity), 0.0, 1e-9, "Inf ceiling height = 0 m³");
  }

  [TestMethod]
  public void TestPumpingSpeedAndPower()
  {
    AssertClose(VacuumPhysics.ComputePumpingSpeedM3PerHour(1), 100.0, 1e-9, "Lv 1 = 100 m³/h");
    AssertClose(VacuumPhysics.ComputePumpingSpeedM3PerHour(50), 5000.0, 1e-9, "Lv 50 = 5000 m³/h");
    AssertClose(VacuumPhysics.ComputePumpingSpeedM3PerHour(5, double.NaN), 0.0, 1e-9, "NaN speedPerLevel = 0 m³/h");
    AssertClose(VacuumPhysics.ComputePowerDrawWatts(1), 250.0, 1e-6, "Lv 1 = 250 W");
    AssertClose(VacuumPhysics.ComputePowerDrawWatts(50), 12500.0, 1e-6, "Lv 50 = 12500 W");
    AssertClose(VacuumPhysics.ComputePowerDrawWatts(0), 0.0, 1e-6, "Lv 0 = 0 W");
    AssertClose(VacuumPhysics.ComputePowerDrawWatts(5, double.PositiveInfinity), 0.0, 1e-6, "Inf powerPerLevel = 0 W");
  }

  [TestMethod]
  public void TestClamping()
  {
    Assert.AreEqual(0, VacuumPhysics.ClampTargetVacuumPercent(-10), "Clamp target < 0 -> 0");
    Assert.AreEqual(0, VacuumPhysics.ClampTargetVacuumPercent(0), "Clamp target 0 -> 0");
    Assert.AreEqual(42, VacuumPhysics.ClampTargetVacuumPercent(42), "Clamp target 42 -> 42");
    Assert.AreEqual(100, VacuumPhysics.ClampTargetVacuumPercent(150), "Clamp target > 100 -> 100");

    Assert.AreEqual(1, VacuumPhysics.ClampPumpingSpeedLevel(0), "Clamp speed < 1 -> 1");
    Assert.AreEqual(1, VacuumPhysics.ClampPumpingSpeedLevel(1), "Clamp speed 1 -> 1");
    Assert.AreEqual(50, VacuumPhysics.ClampPumpingSpeedLevel(50), "Clamp speed 50 -> 50");
    Assert.AreEqual(50, VacuumPhysics.ClampPumpingSpeedLevel(99), "Clamp speed > 50 -> 50");
  }

  [TestMethod]
  public void TestParseClampedInt()
  {
    Assert.AreEqual(42, VacuumPhysics.ParseClampedInt("42", 10, 0, 100), "Valid integer in range");
    Assert.AreEqual(0, VacuumPhysics.ParseClampedInt("-25", 10, 0, 100), "Below min clamps to min");
    Assert.AreEqual(100, VacuumPhysics.ParseClampedInt("250", 10, 0, 100), "Above max clamps to max");
    Assert.AreEqual(10, VacuumPhysics.ParseClampedInt("abc", 10, 0, 100), "Invalid text uses fallback");
    Assert.AreEqual(50, VacuumPhysics.ParseClampedInt(null, 99, 1, 50), "Null text clamps fallback to max");
    Assert.AreEqual(1, VacuumPhysics.ParseClampedInt("", -5, 1, 50), "Empty text clamps fallback to min");
    Assert.AreEqual(5, VacuumPhysics.ParseClampedInt("10", 10, 5, 2), "Inverted bounds clamp to minValue");
  }

  [TestMethod]
  public void TestIsPressurizing()
  {
    Assert.IsTrue(VacuumPhysics.IsPressurizing(0.8f, 0.5f), "Current > target -> pressurizing");
    Assert.IsFalse(VacuumPhysics.IsPressurizing(0.5f, 0.5f), "Current == target -> not pressurizing");
    Assert.IsFalse(VacuumPhysics.IsPressurizing(0.2f, 0.5f), "Current < target -> depressurizing");
    Assert.IsFalse(VacuumPhysics.IsPressurizing(float.NaN, 0.5f), "NaN current -> not pressurizing");
    Assert.IsFalse(VacuumPhysics.IsPressurizing(0.8f, float.NaN), "NaN target -> not pressurizing");
  }

  [TestMethod]
  public void TestStatePriority()
  {
    Assert.AreEqual(
      ChamberState.Unpowered,
      VacuumPhysics.EvaluateState(isPowered: false, isRoomSealedIndoors: false),
      "Unpowered + outdoors -> Unpowered takes priority");
    Assert.AreEqual(
      ChamberState.Unpowered,
      VacuumPhysics.EvaluateState(isPowered: false, isRoomSealedIndoors: true),
      "Unpowered + indoors -> Unpowered");
    Assert.AreEqual(
      ChamberState.Inoperable,
      VacuumPhysics.EvaluateState(isPowered: true, isRoomSealedIndoors: false),
      "Powered + outdoors -> Inoperable");
    Assert.AreEqual(
      ChamberState.Operational,
      VacuumPhysics.EvaluateState(isPowered: true, isRoomSealedIndoors: true),
      "Powered + indoors -> Operational");
  }

  [TestMethod]
  public void TestIso3529SingleStep()
  {
    // 4x5 room = 20 tiles -> V = 50 m³, Lv 1 -> S = 100 m³/h, dt = 0.1 h
    // Depressurize: v_next = 1 - exp(-(100 / 50) * 0.1) = 1 - exp(-0.2) ≈ 0.1812692469
    float nextDepressurize = VacuumPhysics.ComputeNextVacuum(
      0f,
      1f,
      roomCellCount: 20,
      pumpingSpeedLevel: 1);
    AssertClose(
      nextDepressurize,
      1.0 - Math.Exp(-0.2),
      1e-5,
      "ISO 3529 single step depressurization (20 cells, Lv 1)");

    // Pressurize: v_next = 1 * exp(-0.2) ≈ 0.8187307531
    float nextPressurizeFull = VacuumPhysics.ComputeNextVacuum(
      1f,
      0f,
      roomCellCount: 20,
      pumpingSpeedLevel: 1);
    AssertClose(
      nextPressurizeFull,
      Math.Exp(-0.2),
      1e-5,
      "ISO 3529 single step pressurization from 100% (20 cells, Lv 1)");

    // Partial pressurize: 0.80 -> 0.50 => 0.80 * exp(-0.2) ≈ 0.6549846025
    float nextPressurizePartial = VacuumPhysics.ComputeNextVacuum(
      0.80f,
      0.50f,
      roomCellCount: 20,
      pumpingSpeedLevel: 1);
    AssertClose(
      nextPressurizePartial,
      0.80 * Math.Exp(-0.2),
      1e-5,
      "ISO 3529 single step partial pressurization (0.80 -> 0.50)");
  }

  [TestMethod]
  public void TestTargetBounds()
  {
    float cappedHigh = VacuumPhysics.ComputeNextVacuum(0f, 0.10f, roomCellCount: 20, pumpingSpeedLevel: 1);
    AssertClose(cappedHigh, 0.10f, 1e-6, "Depressurization never overshoots targetVacuum");

    float cappedLow = VacuumPhysics.ComputeNextVacuum(0.80f, 0.70f, roomCellCount: 20, pumpingSpeedLevel: 10);
    AssertClose(cappedLow, 0.70f, 1e-6, "Pressurization never undershoots targetVacuum");

    float atTarget = VacuumPhysics.ComputeNextVacuum(0.50f, 0.50f, roomCellCount: 20, pumpingSpeedLevel: 1);
    AssertClose(atTarget, 0.50f, 1e-6, "No change when currentVacuum == targetVacuum");
  }

  [TestMethod]
  public void TestNonFiniteAndExtremeInputs()
  {
    Assert.AreEqual(0f, VacuumPhysics.ComputeNextVacuum(float.NaN, 1f, 20, 1), "NaN currentVacuum returns 0");
    Assert.AreEqual(0.5f, VacuumPhysics.ComputeNextVacuum(0.5f, float.NaN, 20, 1), "NaN targetVacuum preserves clamped current");
    Assert.AreEqual(0.3f, VacuumPhysics.ComputeNextVacuum(0.3f, 1f, 20, 1, deltaHours: float.NaN), "NaN deltaHours makes no change");
    Assert.AreEqual(0.3f, VacuumPhysics.ComputeNextVacuum(0.3f, 1f, 20, 1, ceilingHeightMeters: float.PositiveInfinity), "Inf ceilingHeight makes no change");
    Assert.AreEqual(0.3f, VacuumPhysics.ComputeNextVacuum(0.3f, 1f, 20, 1, speedPerLevel: float.NegativeInfinity), "-Inf speedPerLevel makes no change");

    // Extreme decay underflow: 1-tile room (2.5 m³), Lv 50 (5000 m³/h), dt = 10 h -> exp(-20000) == 0
    Assert.AreEqual(0.75f, VacuumPhysics.ComputeNextVacuum(0f, 0.75f, 1, 50, deltaHours: 10f), "Extreme depressurization clamps to target");
    Assert.AreEqual(0.25f, VacuumPhysics.ComputeNextVacuum(1f, 0.25f, 1, 50, deltaHours: 10f), "Extreme pressurization clamps to target");
  }

  [TestMethod]
  public void TestAsymptoticSnap()
  {
    float snappedHigh = VacuumPhysics.ComputeNextVacuum(0.99995f, 1f, roomCellCount: 20, pumpingSpeedLevel: 1);
    Assert.AreEqual(1f, snappedHigh, "Asymptotic snap reaches exact 100% during depressurization");

    float snappedZero = VacuumPhysics.ComputeNextVacuum(0.00005f, 0f, roomCellCount: 20, pumpingSpeedLevel: 1);
    Assert.AreEqual(0f, snappedZero, "Asymptotic snap reaches exact 0% during pressurization");

    float snappedPartial = VacuumPhysics.ComputeNextVacuum(0.50005f, 0.5f, roomCellCount: 20, pumpingSpeedLevel: 1);
    AssertClose(snappedPartial, 0.5f, 1e-6, "Snap tolerance applies cleanly to partial pressurization targets");

    float vDepressurize = 0f;
    for (int i = 0; i < 60; i++)
    {
      vDepressurize = VacuumPhysics.ComputeNextVacuum(vDepressurize, 1f, roomCellCount: 20, pumpingSpeedLevel: 1);
    }
    Assert.AreEqual(1f, vDepressurize, "Repeated depressurization converges cleanly to exact 100%");

    float vPressurize = 1f;
    for (int i = 0; i < 60; i++)
    {
      vPressurize = VacuumPhysics.ComputeNextVacuum(vPressurize, 0f, roomCellCount: 20, pumpingSpeedLevel: 1);
    }
    Assert.AreEqual(0f, vPressurize, "Repeated pressurization converges cleanly to exact 0%");
  }

  [TestMethod]
  public void TestMultiChamberComposition()
  {
    // Two chambers (Lv 2 and Lv 3) sequentially in same tick == single Lv 5 chamber
    float afterFirstDep = VacuumPhysics.ComputeNextVacuum(0.15f, 1f, roomCellCount: 40, pumpingSpeedLevel: 2);
    float afterSecondDep = VacuumPhysics.ComputeNextVacuum(afterFirstDep, 1f, roomCellCount: 40, pumpingSpeedLevel: 3);
    float combinedDep = VacuumPhysics.ComputeNextVacuum(0.15f, 1f, roomCellCount: 40, pumpingSpeedLevel: 5);
    AssertClose(afterSecondDep, combinedDep, 1e-5, "Sequential multi-chamber matches parallel ISO 3529 depressurization");

    float afterFirstPress = VacuumPhysics.ComputeNextVacuum(0.85f, 0f, roomCellCount: 40, pumpingSpeedLevel: 2);
    float afterSecondPress = VacuumPhysics.ComputeNextVacuum(afterFirstPress, 0f, roomCellCount: 40, pumpingSpeedLevel: 3);
    float combinedPress = VacuumPhysics.ComputeNextVacuum(0.85f, 0f, roomCellCount: 40, pumpingSpeedLevel: 5);
    AssertClose(afterSecondPress, combinedPress, 1e-5, "Sequential multi-chamber matches parallel ISO 3529 pressurization");
  }

  [TestMethod]
  public void TestLargeRoomSubOnePercentAccumulation()
  {
    // 1000-tile room (V = 2500 m³), Lv 1 (S = 100 m³/h) -> ~0.3992% per rare tick
    float v = 0f;
    float step1 = VacuumPhysics.ComputeNextVacuum(v, 1f, roomCellCount: 1000, pumpingSpeedLevel: 1);
    Assert.IsTrue(step1 > 0f && step1 < 0.01f, "Large room depressurization step is positive and < 1%");

    v = step1;
    for (int i = 0; i < 10; i++)
    {
      float next = VacuumPhysics.ComputeNextVacuum(v, 1f, roomCellCount: 1000, pumpingSpeedLevel: 1);
      Assert.IsTrue(next > v, $"Large room vacuum strictly increases on step {i + 2}");
      v = next;
    }
    Assert.IsTrue(v > 0.04f, "Accumulated raw vacuum crosses 1% cleanly across ticks");

    // Pressurization in 1000-tile room from 5% toward 0%
    float vp = 0.05f;
    float pStep1 = VacuumPhysics.ComputeNextVacuum(vp, 0f, roomCellCount: 1000, pumpingSpeedLevel: 1);
    Assert.IsTrue(pStep1 < vp && vp - pStep1 < 0.01f, "Large room pressurization step decreases by < 1%");

    vp = pStep1;
    for (int i = 0; i < 60; i++)
    {
      float next = VacuumPhysics.ComputeNextVacuum(vp, 0f, roomCellCount: 1000, pumpingSpeedLevel: 1);
      Assert.IsTrue(next < vp, $"Large room vacuum strictly decreases on pressurization step {i + 2}");
      vp = next;
    }
    Assert.IsTrue(vp < 0.04f, "Accumulated pressurization reduces vacuum cleanly across ticks");
  }

  [TestMethod]
  public void TestThreadSafetyParallel()
  {
    double expectedDep = 1.0 - Math.Exp(-0.2);
    double expectedPress = Math.Exp(-0.2);
    int failures = 0;
    Parallel.For(0, 10000, i =>
    {
      float rDep = VacuumPhysics.ComputeNextVacuum(0f, 1f, 20, 1);
      float rPress = VacuumPhysics.ComputeNextVacuum(1f, 0f, 20, 1);
      int parsed = VacuumPhysics.ParseClampedInt("42", 0, 0, 100);
      bool pressurizing = VacuumPhysics.IsPressurizing(0.8f, 0.2f);
      if (Math.Abs(rDep - expectedDep) > 1e-5
        || Math.Abs(rPress - expectedPress) > 1e-5
        || parsed != 42
        || !pressurizing)
      {
        Interlocked.Increment(ref failures);
      }
    });
    Assert.AreEqual(0, failures, "10,000 concurrent calls produce deterministic results");
  }
}
