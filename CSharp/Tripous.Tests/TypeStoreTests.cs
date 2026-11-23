/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests registration, lookup and creation of application types.
/// </summary>
public class TypeStoreTests
{
    // ● public types
    /// <summary>
    /// A base class for testing assignable types.
    /// </summary>
    public class ExampleBase { }
    /// <summary>
    /// A derived class used to test instance creation.
    /// </summary>
    public class ExampleDerived : ExampleBase { }
    /// <summary>
    /// A marked type used for assembly discovery.
    /// </summary>
    [TypeStore]
    public class Discoverable { }
    /// <summary>
    /// Inherits the discovery attribute from its base class.
    /// </summary>
    public class InheritedDiscoverable : Discoverable { }
    /// <summary>
    /// An unmarked type used to test lookup in loaded assemblies.
    /// </summary>
    public class NotDiscoverable { }
    /// <summary>
    /// Contains a type whose simple name is intentionally ambiguous.
    /// </summary>
    public class FirstContainer
    {
        /// <summary>
        /// A type with a duplicate simple name.
        /// </summary>
        public class SharedDiscoveryName { }
        /// <summary>
        /// A type used to test lookup constrained by a base class.
        /// </summary>
        public class TypedChoice : ExampleBase { }
    }
    /// <summary>
    /// Contains another type with the same simple name.
    /// </summary>
    public class SecondContainer
    {
        /// <summary>
        /// A type with a duplicate simple name.
        /// </summary>
        public class SharedDiscoveryName { }
        /// <summary>
        /// A different type with the same simple name.
        /// </summary>
        public class TypedChoice { }
    }
    /// <summary>
    /// An assembly that reports a type-loading failure.
    /// </summary>
    public class BrokenAssembly : System.Reflection.Assembly
    {
        /// <summary>
        /// Gets the assembly name used in the error message.
        /// </summary>
        public override string FullName => "BrokenExample";
        /// <summary>
        /// Simulates a failure while reading assembly types.
        /// </summary>
        public override Type[] GetTypes() => throw new System.Reflection.ReflectionTypeLoadException(Array.Empty<Type>(), Array.Empty<Exception>());
    }

    // ● public
    /// <summary>
    /// Assembly registration discovers marked and inherited marked types only.
    /// </summary>
    [Fact]
    public void RegistersMarkedAssemblyTypes()
    {
        TypeStore.Register(typeof(TypeStoreTests).Assembly);
        Assert.Same(typeof(Discoverable), TypeStore.Get(typeof(Discoverable).FullName));
        Assert.Same(typeof(InheritedDiscoverable), TypeStore.Get(typeof(InheritedDiscoverable).FullName));
        Assert.Same(typeof(NotDiscoverable), TypeStore.Find(typeof(NotDiscoverable).FullName));
    }
    /// <summary>
    /// A type-loading failure identifies the assembly and preserves its cause.
    /// </summary>
    [Fact]
    public void AssemblyTypeLoadingFailureIsReported()
    {
        InvalidOperationException Error = Assert.Throws<InvalidOperationException>(() => TypeStore.Register(new BrokenAssembly()));
        Assert.Contains("BrokenExample", Error.Message);
        Assert.IsType<System.Reflection.ReflectionTypeLoadException>(Error.InnerException);
    }
    /// <summary>
    /// Registered types are found by full name without case distinction.
    /// </summary>
    [Fact]
    public void RegistersAndFindsTypes()
    {
        TypeStore.Register(typeof(ExampleDerived));
        TypeStore.Register(typeof(ExampleDerived));
        Assert.Same(typeof(ExampleDerived), TypeStore.Find(typeof(ExampleDerived).FullName.ToUpperInvariant()));
        Assert.Same(typeof(ExampleDerived), TypeStore.Get(typeof(ExampleDerived).FullName));
        Assert.Null(TypeStore.Find("Tripous.Tests.MissingType"));
        Assert.Throws<KeyNotFoundException>(() => TypeStore.Get("Tripous.Tests.MissingType"));
    }
    /// <summary>
    /// Loaded types can be found by simple and assembly-qualified names.
    /// </summary>
    [Fact]
    public void FindsLoadedTypesByName()
    {
        Assert.Same(typeof(NotDiscoverable), TypeStore.Find(nameof(NotDiscoverable)));
        Assert.Same(typeof(NotDiscoverable), TypeStore.Find(typeof(NotDiscoverable).AssemblyQualifiedName));
    }
    /// <summary>
    /// Ambiguous simple names report all matching type names.
    /// </summary>
    [Fact]
    public void AmbiguousSimpleNameThrows()
    {
        InvalidOperationException Error = Assert.Throws<InvalidOperationException>(() => TypeStore.Find("SharedDiscoveryName"));
        Assert.Contains(typeof(FirstContainer.SharedDiscoveryName).FullName, Error.Message);
        Assert.Contains(typeof(SecondContainer.SharedDiscoveryName).FullName, Error.Message);
    }
    /// <summary>
    /// A base type narrows simple-name matches before ambiguity is checked.
    /// </summary>
    [Fact]
    public void CreationFiltersByBaseTypeBeforeCheckingAmbiguity()
    {
        TypeStore.Register(typeof(FirstContainer.TypedChoice));
        TypeStore.Register(typeof(SecondContainer.TypedChoice));
        Assert.Throws<InvalidOperationException>(() => TypeStore.Find("TypedChoice"));
        Assert.IsType<FirstContainer.TypedChoice>(TypeStore.CreateInstance<ExampleBase>("TypedChoice"));
    }
    /// <summary>
    /// Additional application assembly exclusions apply only to the current call.
    /// </summary>
    [Fact]
    public void ApplicationAssembliesRespectExclusions()
    {
        System.Reflection.Assembly TestAssembly = typeof(TypeStoreTests).Assembly;
        string Name = TestAssembly.GetName().Name;
        Assert.Contains(TestAssembly, AppAssemblies.GetApplicationAssemblies());
        Assert.DoesNotContain(TestAssembly, AppAssemblies.GetApplicationAssemblies(new[] { Name.ToUpperInvariant() }));
        Assert.DoesNotContain(AppAssemblies.GetApplicationAssemblies(), Assembly => Assembly.GetName().Name.StartsWith("System", StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>
    /// Instance creation enforces the requested base type.
    /// </summary>
    [Fact]
    public void CreatesAssignableTypeAndRejectsOtherTypes()
    {
        TypeStore.Register(typeof(ExampleDerived));
        Assert.IsType<ExampleDerived>(TypeStore.CreateInstance<ExampleBase>(typeof(ExampleDerived).FullName));
        Assert.Throws<InvalidOperationException>(() => TypeStore.CreateInstance<string>(typeof(ExampleDerived).FullName));
    }
}
