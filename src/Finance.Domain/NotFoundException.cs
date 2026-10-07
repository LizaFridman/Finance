namespace Finance.Domain;

/// <summary>
/// A referenced entity does not exist. Thrown by application use cases; the host
/// maps it to HTTP 404.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entity, string id)
        : base($"{entity} '{id}' was not found.")
    {
        Entity = entity;
        Id = id;
    }

    public string Entity { get; }
    public string Id { get; }
}
