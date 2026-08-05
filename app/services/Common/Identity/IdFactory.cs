using IdGen;

namespace Common.Identity;

/// <summary>
/// Time-ordered ids generated in the application, so a row knows its identity before it is written.
/// </summary>
/// <remarks>
/// The trip service writes a trip and consumes its quote in one transaction, and the capture outbox
/// keys an intent by the trip that caused it. Both need the id in hand before the insert, which is
/// what rules out a database-assigned key here.
/// </remarks>
public sealed class IdFactory
{
    private readonly IdGenerator _generator;

    public IdFactory(int generatorId, DateTimeOffset epoch)
    {
        var structure = new IdStructure(45, 2, 16);
        var options = new IdGeneratorOptions(structure, new DefaultTimeSource(epoch));
        _generator = new IdGenerator(generatorId, options);
    }

    public long Create() => _generator.CreateId();
}
