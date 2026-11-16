// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>
/// Resolves the host's external type constructors for the constructor imports of a Program while
/// <c>Host.Load</c> links it; it is not consulted per call.
/// </summary>
public interface IGesExternalTypeRegistry
{
    /// <summary>
    /// Returns the constructor bound to a normalized type name and argument signature. Equal references must
    /// resolve consistently during one load.
    /// </summary>
    /// <param name="reference">The normalized type name and argument labels of the imported constructor.</param>
    /// <returns>
    /// The constructor, or <see langword="null"/> when none is registered. Absence, or a constructor whose
    /// <see cref="IGesExternalTypeConstructor.Definition"/> does not match the import exactly, fails the link.
    /// </returns>
    IGesExternalTypeConstructor? Resolve(GesExternalTypeConstructorReference reference);
}

/// <summary>
/// Declarative compile-time description of the external types a host offers: names, fields and constructor
/// signatures, without constructors, field accessors or host instances.
/// </summary>
public interface IGesExternalTypeCatalog
{
    /// <summary>
    /// Gets immutable type definitions in declaration order without exposing writable collection storage.
    /// </summary>
    IReadOnlyList<GesExternalTypeDefinition> Types { get; }

    /// <summary>
    /// Looks up the definition of one external type.
    /// </summary>
    /// <param name="typeName">The type name, with or without a leading <c>:</c>.</param>
    /// <returns>The definition, or <see langword="null"/> when the catalog does not declare the type.</returns>
    GesExternalTypeDefinition? Resolve(string typeName);
}

/// <summary>
/// A host object exposed to scripts as a value of a declared external type. The runtime keeps no field values:
/// every script field read calls <see cref="GetField(string)"/> again, and every conversion to Map, JSON or schema
/// encoding reads all declared fields in declaration order.
/// </summary>
public interface IGesExternalValue
{
    /// <summary>
    /// Gets the definition. All values of a type should share one, because creating one validates and sorts its fields.
    /// </summary>
    GesExternalTypeDefinition Definition { get; }

    /// <summary>
    /// Reads the current value of one declared field. The read must be synchronous and deterministic for an unchanged
    /// object during one handler; between handlers the value may change. The runtime converts the result to the
    /// field's declared type.
    /// </summary>
    /// <param name="fieldName">The declared field name.</param>
    /// <returns>The field value, or <see langword="null"/> when the field has no value; such a field is left out of a materialized Map.</returns>
    GesValue? GetField(string fieldName);
}

/// <summary>
/// Immutable external type catalog built from a fixed list of definitions.
/// </summary>
public sealed class GesDeclaredExternalTypeCatalog : IGesExternalTypeCatalog
{
    private readonly Dictionary<string, GesExternalTypeDefinition> _typesByName;

    /// <summary>
    /// Creates a catalog from definitions, copying them in their given order.
    /// </summary>
    /// <param name="types">The type definitions in declaration order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="types"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The sequence contains <see langword="null"/> or declares a type name more than once.</exception>
    public GesDeclaredExternalTypeCatalog(IEnumerable<GesExternalTypeDefinition> types)
    {
        _ = types ?? throw new ArgumentNullException(nameof(types));
        var definitions = new List<GesExternalTypeDefinition>();
        _typesByName = new Dictionary<string, GesExternalTypeDefinition>(StringComparer.Ordinal);
        foreach (var definition in types)
        {
            _ = definition ?? throw new ArgumentException("External type catalog contains null.", nameof(types));
            if (!_typesByName.TryAdd(definition.Name, definition))
                throw new ArgumentException($"External GameEventScript type ':{definition.Name}' is declared more than once.", nameof(types));
            definitions.Add(definition);
        }

        Types = GesReadOnlyArray<GesExternalTypeDefinition>.FromOwnedArray(definitions.ToArray());
    }

    /// <summary>
    /// Gets immutable type definitions in declaration order without exposing writable collection storage.
    /// </summary>
    public IReadOnlyList<GesExternalTypeDefinition> Types { get; }

