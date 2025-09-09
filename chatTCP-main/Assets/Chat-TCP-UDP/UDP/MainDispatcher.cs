using System;
using System.Collections.Generic;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    static MainThreadDispatcher _instance;
    public static MainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("MainThreadDispatcher");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<MainThreadDispatcher>();
            }
            return _instance;
        }
    }

    readonly Queue<Action> actions = new Queue<Action>();
    readonly object queueLock = new object();

    public void Enqueue(Action a)
    {
        if (a == null) return;
        lock (queueLock) actions.Enqueue(a);
    }

    void Update()
    {
        lock (queueLock)
        {
            while (actions.Count > 0)
            {
                try { actions.Dequeue()?.Invoke(); }
                catch (Exception ex) { Debug.LogError("Dispatcher action error: " + ex); }
            }
        }
    }
}
