using UnityEngine;
using UnityEngine.Events;

public class LapCounter : MonoBehaviour
{
    [Min(1)]
    public int totalLaps = 3;
    public UnityEvent<int> lapCompleted;
    public UnityEvent raceCompleted;

    public int CurrentLap { get; private set; } = 1;
    public bool RaceFinished { get; private set; }

    bool hasLeftStart;

    public void NotifyDistanceAdvanced(
        float previousDistance,
        float currentDistance,
        float pathLength)
    {
        if (RaceFinished || pathLength <= 0f) return;

        if (!hasLeftStart && currentDistance > pathLength * 0.1f)
            hasLeftStart = true;

        bool crossedStart = hasLeftStart && currentDistance < previousDistance;
        if (!crossedStart) return;

        CurrentLap++;
        if (CurrentLap > totalLaps)
        {
            RaceFinished = true;
            raceCompleted?.Invoke();
            return;
        }

        lapCompleted?.Invoke(CurrentLap);
    }
}
