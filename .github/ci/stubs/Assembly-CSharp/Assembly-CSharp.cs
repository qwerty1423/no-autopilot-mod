using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mirage;
using UnityEngine;

public static class CursorManager
{
	public static bool GetFlag(CursorFlags flag) => throw null;
}

public class Pilot : MonoBehaviour, IDamageable
{
	public Aircraft aircraft;
	public PilotType pilotType;
	public PilotPlayerState playerState;

	public void SwitchState(PilotBaseState state) => throw null;

	public Vector3 GetAccel() => throw null;

	public enum PilotType
	{
		FixedWing,
		Helo,
		Tiltwing
	}
}

public class FactionHQ : NetworkBehaviour
{
	public IEnumerable<Airbase> GetAirbases() => throw null;
}

public static class GlobalPositionExtensions
{
	public static GlobalPosition GlobalPosition(this Unit unit) => throw null;
	public static GlobalPosition GlobalPosition(this Transform transform) => throw null;
	public static float GlobalY(this Vector3 v) => throw null;
	public static GlobalPosition ToGlobalPosition(this Vector3 v) => throw null;
}

public static class Vector3Extensions
{
}

public class LevelInfo : NetworkSceneSingleton<LevelInfo>
{
	public static float GetSpeedOfSound(float altitude) => throw null;
}

public static class UnitConverter
{
	public static string AltitudeReading(float altitude) => throw null;
	public static string ClimbRateReading(float speed) => throw null;
	public static string SpeedReading(float speed) => throw null;
	public static string DistanceReading(float distance) => throw null;
}

public class DynamicMap : SceneSingleton<DynamicMap>
{
	public static bool mapMaximized { get; }
	public float mapScaleMinimized;
	public float mapScaleCurrent;
	public float mapDimension;
	public GameObject mapImage;
	public GameObject mapWaypoint;
	public GameObject mapWaypointVector;
	public List<MapIcon> selectedIcons;
	public Vector2 positionOffset;
	public Vector2 stationaryOffset;
	public bool followingCamera;

	public void CenterMap() => throw null;
	public float GetZoomLevel() => throw null;
	public void SetZoomLevel(float zoomLevel) => throw null;
	public void UpdateMap() => throw null;
	public void Minimize() => throw null;
	public void Maximize() => throw null;
	public void CenterMinimizedMap() => throw null;
	public void LoadMapImage() => throw null;
	public bool TryGetCursorCoordinates(out GlobalPosition position) => throw null;
	public static FactionMode GetFactionMode(FactionHQ hq = null, bool checkNoFactionBeforeSpectator = false) => throw null;
}

public struct GlobalPosition : IEquatable<GlobalPosition>
{
	public float x;
	public float y;
	public float z;

	public bool Equals(GlobalPosition other) => throw null;
	public Vector3 AsVector3() => throw null;
}

public class ControlsFilter : MonoBehaviour
{
	public (bool, float[]) GetFlyByWireParameters() => throw null;
	public void SetFlyByWireParameters(bool enabled, float[] parameters) => throw null;
}

public enum CursorFlags
{
	None = 0,
	Pause = 1,
	Map = 2,
	SelectionMenu = 4,
	Dialogue = 8,
	NotInGame = 0x10,
	Chat = 0x20,
	EditorWindow = 0x40,
	Loading = 0x80,
	EmptyScene = 0x100
}

public class UnitPart : MonoBehaviour, IDamageable
{
	public Rigidbody rb;
}

public class Weapon : MonoBehaviour
{
}

public class Aircraft : Unit, IRadarReturn, IRearmable, IRefuelable
{
	public UnitPart cockpit;
	public ControlsFilter controlsFilter;
	public float fuelCapacity;
	public LandingGear.GearState gearState;
	public Pilot[] pilots;
	public PowerSupply powerSupply;
	public WeaponManager weaponManager;

	public float GetFuelLevel() => throw null;
}

public enum FactionMode
{
	NoFaction = 0,
	Spectator = 1,
	Friendly = 2,
	Enemy = 4
}