    /// <summary>
    /// Looks up the definition of one external type by its normalized name, compared ordinally.
    /// </summary>
    /// <param name="typeName">The type name, with or without a leading <c>:</c>.</param>
    /// <returns>The definition, or <see langword="null"/> when the catalog does not declare the type.</returns>
    /// <exception cref="ArgumentException"><paramref name="typeName"/> is not a valid type name.</exception>
    public GesExternalTypeDefinition? Resolve(string typeName)
    {
        var normalized = GesExternalTypeNames.NormalizeTypeName(typeName);
        return _typesByName.TryGetValue(normalized, out var definition) ? definition : null;
    }
}

/// <summary>
/// Host implementation of one declared external type constructor.
/// </summary>
public interface IGesExternalTypeConstructor
{
    /// <summary>
    /// Gets the declarative constructor signature this callback implements; linking requires it to match the
    /// Program's import exactly.
    /// </summary>
    GesExternalTypeConstructorDefinition Definition { get; }

    /// <summary>
    /// Constructs a value synchronously: reads the borrowed arguments and sets an external value or Nothing on the
    /// call. Setting no result yields Nothing. The call object and its arguments must not be retained.
    /// </summary>
    /// <param name="call">The reusable call holding the arguments and receiving the result.</param>
    void Invoke(GesExternalTypeConstructorCall call);
}

/// <summary>
/// Reusable call-scoped adapter passed to <see cref="IGesExternalTypeConstructor.Invoke"/>: it lends the arguments
/// and receives the constructed value. It must not be retained beyond the callback.
/// </summary>
public sealed class GesExternalTypeConstructorCall
{
    private GesValueArguments _arguments = GesValueArguments.Empty;
    private Runtime.Vm.GesVirtualMachine? _vm;
    private ushort _destinationRegister;
    private string? _expectedTypeName;
    private GesValue _result;
    private bool _hasResult;

    /// <summary>
    /// Creates a call without arguments whose result is Nothing.
    /// </summary>
    public GesExternalTypeConstructorCall()
    {
    }

    /// <summary>
    /// Creates a call over the given arguments whose result is Nothing, for invoking a constructor outside a host.
    /// </summary>
    /// <param name="arguments">The constructor arguments in declared parameter order.</param>
    public GesExternalTypeConstructorCall(GesValueArguments arguments)
    {
        _arguments = arguments;
    }

    /// <summary>
    /// Gets the borrowed constructor arguments in declared parameter order.
    /// </summary>
    public GesValueArguments Arguments => _arguments;

    /// <summary>
    /// Gets the constructed value, or Nothing when no result was set.
    /// </summary>
    public GesValue Result => _hasResult ? _result : GesValue.GesNothing();

    internal bool HasResult => _hasResult;

    internal void BeginCall(Runtime.Vm.GesVirtualMachine vm, ushort destinationRegister, GesValueArguments arguments, string expectedTypeName)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _destinationRegister = destinationRegister;
        _arguments = arguments;
        _expectedTypeName = expectedTypeName;
        _result = default;
        _hasResult = false;
    }

    internal void EndCall()
    {
        _vm = null;
        _destinationRegister = 0;
        _arguments = GesValueArguments.Empty;
        _expectedTypeName = null;
        _result = default;
        _hasResult = false;
    }

    /// <summary>
    /// Sets the result to Nothing, which rejects the construction without raising a script error.
    /// </summary>
    public void SetNothing()
    {
        var value = GesValue.GesNothing();
        SetValue(in value);
    }

    private void SetValue(in GesValue value)
    {
        _hasResult = true;
        _result = value;
        _vm?.SetValue(_destinationRegister, in value);
    }

    /// <summary>
    /// Sets the constructed external value as the result. When the value's declared type differs from the type
    /// the constructor builds, the result becomes Nothing and the runtime raises
    /// <c>runtime.invalidExternalTypeBinding</c>.
    /// </summary>
    /// <param name="value">The constructed value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public void SetExternalValue(IGesExternalValue value)
    {
        _ = value ?? throw new ArgumentNullException(nameof(value));
        if (_expectedTypeName is not null && !string.Equals(value.Definition.Name, _expectedTypeName, StringComparison.Ordinal))
        {
            SetNothing();
            _vm?.RaiseError(GesDiagnosticCodes.RuntimeInvalidExternalTypeBinding,
                $"External constructor for ':{_expectedTypeName}' returned value of type ':{value.Definition.Name}'.");
            return;
        }
        _hasResult = true;
        _result = default;
        _result.SetExternal(value);
        _vm?.SetExternal(_destinationRegister, value);
    }
}

