using System.ComponentModel;
using JetBrains.Annotations;
using TrackGenius.Const;

namespace TrackGenius.UI.ViewModels;

public sealed class RaceDataColumnOption : INotifyPropertyChanged
{
    private bool _isVisible;

    public string Key { get; }

    public string Label { get; }

    public bool CanHide { get; }

    /// <summary>Constructor visibility — the baseline a user deviation is measured against.</summary>
    public bool DefaultIsVisible { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            // Non-hideable columns (Position) stay visible.
            if (!CanHide && !value)
                return;

            PropertyChanged.RaiseIfChanged(this, ref _isVisible, value, nameof(IsVisible));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public RaceDataColumnOption([NotNull] string key, [NotNull] string label, bool canHide, bool isVisible = true)
    {
        Key = key ?? throw new System.ArgumentNullException(nameof(key));
        Label = label ?? throw new System.ArgumentNullException(nameof(label));
        CanHide = canHide;
        DefaultIsVisible = isVisible;
        _isVisible = isVisible;
    }
}