public class FlightHud : SceneSingleton<FlightHud>
{
	public Aircraft aircraft;

	public Transform GetHUDCenter() => throw null;
	public void SetAircraft(Aircraft aircraft) => throw null;
	public static void EnableCanvas(bool enable) => throw null;
}

public abstract class PilotBaseState
{
	public Pilot pilot;
	public ControlInputs controlInputs;

	public virtual void FixedUpdateState() => throw null;
	public virtual void CheckApproachParameters() => throw null;
}

public class AIPilotLandingState : PilotBaseState
{
	public Airbase.Runway.RunwayUsage runwayUsage;
	public bool touchedDown;

	public void SearchBestAirbase() => throw null;
}

public class GLOC : MonoBehaviour
{
	public float bloodPressure;
	public bool conscious;

	public void ResetGLOC() => throw null;
}

public sealed class Airbase : NetworkBehaviour, IEditorSelectable, ICapturable
{
	public Runway[] runways;

	public void CmdRegisterUsage(Aircraft aircraft, bool isUsing, byte? landingRunway) => throw null;

	public class Runway
	{
		public Airbase airbase;

		public readonly struct RunwayUsage
		{
			public readonly Runway Runway;
			public readonly bool Reverse;
		}
	}
}

public class WeaponManager : MonoBehaviour
{
	public WeaponStation currentWeaponStation;
	public List<Unit> targetList;

	public void Fire() => throw null;
}

public class WeaponStation
{
	public List<Weapon> Weapons;
}

public class JammingPod : MonoBehaviour
{
}

public class PowerSupply : MonoBehaviour
{
	public float charge;
	public float maxCharge;
}

public class AIPilotShortLandingState : PilotBaseState
{
	public Airbase.Runway.RunwayUsage runwayUsage;
	public bool touchedDown;

	public void SearchBestAirbase() => throw null;
}

public class AIPilotTaxiState : PilotBaseState
{
	public Airbase airbase;
}

public class PilotPlayerState : PilotBaseState
{
	public GLOC gloc;

	public void LeaveState() => throw null;
	public void EnterState() => throw null;
	public void PlayerAxisControls() => throw null;
	public void PlayerThrottleAxis1Controls() => throw null;
}

public class ControlInputs
{
	public float throttle;
	public float pitch;
	public float yaw;
	public float roll;
}

public class GridLabels : MonoBehaviour
{
	public void SetupGrid() => throw null;
	public void LateUpdate() => throw null;
}

public class FuelGauge : MonoBehaviour
{
	public UnityEngine.UI.Text fuelLabel;
}

public class MapIcon : MonoBehaviour
{
}

public class UnitMapIcon : MapIcon
{
	public Unit unit { get; }
}

public class CombatHUD : SceneSingleton<CombatHUD>
{
	public Aircraft aircraft;

	public void SetAircraft(Aircraft aircraft) => throw null;
}

public class CameraBaseState { }
public class CameraCockpitState : CameraBaseState { }

public class CameraStateManager : SceneSingleton<CameraStateManager>
{
	public CameraBaseState currentState { get; }
	public CameraCockpitState cockpitState;
}

public class Building : Unit
{
}

public static class LandingGear
{
	public enum GearState
	{
		Uninitialized,
		LockedRetracted,
		LockedExtended,
		Retracting,
		Extending
	}
}

public static class PlayerSettings
{
	public static UnitSystem unitSystem;
	public static float hudTextSize;

	public enum UnitSystem
	{
		Metric,
		Imperial
	}
}

public interface IDamageable { }
public class NetworkSceneSingleton<T> : MonoBehaviour where T : class { public static T i; }
public class SceneSingleton<T> : MonoBehaviour where T : class { public static T i; }
public class Unit : MonoBehaviour
{
	public FactionHQ NetworkHQ { get; set; }
	public bool disabled;
	public float radarAlt;
	public float speed;
}
public interface IRadarReturn { }
public interface IRearmable { }
public interface IRefuelable { }
public interface IEditorSelectable { }
public interface ICapturable { }