/// <summary>
/// Immutable declarative description of one external type: its name, ordered unique fields and ordered unique
/// constructor signatures.
/// </summary>
public sealed class GesExternalTypeDefinition
{
    /// <summary>
    /// Creates a definition, copying the field and constructor sequences. Creation validates and sorts the fields,
    /// so all values of a type should share one definition.
    /// </summary>
    /// <param name="name">The type name, with or without a leading <c>:</c>.</param>
    /// <param name="fields">The fields in declaration order.</param>
    /// <param name="constructors">The constructor signatures in declaration order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fields"/> or <paramref name="constructors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The name is invalid, or a sequence contains <see langword="null"/>, a duplicate field name or a duplicate
    /// constructor signature, or a constructor builds another type or has a parameter that is not a declared field.
    /// </exception>
    public GesExternalTypeDefinition(string name, IEnumerable<GesExternalTypeFieldDefinition> fields, IEnumerable<GesExternalTypeConstructorDefinition> constructors)
    {
        Name = GesExternalTypeNames.NormalizeTypeName(name);
        var copiedFields = CopyFields(fields ?? throw new ArgumentNullException(nameof(fields)));
        var copiedConstructors = CopyConstructors(constructors ?? throw new ArgumentNullException(nameof(constructors)));
        ValidateFields(copiedFields);
        Fields = GesReadOnlyArray<GesExternalTypeFieldDefinition>.FromOwnedArray(copiedFields);
        CanonicalKeys = new string[copiedFields.Length];
        var order = new int[copiedFields.Length];
        for (var index = 0; index < order.Length; index++)
        {
            CanonicalKeys[index] = copiedFields[index].Name;
            order[index] = index;
        }

        Array.Sort(CanonicalKeys, order, GesText.ScalarComparer);
        CanonicalPositions = new int[order.Length];
        for (var position = 0; position < order.Length; position++) CanonicalPositions[order[position]] = position;
        ValidateConstructors(copiedConstructors);
        Constructors = GesReadOnlyArray<GesExternalTypeConstructorDefinition>.FromOwnedArray(copiedConstructors);
    }

    /// <summary>
    /// Gets the normalized type name without the leading <c>:</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the immutable ordered fields without exposing writable collection storage.
    /// </summary>
    public IReadOnlyList<GesExternalTypeFieldDefinition> Fields { get; }

    /// <summary>
    /// Gets the immutable ordered constructors without exposing writable collection storage.
    /// </summary>
    public IReadOnlyList<GesExternalTypeConstructorDefinition> Constructors { get; }

    // The field names in scalar order, which every materialized map shares, and the place of each declared field in it.
    internal string[] CanonicalKeys { get; }

    internal int[] CanonicalPositions { get; }

