using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents a message used within the Game Event Script system.
/// This message serves as a fundamental unit for communication, allowing for
/// event-driven execution between components, scripts, and handlers.
/// </summary>
/// <remarks>
/// A GameEventScriptMessage encapsulates a message name and an optional set
/// of named arguments. Additionally, it generates a unique signature ID for
/// efficient message identification and dispatch during runtime.
/// </remarks>
public sealed class GameEventScriptMessage
{
    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptMessage"/> using the specified name and arguments.
    /// This method facilitates the construction of a message containing the provided name and its associated
    /// arguments, allowing events to be parameterized and passed within the Game Event Script system.
    /// </summary>
    /// <param name="name">The name of the message. This is used to identify the message within the system.</param>
    /// <param name="arguments">A dictionary containing the named arguments for the message, where keys
    /// represent argument names, and values are the corresponding <see cref="GameEventScriptValue"/> instances.
    /// This parameter can be null if no arguments are provided.</param>
    /// <returns>Returns a <see cref="GameEventScriptMessage"/> object encapsulating the specified name and arguments.</returns>
    public static GameEventScriptMessage Create(string name, IReadOnlyDictionary<string, GameEventScriptValue>? arguments) => new(name, GameEventScriptNamedArguments.Create(arguments));

    /// <summary>
    /// Constructs a <see cref="GameEventScriptMessage"/> instance with a specified name and associated arguments.
    /// This method provides a convenient way to create a message by defining its name
    /// and passing a collection of key-value argument pairs.
    /// </summary>
    /// <param name="name">The name of the message, used to identify its purpose or type.</param>
    /// <param name="arguments">
    /// A collection of key-value pairs where each key represents the argument's name and
    /// the value is a <see cref="GameEventScriptValue"/> associated with that argument.
    /// </param>
    /// <returns>A new instance of the <see cref="GameEventScriptMessage"/> class constructed with the specified name and arguments.</returns>
    public static GameEventScriptMessage Create(string name, params (string name, GameEventScriptValue value)[] arguments)
        => new(name, GameEventScriptNamedArguments.CreateOrdered(arguments
            .Select(pair => new KeyValuePair<string, GameEventScriptValue>(pair.name, pair.value))
            .ToArray()));

    /// <summary>
    /// Represents an empty or default message instance for the GameEventScriptMessage class.
    /// This instance is commonly used to signify the absence of meaningful data
    /// or as a default value in operations involving game event script messaging.
    /// </summary>
    public static readonly GameEventScriptMessage Empty = new(string.Empty);

    /// <summary>
    /// Gets the name of the message associated with the GameEventScriptMessage instance.
    /// This name serves as a key identifier for the message, which is normalized to ensure
    /// a consistent format and allow for accurate handling and comparison within the
    /// Game Event Script system.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Represents the named arguments associated with a Game Event Script message.
    /// This property holds a collection of key-value pairs where keys are argument names
    /// and values are their corresponding data. It provides access to additional contextual
    /// information required to process the message during event-driven execution.
    /// </summary>
    /// <remarks>
    /// If no arguments are provided, this property defaults to an empty collection.
    /// The arguments are immutable and are used to construct the message's unique
    /// signature for identification and dispatch purposes.
    /// </remarks>
    public GameEventScriptNamedArguments Arguments { get; }

    /// <summary>
    /// Represents a unique identifier for the signature of a <see cref="GameEventScriptMessage"/>.
    /// The value is derived from the normalized name of the message and its associated arguments,
    /// allowing for precise matching and routing within the game event script system.
    /// </summary>
    public string SignatureId { get; }

    /// <summary>
    /// Returns a string representation of the current GameEventScriptMessage instance.
    /// This representation includes the message name and, if present, its associated arguments.
    /// </summary>
    /// <returns>A string that represents the current GameEventScriptMessage instance.</returns>
    public override string ToString() => Arguments.Count == 0 ? Name : $"{Name}({Arguments})";

    private GameEventScriptMessage(string name, GameEventScriptNamedArguments? arguments = null)
    {
        Name = GameEventScriptMessageSignature.NormalizeMessageName(name);
        Arguments = arguments ?? GameEventScriptNamedArguments.Empty;
        SignatureId = GameEventScriptMessageSignature.CreateSignatureId(Name, Arguments.SignatureLabels);
    }

    private GameEventScriptMessage(string normalizedName, GameEventScriptNamedArguments arguments, string signatureId)
    {
        Name = normalizedName;
        Arguments = arguments;
        SignatureId = signatureId;
    }

    internal static GameEventScriptMessage CreatePrecomputed(string normalizedName, GameEventScriptNamedArguments arguments, string signatureId) => new(normalizedName, arguments, signatureId);
}