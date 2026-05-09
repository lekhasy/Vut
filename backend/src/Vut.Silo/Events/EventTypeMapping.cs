using System.Collections.Frozen;
using System.Collections.Concurrent;

namespace Vut.Silo.Events;

/// <summary>
/// Provides bidirectional mapping between event type name strings (used in KurrentDB)
/// and their corresponding CLR types. Event types must be registered at application
/// startup before any grains are activated.
/// </summary>
public static class EventTypeMapping
{
    private static readonly ConcurrentDictionary<string, Type> NameToType = new();
    private static readonly ConcurrentDictionary<Type, string> TypeToName = new();

    private static FrozenDictionary<string, Type>? _frozenNameToType;
    private static FrozenDictionary<Type, string>? _frozenTypeToName;

    /// <summary>
    /// Registers a mapping between a KurrentDB event type name and a CLR type.
    /// Must be called during application startup before the silo begins accepting requests.
    /// </summary>
    /// <typeparam name="TEvent">The CLR type of the event.</typeparam>
    /// <param name="eventTypeName">
    /// The event type name as stored in KurrentDB (e.g., "UserCreated").
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the event type name is null/empty, or when a duplicate mapping is detected.
    /// </exception>
    public static void Register<TEvent>(string eventTypeName) where TEvent : class, IEvent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);

        if (_frozenNameToType is not null)
        {
            throw new InvalidOperationException(
                "Event type mappings have been frozen and cannot be modified. " +
                "Register all event types before calling Freeze().");
        }

        var type = typeof(TEvent);

        if (!NameToType.TryAdd(eventTypeName, type))
        {
            throw new ArgumentException(
                $"Event type name '{eventTypeName}' is already registered to {NameToType[eventTypeName].FullName}.",
                nameof(eventTypeName));
        }

        if (!TypeToName.TryAdd(type, eventTypeName))
        {
            throw new ArgumentException(
                $"CLR type '{type.FullName}' is already registered with name '{TypeToName[type]}'.",
                nameof(eventTypeName));
        }
    }

    /// <summary>
    /// Freezes the mappings for optimal read performance. Call once after all registrations are complete.
    /// </summary>
    public static void Freeze()
    {
        _frozenNameToType = NameToType.ToFrozenDictionary();
        _frozenTypeToName = TypeToName.ToFrozenDictionary();
    }

    /// <summary>
    /// Gets the KurrentDB event type name for a given CLR type.
    /// </summary>
    /// <param name="clrType">The CLR type of the event.</param>
    /// <returns>The event type name string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the CLR type is not registered.</exception>
    public static string GetTypeName(Type clrType)
    {
        if (_frozenTypeToName is not null)
        {
            return _frozenTypeToName.TryGetValue(clrType, out var frozenName)
                ? frozenName
                : throw new InvalidOperationException(
                    $"No event type name registered for CLR type '{clrType.FullName}'. " +
                    "Ensure the event type is registered via EventTypeMapping.Register<T>() at startup.");
        }

        return TypeToName.TryGetValue(clrType, out var name)
            ? name
            : throw new InvalidOperationException(
                $"No event type name registered for CLR type '{clrType.FullName}'. " +
                "Ensure the event type is registered via EventTypeMapping.Register<T>() at startup.");
    }

    /// <summary>
    /// Gets the CLR type for a given KurrentDB event type name.
    /// </summary>
    /// <param name="eventTypeName">The event type name string from KurrentDB.</param>
    /// <returns>The corresponding CLR type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the event type name is not registered.</exception>
    public static Type GetClrType(string eventTypeName)
    {
        if (_frozenNameToType is not null)
        {
            return _frozenNameToType.TryGetValue(eventTypeName, out var frozenType)
                ? frozenType
                : throw new InvalidOperationException(
                    $"No CLR type registered for event type name '{eventTypeName}'. " +
                    "Ensure the event type is registered via EventTypeMapping.Register<T>() at startup.");
        }

        return NameToType.TryGetValue(eventTypeName, out var type)
            ? type
            : throw new InvalidOperationException(
                $"No CLR type registered for event type name '{eventTypeName}'. " +
                "Ensure the event type is registered via EventTypeMapping.Register<T>() at startup.");
    }

    /// <summary>
    /// Resets all registered mappings. Intended for testing only.
    /// </summary>
    internal static void Reset()
    {
        _frozenNameToType = null;
        _frozenTypeToName = null;
        NameToType.Clear();
        TypeToName.Clear();
    }
}
