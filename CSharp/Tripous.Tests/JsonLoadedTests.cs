/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests JSON loaded notifications and descriptor reference updates.
/// </summary>
public class JsonLoadedTests
{
    // ● public types
    /// <summary>
    /// An object that records its loaded notifications.
    /// </summary>
    public class Sample : IJsonLoadable
    {
        /// <summary>
        /// Gets or sets the sample name.
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// Gets the number of loaded notifications.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public int Notifications { get; private set; }
        /// <summary>
        /// Records a loaded notification.
        /// </summary>
        public void JsonLoaded() => Notifications++;
    }
    /// <summary>
    /// An object containing another loadable object.
    /// </summary>
    public class Parent : IJsonLoadable
    {
        /// <summary>
        /// Gets or sets the nested object.
        /// </summary>
        public Sample Child { get; set; }
        /// <summary>
        /// Gets the number of loaded notifications.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public int Notifications { get; private set; }
        /// <summary>
        /// Records the root notification.
        /// </summary>
        public void JsonLoaded() => Notifications++;
    }
    /// <summary>
    /// A descriptor that records its state when references are updated.
    /// </summary>
    public class ReferenceDef : BaseDef
    {
        /// <summary>
        /// Gets the number of reference updates.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public int Updates { get; private set; }
        /// <summary>
        /// Gets whether the title key was unset during the last update.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool TitleKeyWasEmpty { get; private set; }
        /// <summary>
        /// Records the descriptor state after loading.
        /// </summary>
        public override void UpdateReferences()
        {
            Updates++;
            TitleKeyWasEmpty = IsTitleKeyEmpty;
        }
    }

    // ● public
    /// <summary>
    /// Both deserialize overloads notify the root once after values are assigned.
    /// </summary>
    [Fact]
    public void DeserializeNotifiesRootOnce()
    {
        Sample Generic = Json.Deserialize<Sample>("{\"Name\":\"First\"}");
        Sample Typed = (Sample)Json.Deserialize("{\"Name\":\"Second\"}", typeof(Sample));
        Assert.Equal("First", Generic.Name);
        Assert.Equal(1, Generic.Notifications);
        Assert.Equal("Second", Typed.Name);
        Assert.Equal(1, Typed.Notifications);
    }
    /// <summary>
    /// Populate notifies the existing object only after successful population.
    /// </summary>
    [Fact]
    public void PopulateNotifiesOnceAfterSuccess()
    {
        Sample Instance = new();
        Json.PopulateObject(Instance, "{\"Name\":\"New\"}");
        Assert.Equal("New", Instance.Name);
        Assert.Equal(1, Instance.Notifications);
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => Json.PopulateObject(Instance, "{"));
        Assert.Equal(1, Instance.Notifications);
    }
    /// <summary>
    /// Loading notifies only the root; nested objects are managed by their owner.
    /// </summary>
    [Fact]
    public void DeserializeDoesNotNotifyNestedObjectsAutomatically()
    {
        Parent Instance = Json.Deserialize<Parent>("{\"Child\":{\"Name\":\"Child\"}}");
        Assert.Equal(1, Instance.Notifications);
        Assert.Equal("Child", Instance.Child.Name);
        Assert.Equal(0, Instance.Child.Notifications);
    }
    /// <summary>
    /// Assign notifies after restoring the descriptor's unset title-key state.
    /// </summary>
    [Fact]
    public void AssignNotifiesAfterRestoringFallbackState()
    {
        ReferenceDef Source = new() { Name = "Customer" };
        ReferenceDef Target = new();
        Target.Assign(Source);
        Assert.Equal("Customer", Target.Name);
        Assert.True(Target.IsTitleKeyEmpty);
        Assert.True(Target.TitleKeyWasEmpty);
        Assert.Equal(1, Target.Updates);
    }
    /// <summary>
    /// A loaded descriptor list updates its items once.
    /// </summary>
    [Fact]
    public void DeserializeDescriptorListUpdatesReferences()
    {
        DefList<ReferenceDef> List = Json.Deserialize<DefList<ReferenceDef>>("[{\"Name\":\"First\"},{\"Name\":\"Second\"}]");
        Assert.Equal(2, List.Count);
        Assert.All(List, Item => Assert.Equal(1, Item.Updates));
    }
    /// <summary>
    /// File loading follows the same single-callback rule and ignores missing files.
    /// </summary>
    [Fact]
    public void FileLoadingNotifiesOnceWhenFileExists()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-json-loaded-{Guid.NewGuid():N}.json");
        try
        {
            Sample Target = new();
            Json.LoadFromFile(Target, FilePath);
            Assert.Equal(0, Target.Notifications);
            File.WriteAllText(FilePath, "{\"Name\":\"From file\"}");
            Json.LoadFromFile(Target, FilePath);
            Assert.Equal("From file", Target.Name);
            Assert.Equal(1, Target.Notifications);
            Sample Created = (Sample)Json.LoadFromFile(typeof(Sample), FilePath);
            Assert.Equal(1, Created.Notifications);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
