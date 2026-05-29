using UnityEngine;
public abstract class StateMachine<T> : MonoBehaviour where T : System.Enum
{
    protected T CurrentState { get; private set; }

    protected void ChangeState(T newState)
    {
        if (CurrentState.Equals(newState)) return;

        OnExit(CurrentState);
        Debug.Log($"[FSM] {GetType().Name}: {CurrentState} → {newState}");
        CurrentState = newState;
        OnEnter(CurrentState);
    }

    protected abstract void OnUpdate(T state);
    protected abstract void OnEnter(T state);
    protected abstract void OnExit(T state);
    private void Update()
    {
        OnUpdate(CurrentState);
    }
}
