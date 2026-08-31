// src/Nestify.Web/Services/ToastService.cs
using System.Collections.ObjectModel;

namespace Nestify.Web.Services;

/// <summary>
/// Toast notification service for displaying temporary messages.
/// Implements the toast/notification bell mentioned in §2.
/// </summary>
public sealed class ToastService
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public sealed class Toast
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ToastType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Title { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int DurationMs { get; set; } = 4000;
        public bool IsDismissed { get; set; }
    }

    private readonly List<Toast> _toasts = new();
    
    public event EventHandler? OnToastsChanged;

    /// <summary>
    /// Gets a read-only collection of active toasts.
    /// </summary>
    public IReadOnlyList<Toast> Toasts => _toasts.AsReadOnly();

    /// <summary>
    /// Shows an info toast.
    /// </summary>
    public void ShowInfo(string message, string? title = null, int durationMs = 4000)
    {
        Show(ToastType.Info, message, title, durationMs);
    }

    /// <summary>
    /// Shows a success toast.
    /// </summary>
    public void ShowSuccess(string message, string? title = null, int durationMs = 4000)
    {
        Show(ToastType.Success, message, title, durationMs);
    }

    /// <summary>
    /// Shows a warning toast.
    /// </summary>
    public void ShowWarning(string message, string? title = null, int durationMs = 5000)
    {
        Show(ToastType.Warning, message, title, durationMs);
    }

    /// <summary>
    /// Shows an error toast. Default duration is longer to ensure users can read it.
    /// </summary>
    public void ShowError(string message, string? title = null, int durationMs = 6000)
    {
        Show(ToastType.Error, message, title, durationMs);
    }

    /// <summary>
    /// Shows a generic toast notification.
    /// </summary>
    public void Show(ToastType type, string message, string? title = null, int durationMs = 4000)
    {
        var toast = new Toast
        {
            Type = type,
            Message = message,
            Title = title,
            DurationMs = durationMs
        };

        _toasts.Add(toast);
        OnToastsChanged?.Invoke(this, EventArgs.Empty);

        // Auto-dismiss after duration
        _ = Task.Delay(durationMs).ContinueWith(_ =>
        {
            Dismiss(toast.Id);
        });
    }

    /// <summary>
    /// Dismisses a toast by its ID.
    /// </summary>
    public void Dismiss(string toastId)
    {
        var toast = _toasts.FirstOrDefault(t => t.Id == toastId);
        if (toast != null)
        {
            toast.IsDismissed = true;
            _toasts.Remove(toast);
            OnToastsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Clears all toasts.
    /// </summary>
    public void ClearAll()
    {
        _toasts.Clear();
        OnToastsChanged?.Invoke(this, EventArgs.Empty);
    }
}
