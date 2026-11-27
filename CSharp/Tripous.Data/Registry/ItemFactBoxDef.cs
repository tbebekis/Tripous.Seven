/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Defines a read-only contextual FactBox for a data module item.
/// </summary>
public class ItemFactBoxDef : BaseDef
{
    // ● public
    /// <summary>
    /// Creates the provider responsible for gathering the FactBox data.
    /// </summary>
    public ItemFactBoxProvider CreateProvider() => TypeStore.CreateInstance<ItemFactBoxProvider>(ProviderClassName);
    /// <summary>
    /// Validates the FactBox definition and its provider type.
    /// </summary>
    public override void CheckDescriptor()
    {
        base.CheckDescriptor();
        Type Provider = TypeStore.Find(ProviderClassName);
        if (Provider == null || !typeof(ItemFactBoxProvider).IsAssignableFrom(Provider)
            || Provider.IsAbstract || Provider.ContainsGenericParameters || Provider.GetConstructor(Type.EmptyTypes) == null)
            throw new InvalidOperationException($"FactBox '{Name}' refers to an invalid provider '{ProviderClassName}'.");
    }

    // ● properties
    /// <summary>
    /// Gets or sets the provider class name.
    /// </summary>
    public string ProviderClassName
    {
        get => field;
        set { if (field != value) { field = value; NotifyPropertyChanged(nameof(ProviderClassName)); } }
    }
    /// <summary>
    /// Gets or sets whether the FactBox is visible by default.
    /// </summary>
    public bool IsVisible
    {
        get => field;
        set { if (field != value) { field = value; NotifyPropertyChanged(nameof(IsVisible)); } }
    } = true;
}
