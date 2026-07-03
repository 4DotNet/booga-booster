using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Microsoft.Extensions.Options;
using Faker = Bogus.Faker;
using Randomizer = Bogus.Randomizer;

namespace FourDotnet.BoogaBooster.Queue.Filling;

/// <summary>
/// Default <see cref="IPersonGenerator"/>. Hands out ever-increasing unique person
/// numbers, generates full names with Bogus, and draws each weight from a normal
/// distribution centred in the typical 70–100 kg band so most guests are average
/// while lighter and heavier exceptions still occur across the full
/// <c>[<see cref="Person.MinWeightInKilograms"/>, <see cref="Person.MaxWeightInKilograms"/>]</c>
/// range. Registered as a singleton so the unique-number counter is shared; its
/// randomness honours <see cref="QueueModuleOptions.RandomSeed"/> for deterministic tests.
/// </summary>
internal sealed class PersonGenerator : IPersonGenerator
{
    // Centre and spread of the weight distribution. The mean sits in the middle of
    // the likely 70–100 kg band; the spread is chosen so that band holds the bulk
    // of the mass yet the clamped tails still reach 30 kg and 150 kg on occasion.
    private const double MeanWeightInKilograms = 85.0;
    private const double WeightStandardDeviation = 12.0;

    private readonly Faker _faker;
    private long _lastNumber;

    public PersonGenerator(IOptions<QueueModuleOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _faker = new Faker();
        if (options.Value.RandomSeed is { } seed)
        {
            _faker.Random = new Randomizer(seed);
        }
    }

    public Person Next()
    {
        var number = Interlocked.Increment(ref _lastNumber);
        var name = _faker.Name.FullName();
        var weight = NextWeight();

        return new Person(number, name, weight);
    }

    public GroupArrival CreateGroup(int size)
    {
        if (size < 1)
        {
            throw new DomainValidationException("A group must contain at least one person.");
        }

        var members = new Person[size];
        for (var i = 0; i < size; i++)
        {
            members[i] = Next();
        }

        return new GroupArrival(Guid.NewGuid(), members);
    }

    /// <summary>
    /// Draws a weight from a normal distribution (Box–Muller) and clamps it to the
    /// permitted range, yielding mostly typical weights with lighter/heavier exceptions.
    /// </summary>
    private int NextWeight()
    {
        // (0, 1] on the first draw keeps the logarithm well-defined.
        var u1 = 1.0 - _faker.Random.Double();
        var u2 = _faker.Random.Double();
        var standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

        var weight = MeanWeightInKilograms + (WeightStandardDeviation * standardNormal);
        var clamped = Math.Clamp(weight, Person.MinWeightInKilograms, Person.MaxWeightInKilograms);

        return (int)Math.Round(clamped);
    }
}
