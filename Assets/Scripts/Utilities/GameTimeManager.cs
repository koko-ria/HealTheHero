// GameTimeManager.cs (NEW SCRIPT)
using UnityEngine;
using System.Collections.Generic;

public interface IPausable
{
    void Pause();
    void Resume();
}

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance { get; private set; }

    private List<IPausable> pausableObjects = new List<IPausable>();
    private bool isTimeStopped = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void RegisterPausable(IPausable pausable)
    {
        if (!pausableObjects.Contains(pausable))
        {
            pausableObjects.Add(pausable);
            // If time is already stopped, pause newly registered object immediately
            if (isTimeStopped)
            {
                pausable.Pause();
            }
        }
    }

    public void UnregisterPausable(IPausable pausable)
    {
        pausableObjects.Remove(pausable);
    }

    public void StopTime()
    {
        if (isTimeStopped) return;

        isTimeStopped = true;
        foreach (IPausable obj in pausableObjects)
        {
            obj.Pause();
        }
        // Time.timeScale = 0f; // Optionally pause ALL game logic, but allows Player actions (healing/buffs)
                               // Only use if you want UI to also update based on real time
                               // For this implementation, we manually pause objects.
    }

    public void ResumeTime()
    {
        if (!isTimeStopped) return;

        isTimeStopped = false;
        foreach (IPausable obj in pausableObjects)
        {
            obj.Resume();
        }
        // Time.timeScale = 1f; // Restore if used for StopTime
    }
}