// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.CSharpBridge;

namespace GameEventScript.Tests.Native.Api;

[TestClass]
public sealed class GameEventScriptExternalTypeImmutabilityTests
{
    [TestMethod]
    public void CatalogEnumerationAndLookupRemainConsistent()
    {
        var original = Definition();
        var catalog = new GesDeclaredExternalTypeCatalog([original]);

        CollectionMutationProbe.ReplaceFirstIfWritable(catalog.Types, new GesExternalTypeDefinition("Other", [], []));

        Assert.AreSame(original, catalog.Types.Single());
        Assert.AreSame(original, catalog.Resolve(":Sample"));
        Assert.IsNull(catalog.Resolve("Other"));
    }

    [TestMethod]
    public void FieldsRemainValidAfterCollectionMutationAttempts()
    {
        var definition = Definition();

        CollectionMutationProbe.ReplaceFirstIfWritable(definition.Fields, new GesExternalTypeFieldDefinition("changed", "Text"));

        Assert.AreEqual("value", definition.Fields.Single().Name);
        Assert.AreEqual("Number", definition.Fields[0].TypeName);
        Assert.AreEqual(definition.Fields[0].Name, definition.Constructors[0].Parameters[0].Name);
    }

    [TestMethod]
    public void ConstructorsRemainOwnedByTheirDeclaredType()
    {
        var definition = Definition();
        var original = definition.Constructors[0];

        CollectionMutationProbe.ReplaceFirstIfWritable(definition.Constructors, new GesExternalTypeConstructorDefinition("Other", []));

        Assert.AreSame(original, definition.Constructors.Single());
        Assert.AreEqual(definition.Name, definition.Constructors[0].TypeName);
    }

    [TestMethod]
    public void ConstructorParametersRemainConsistentWithTheirSignature()
    {
        var constructor = Definition().Constructors[0];
        var signature = constructor.SignatureId;

        CollectionMutationProbe.ReplaceFirstIfWritable(constructor.Parameters, new GesExternalTypeParameterDefinition("changed", "Text"));

        Assert.AreEqual("value", constructor.Parameters.Single().Name);
        Assert.AreEqual("Number", constructor.Parameters[0].TypeName);
        Assert.AreEqual(signature, GesExternalTypeConstructorReference.CreateSignatureId("Sample", constructor.Parameters.Select(parameter => parameter.Name).ToArray()));
    }

    [TestMethod]
    public void ConstructionCopiesCallerOwnedSequences()
    {
        var fields = new[] { new GesExternalTypeFieldDefinition("value", "Number") };
        var parameters = new[] { new GesExternalTypeParameterDefinition("value", "Number") };
        var constructors = new[] { new GesExternalTypeConstructorDefinition("Sample", parameters) };
        var definitions = new[] { new GesExternalTypeDefinition("Sample", fields, constructors) };
        var catalog = new GesDeclaredExternalTypeCatalog(definitions);

        fields[0] = new GesExternalTypeFieldDefinition("changed", "Text");
        parameters[0] = new GesExternalTypeParameterDefinition("changed", "Text");
        constructors[0] = new GesExternalTypeConstructorDefinition("Other", []);
        definitions[0] = new GesExternalTypeDefinition("Other", [], []);

        var retained = catalog.Types.Single();
        Assert.AreSame(retained, catalog.Resolve("Sample"));
        Assert.IsNull(catalog.Resolve("Other"));
        Assert.AreEqual("value", retained.Fields.Single().Name);
        Assert.AreEqual("Number", retained.Fields[0].TypeName);
        Assert.AreEqual("Sample", retained.Constructors.Single().TypeName);
        Assert.AreEqual("value", retained.Constructors[0].Parameters.Single().Name);
        Assert.AreEqual("Number", retained.Constructors[0].Parameters[0].TypeName);
    }

    [TestMethod]
    public void BridgeCatalogEnumerationAndConstructorLookupRemainConsistent()
    {
        var registry = GameEventScriptCSharpExternalTypes.CreateRegistry(typeof(SampleValue));
        var original = registry.Types.Single();

        CollectionMutationProbe.ReplaceFirstIfWritable(registry.Types, new GesExternalTypeDefinition("Other", [], []));

        Assert.AreSame(original, registry.Types.Single());
        Assert.AreSame(original, registry.Resolve("Sample"));
        Assert.IsNull(registry.Resolve("Other"));
        var constructor = registry.Resolve(new GesExternalTypeConstructorReference("Sample", ["value"]));
        Assert.IsNotNull(constructor);
        Assert.AreEqual(original.Constructors[0].SignatureId, constructor.Definition.SignatureId);
    }

    private static GesExternalTypeDefinition Definition()
    {
        var field = new GesExternalTypeFieldDefinition("value", "Number");
        var constructor = new GesExternalTypeConstructorDefinition("Sample", [new GesExternalTypeParameterDefinition("value", "Number")]);
        return new GesExternalTypeDefinition("Sample", [field], [constructor]);
    }

    [GesType("Sample")]
    private sealed class SampleValue
    {
        [GesConstruct]
        public SampleValue([GesParam("value", "Number")] long value) => Value = value;

        [GesField("value", "Number")]
        public long Value { get; }
    }
}
