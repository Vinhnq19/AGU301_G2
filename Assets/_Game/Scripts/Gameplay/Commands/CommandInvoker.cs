using System.Collections.Generic;
using UnityEngine;

public class CommandInvoker : MonoBehaviour
{
    private readonly Stack<Command> _undoStack = new Stack<Command>();
    private readonly Stack<Command> _redoStack = new Stack<Command>();

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public void ExecuteCommand(Command command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            Debug.Log("[CommandInvoker] Undo — không có command.");
            return;
        }

        Command command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            Debug.Log("[CommandInvoker] Redo — không có command.");
            return;
        }

        Command command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
    }

    public void ClearHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        Debug.Log("[CommandInvoker] Đã xóa undo/redo stack.");
    }
}