    internal bool HasConstructor(IReadOnlyCollection<string> argumentLabels)
    {
        var signatureId = GesExternalTypeConstructorReference.CreateSignatureId(Name, argumentLabels);
        for (var index = 0; index < Constructors.Count; index++)
        {
            if (string.Equals(Constructors[index].SignatureId, signatureId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ValidateFields(IReadOnlyList<GesExternalTypeFieldDefinition> fields)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < fields.Count; index++)
        {
            if (fields[index] is null)
                throw new ArgumentException("External type field list contains null.", nameof(fields));
            if (!names.Add(fields[index].Name))
                throw new ArgumentException($"External GameEventScript type ':{Name}' declares field '{fields[index].Name}' more than once.", nameof(fields));
        }
    }

    private void ValidateConstructors(IReadOnlyList<GesExternalTypeConstructorDefinition> constructors)
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        for (var fieldIndex = 0; fieldIndex < Fields.Count; fieldIndex++) fieldNames.Add(Fields[fieldIndex].Name);
        for (var index = 0; index < constructors.Count; index++)
        {
            var constructor = constructors[index] ?? throw new ArgumentException("External type constructor list contains null.", nameof(constructors));
            if (!string.Equals(constructor.TypeName, Name, StringComparison.Ordinal))
                throw new ArgumentException($"External constructor '{constructor.SignatureId}' does not construct type ':{Name}'.", nameof(constructors));
            if (!signatures.Add(constructor.SignatureId))
                throw new ArgumentException($"External GameEventScript type ':{Name}' declares constructor '{constructor.SignatureId}' more than once.", nameof(constructors));
            for (var parameterIndex = 0; parameterIndex < constructor.Parameters.Count; parameterIndex++)
            {
                if (!fieldNames.Contains(constructor.Parameters[parameterIndex].Name))
                    throw new ArgumentException($"External constructor '{constructor.SignatureId}' parameter '{constructor.Parameters[parameterIndex].Name}' is not a declared field.", nameof(constructors));
            }
        }
    }

    private static GesExternalTypeFieldDefinition[] CopyFields(IEnumerable<GesExternalTypeFieldDefinition> fields)
    {
        if (fields is IReadOnlyCollection<GesExternalTypeFieldDefinition> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var result = new GesExternalTypeFieldDefinition[collection.Count];
            var index = 0;
            foreach (var field in collection)
            {
                result[index++] = field;
            }

            return result;
        }

        var list = new List<GesExternalTypeFieldDefinition>();
        foreach (var field in fields)
        {
            list.Add(field);
        }

        var values = new GesExternalTypeFieldDefinition[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            values[index] = list[index];
        }

        return values;
    }

    private static GesExternalTypeConstructorDefinition[] CopyConstructors(IEnumerable<GesExternalTypeConstructorDefinition> constructors)
    {
        if (constructors is IReadOnlyCollection<GesExternalTypeConstructorDefinition> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var result = new GesExternalTypeConstructorDefinition[collection.Count];
            var index = 0;
            foreach (var constructor in collection)
            {
                result[index++] = constructor;
            }

            return result;
        }

        var list = new List<GesExternalTypeConstructorDefinition>();
        foreach (var constructor in constructors)
        {
            list.Add(constructor);
        }

        var values = new GesExternalTypeConstructorDefinition[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            values[index] = list[index];
        }

        return values;
    }
}

/// <summary>
/// Immutable description of one external type field: its name and declared type. Field values are converted to
/// the declared builtin type before a script sees them; Nothing stays Nothing.
/// </summary>
public sealed class GesExternalTypeFieldDefinition
{
    /// <summary>
    /// Creates a field from a type name, which may name a builtin type such as <c>:Quantity(W)</c> or a custom type.
    /// </summary>
    /// <param name="name">The field name, a lower-case-initial ASCII identifier.</param>
    /// <param name="typeName">The declared type name, with or without a leading <c>:</c>.</param>
    /// <exception cref="ArgumentException">The name or type name is invalid.</exception>
    public GesExternalTypeFieldDefinition(string name, string typeName)
    {
        Name = GesExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = GesExternalTypeNames.NormalizeTypeName(typeName);
        var (kind, unit) = GesExternalTypeNames.GetKindAndUnit(TypeName);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    /// <summary>
    /// Creates a field of a builtin kind without a unit constraint.
    /// </summary>
    /// <param name="name">The field name, a lower-case-initial ASCII identifier.</param>
    /// <param name="kind">The builtin kind of the field.</param>
    /// <exception cref="ArgumentException">The name is invalid.</exception>
    public GesExternalTypeFieldDefinition(string name, GesBytecodeTypeKind kind)
        : this(name, kind, unit: null)
    {
    }

    /// <summary>
    /// Creates a field of a builtin kind with a unit. A non-none unit on a Vector or Point overrides the supplied
    /// value's unit without rescaling its components.
    /// </summary>
    /// <param name="name">The field name, a lower-case-initial ASCII identifier.</param>
    /// <param name="kind">The builtin kind of the field.</param>
    /// <param name="unit">The declared unit.</param>
    /// <exception cref="ArgumentException">The name is invalid, or the unit is a numeric unit and the kind is not <see cref="GesBytecodeTypeKind.Float"/>, <see cref="GesBytecodeTypeKind.Vector"/> or <see cref="GesBytecodeTypeKind.Point"/>.</exception>
    public GesExternalTypeFieldDefinition(string name, GesBytecodeTypeKind kind, GesQuantityUnit unit)
        : this(name, kind, (GesQuantityUnit?)unit)
    {
    }

    internal GesExternalTypeFieldDefinition(string name, GesBytecodeTypeKind? kind, GesQuantityUnit? unit)
    {
        Name = GesExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = kind is { } resolvedKind
            ? GesExternalTypeNames.ToTypeName(resolvedKind, unit)
            : throw new ArgumentException("External GameEventScript field kind must be specified.", nameof(kind));
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    /// <summary>
    /// Gets the normalized field name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the normalized declared type name without the leading <c>:</c>, including a unit where one is declared.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the builtin kind, or <see langword="null"/> when the field is declared with a custom type name.
    /// </summary>
    public GesBytecodeTypeKind? Kind { get; }

    /// <summary>
    /// Gets the declared unit. UnitNone leaves Vector and Point units unconstrained.
    /// </summary>
    public GesQuantityUnit Unit { get; }
}

/// <summary>
/// Immutable description of one external constructor parameter: its argument label, which must name a declared
/// field, and its declared type. Arguments are converted to the declared builtin type before the constructor
/// sees them; Nothing stays Nothing.
/// </summary>
public sealed class GesExternalTypeParameterDefinition
{
    /// <summary>
    /// Creates a parameter from a type name, which may name a builtin type such as <c>:Quantity(W)</c> or a custom type.
    /// </summary>
    /// <param name="name">The argument label, a lower-case-initial ASCII identifier.</param>
    /// <param name="typeName">The declared type name, with or without a leading <c>:</c>.</param>
    /// <exception cref="ArgumentException">The name or type name is invalid.</exception>
    public GesExternalTypeParameterDefinition(string name, string typeName)
    {
        Name = GesExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = GesExternalTypeNames.NormalizeTypeName(typeName);
        var (kind, unit) = GesExternalTypeNames.GetKindAndUnit(TypeName);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    /// <summary>
    /// Creates a parameter of a builtin kind without a unit constraint.
    /// </summary>
    /// <param name="name">The argument label, a lower-case-initial ASCII identifier.</param>
    /// <param name="kind">The builtin kind of the parameter.</param>
    /// <exception cref="ArgumentException">The name is invalid.</exception>
    public GesExternalTypeParameterDefinition(string name, GesBytecodeTypeKind kind)
        : this(name, kind, unit: null)
    {
    }

    /// <summary>
    /// Creates a parameter of a builtin kind with a unit. A non-none unit on a Vector or Point overrides the supplied
    /// argument's unit without rescaling its components.
    /// </summary>
    /// <param name="name">The argument label, a lower-case-initial ASCII identifier.</param>
    /// <param name="kind">The builtin kind of the parameter.</param>
    /// <param name="unit">The declared unit.</param>
    /// <exception cref="ArgumentException">The name is invalid, or the unit is a numeric unit and the kind is not <see cref="GesBytecodeTypeKind.Float"/>, <see cref="GesBytecodeTypeKind.Vector"/> or <see cref="GesBytecodeTypeKind.Point"/>.</exception>
    public GesExternalTypeParameterDefinition(string name, GesBytecodeTypeKind kind, GesQuantityUnit unit)
        : this(name, kind, (GesQuantityUnit?)unit)
    {
    }

    internal GesExternalTypeParameterDefinition(string name, GesBytecodeTypeKind? kind, GesQuantityUnit? unit)
    {
        Name = GesExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = kind is { } resolvedKind
            ? GesExternalTypeNames.ToTypeName(resolvedKind, unit)
            : throw new ArgumentException("External GameEventScript parameter kind must be specified.", nameof(kind));
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    /// <summary>
    /// Gets the normalized argument label.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the normalized declared type name without the leading <c>:</c>, including a unit where one is declared.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the builtin kind, or <see langword="null"/> when the parameter is declared with a custom type name.
    /// </summary>
    public GesBytecodeTypeKind? Kind { get; }

    /// <summary>
    /// Gets the declared unit. UnitNone leaves Vector and Point units unconstrained.
    /// </summary>
    public GesQuantityUnit Unit { get; }
}

/// <summary>
/// Immutable declarative signature of one external type constructor: the type it builds and its parameters.
/// </summary>
public sealed class GesExternalTypeConstructorDefinition
{
    /// <summary>
    /// Creates a constructor signature, copying the parameter sequence and deriving <see cref="SignatureId"/>.
    /// </summary>
    /// <param name="typeName">The type the constructor builds, with or without a leading <c>:</c>.</param>
    /// <param name="parameters">The parameters in declaration order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The type name is invalid, or the sequence contains <see langword="null"/> or a repeated parameter name.</exception>
    public GesExternalTypeConstructorDefinition(string typeName, IEnumerable<GesExternalTypeParameterDefinition> parameters)
    {
        TypeName = GesExternalTypeNames.NormalizeTypeName(typeName);
        var copiedParameters = CopyParameters(parameters ?? throw new ArgumentNullException(nameof(parameters)));
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < copiedParameters.Length; index++)
        {
            var parameter = copiedParameters[index] ?? throw new ArgumentException("External constructor parameter list contains null.", nameof(parameters));
            if (!names.Add(parameter.Name))
                throw new ArgumentException($"External constructor for ':{TypeName}' declares parameter '{parameter.Name}' more than once.", nameof(parameters));
        }
        Parameters = GesReadOnlyArray<GesExternalTypeParameterDefinition>.FromOwnedArray(copiedParameters);
        SignatureId = GesExternalTypeConstructorReference.CreateSignatureId(TypeName, CreateParameterNameArray(Parameters));
    }

    /// <summary>
    /// Gets the normalized name of the type the constructor builds, without the leading <c>:</c>.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the immutable ordered parameters used to derive the constructor signature.
    /// </summary>
    public IReadOnlyList<GesExternalTypeParameterDefinition> Parameters { get; }

    /// <summary>
    /// Gets the canonical signature <c>TypeName(label,...)</c> formed by
    /// <see cref="GesExternalTypeConstructorReference.CreateSignatureId"/> from the parameter names.
    /// </summary>
    public string SignatureId { get; }

    private static GesExternalTypeParameterDefinition[] CopyParameters(IEnumerable<GesExternalTypeParameterDefinition> parameters)
    {
        if (parameters is IReadOnlyCollection<GesExternalTypeParameterDefinition> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var result = new GesExternalTypeParameterDefinition[collection.Count];
            var index = 0;
            foreach (var parameter in collection)
            {
                result[index++] = parameter;
            }

            return result;
        }

        var list = new List<GesExternalTypeParameterDefinition>();
        foreach (var parameter in parameters)
        {
            list.Add(parameter);
        }

        var values = new GesExternalTypeParameterDefinition[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            values[index] = list[index];
        }

        return values;
    }

    private static string[] CreateParameterNameArray(IReadOnlyList<GesExternalTypeParameterDefinition> parameters)
    {
        if (parameters.Count == 0)
        {
            return [];
        }

        var values = new string[parameters.Count];
        for (var index = 0; index < parameters.Count; index++)
        {
            values[index] = parameters[index].Name;
        }

        return values;
    }
}

/// <summary>
/// Represents an immutable normalized external type name and constructor argument signature.
/// </summary>
public sealed class GesExternalTypeConstructorReference
{
    /// <summary>
    /// Creates a reference, normalizing the type name and copying the argument labels in ordinal order.
    /// </summary>
    /// <param name="typeName">The type name, with or without a leading <c>:</c>.</param>
    /// <param name="argumentLabels">The argument labels; <see langword="null"/> means none.</param>
    /// <exception cref="ArgumentException">The type name or a label is invalid.</exception>
    public GesExternalTypeConstructorReference(string typeName, IEnumerable<string?>? argumentLabels)
    {
        TypeName = GesExternalTypeNames.NormalizeTypeName(typeName);
        ArgumentLabels = GesReadOnlyArray<string>.FromOwnedArray(NormalizeAndSortLabels(argumentLabels));
        SignatureId = CreateSignatureId(TypeName, ArgumentLabels);
    }

    /// <summary>
    /// Gets the normalized type name without the leading <c>:</c>.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the immutable normalized argument labels copied during construction.
    /// </summary>
    public IReadOnlyList<string> ArgumentLabels { get; }

    /// <summary>
    /// Gets the canonical signature formed by <see cref="CreateSignatureId"/>.
    /// </summary>
    public string SignatureId { get; }

    /// <summary>
    /// Formats the canonical constructor signature <c>TypeName(label,...)</c>: the normalized type name followed by
    /// the normalized labels in ordinal order, separated by commas. The function is pure.
    /// </summary>
    /// <param name="typeName">The type name, with or without a leading <c>:</c>.</param>
    /// <param name="argumentLabels">The argument labels in any order; <see langword="null"/> means none.</param>
    /// <returns>The canonical signature, for example <c>Turret(power,range)</c>.</returns>
    /// <exception cref="ArgumentException">The type name or a label is invalid.</exception>
    public static string CreateSignatureId(string typeName, IEnumerable<string?>? argumentLabels)
    {
        var normalizedTypeName = GesExternalTypeNames.NormalizeTypeName(typeName);
        var labels = NormalizeAndSortLabels(argumentLabels);
        var builder = new StringBuilder();
        builder.Append(normalizedTypeName).Append('(');
        for (var index = 0; index < labels.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(labels[index]);
        }

        return builder.Append(')').ToString();
    }

    private static string[] NormalizeAndSortLabels(IEnumerable<string?>? argumentLabels)
    {
        if (argumentLabels is null)
        {
            return [];
        }

        string[] values;
        if (argumentLabels is IReadOnlyCollection<string?> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            values = new string[collection.Count];
            var index = 0;
            foreach (var label in collection)
            {
                values[index++] = GesExternalTypeNames.NormalizeIdentifier(label, nameof(argumentLabels));
            }
        }
        else
        {
            var list = new List<string>();
            foreach (var label in argumentLabels)
            {
                list.Add(GesExternalTypeNames.NormalizeIdentifier(label, nameof(argumentLabels)));
            }

            if (list.Count == 0)
            {
                return [];
            }

            values = new string[list.Count];
            for (var index = 0; index < list.Count; index++)
            {
                values[index] = list[index];
            }
        }

        Array.Sort(values, StringComparer.Ordinal);
        return values;
    }
}

internal sealed class GesEmptyExternalTypeCatalog : IGesExternalTypeCatalog
{
    public static readonly GesEmptyExternalTypeCatalog Instance = new();

    private GesEmptyExternalTypeCatalog()
    {
    }

    public IReadOnlyList<GesExternalTypeDefinition> Types { get; } = Array.Empty<GesExternalTypeDefinition>();

    public GesExternalTypeDefinition? Resolve(string typeName) => null;
}

internal sealed class GesEmptyExternalTypeRegistry : IGesExternalTypeRegistry
{
    public static readonly GesEmptyExternalTypeRegistry Instance = new();

    private GesEmptyExternalTypeRegistry()
    {
    }

    public IGesExternalTypeConstructor? Resolve(GesExternalTypeConstructorReference reference) => null;
}
