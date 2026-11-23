/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Provides common behavior for descriptor classes.
/// </summary>
public class BaseDef : IDef, IJsonLoadable, INotifyPropertyChanged
{
    // ● protected fields
    /// <summary>
    /// Holds the explicitly assigned descriptor name.
    /// </summary>
    protected string fName;
    /// <summary>
    /// Holds the explicitly assigned title resource key.
    /// </summary>
    protected string fTitleKey;

    // ● protected
    /// <summary>
    /// Raises a property change notification.
    /// </summary>
    protected virtual void NotifyPropertyChanged(string PropertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
    /// <summary>
    /// Returns the title key, falling back to the descriptor name.
    /// </summary>
    protected virtual string GetTitleKey() => !string.IsNullOrWhiteSpace(fTitleKey) ? fTitleKey : Name;

    // ● public
    /// <summary>
    /// Returns the descriptor name.
    /// </summary>
    public override string ToString() => Name;
    /// <summary>
    /// Updates references to other descriptors.
    /// </summary>
    public virtual void UpdateReferences()
    {
    }
    /// <summary>
    /// Updates descriptor references after JSON loading has completed.
    /// </summary>
    public virtual void JsonLoaded() => UpdateReferences();
    /// <summary>
    /// Creates a new instance of the descriptor's runtime type.
    /// </summary>
    public virtual BaseDef CreateNew() => (BaseDef)Activator.CreateInstance(GetType());
    /// <summary>
    /// Checks that a descriptor name has been explicitly assigned.
    /// </summary>
    public virtual void CheckDescriptor()
    {
        if (string.IsNullOrWhiteSpace(fName))
            throw new InvalidOperationException(Texts.GS($"E_{GetType()}_NoName", $"{GetType().Name} must have a name."));
    }
    /// <summary>
    /// Assigns serialized property values from another descriptor.
    /// Keeps this instance and its event subscribers.
    /// </summary>
    public virtual void Assign(IDef Source)
    {
        ArgumentNullException.ThrowIfNull(Source);
        if (ReferenceEquals(this, Source))
            return;
        Json.PopulateObjectCore(this, Json.Serialize(Source));
        if (Source is BaseDef Descriptor)
        {
            Name = Descriptor.fName;
            TitleKey = Descriptor.fTitleKey;
        }
        JsonLoaded();
    }
    /// <summary>
    /// Returns a new descriptor populated with this descriptor's values.
    /// </summary>
    public virtual IDef Clone()
    {
        BaseDef Result = CreateNew();
        Result.Assign(this);
        return Result;
    }
    /// <summary>
    /// Resets serialized property values to those of a new descriptor.
    /// </summary>
    public virtual void Clear() => Assign(CreateNew());

    // ● properties
    /// <summary>
    /// Gets or sets the name, falling back to the runtime type's full name.
    /// </summary>
    public virtual string Name
    {
        get => !string.IsNullOrWhiteSpace(fName) ? fName : GetType().FullName;
        set
        {
            if (fName == value)
                return;
            fName = value;
            NotifyPropertyChanged(nameof(Name));
            NotifyPropertyChanged(nameof(TitleKey));
            NotifyPropertyChanged(nameof(Title));
        }
    }
    /// <summary>
    /// Gets or sets the title resource key, falling back to the name.
    /// </summary>
    public virtual string TitleKey
    {
        get => GetTitleKey();
        set
        {
            if (fTitleKey == value)
                return;
            fTitleKey = value;
            NotifyPropertyChanged(nameof(TitleKey));
            NotifyPropertyChanged(nameof(Title));
        }
    }
    /// <summary>
    /// Gets whether the title key has not been explicitly assigned.
    /// </summary>
    [JsonIgnore]
    public virtual bool IsTitleKeyEmpty => string.IsNullOrWhiteSpace(fTitleKey);
    /// <summary>
    /// Gets the localized title for the descriptor.
    /// </summary>
    [JsonIgnore]
    public virtual string Title => Texts.L(TitleKey);
    /// <summary>
    /// Gets or sets a user-defined value that is not serialized.
    /// </summary>
    [JsonIgnore]
    public virtual object Tag { get; set; }
    /// <summary>
    /// Gets whether this descriptor should be serialized.
    /// </summary>
    [JsonIgnore]
    public virtual bool IsSerializable => true;

    // ● events
    /// <summary>
    /// Occurs when a descriptor property changes.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;
}
