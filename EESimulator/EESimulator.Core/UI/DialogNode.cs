using System;

namespace EESimulator.Core.UI;

/// <summary>
/// Dialog UI node. Represents a modal or popup dialog in the UI hierarchy.
/// Scriptable via Python: e.g. eesim.ui.dialog('edit_dialog').open()
/// </summary>
public class DialogNode : UINode
{
    private string _header = "Dialog";
    private string _message = string.Empty;
    private bool _isModal = true;

    public string Header
    {
        get => _header;
        set
        {
            if (_header != value)
            {
                var old = _header;
                _header = value;
                NotifyModified(nameof(Header), old, value);
            }
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            if (_message != value)
            {
                var old = _message;
                _message = value;
                NotifyModified(nameof(Message), old, value);
            }
        }
    }

    public bool IsModal
    {
        get => _isModal;
        set
        {
            if (_isModal != value)
            {
                var old = _isModal;
                _isModal = value;
                NotifyModified(nameof(IsModal), old, value);
            }
        }
    }

    public event Action<DialogNode>? OnConfirm;
    public event Action<DialogNode>? OnCancel;

    public DialogNode()
    {
        Name = "Dialog";
        Visible = false;
        IsOpen = false;
    }

    public DialogNode(string name, string header = "Dialog") : this()
    {
        Name = name;
        Header = header;
    }

    public void Open()
    {
        Show();
    }

    public void Close()
    {
        Hide();
    }

    public void Confirm()
    {
        OnConfirm?.Invoke(this);
        Close();
    }

    public void Cancel()
    {
        OnCancel?.Invoke(this);
        Close();
    }
}
