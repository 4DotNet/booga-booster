namespace FourDotnet.BoogaBooster.Core.Cqrs;

/// <summary>
/// Base type for a command — an intent to change state (ADR-0005). A command's
/// response is optional, so the base type carries no response. Commands are
/// plain data; the behavior lives in the matching
/// <see cref="ICommandHandler{TCommand}"/>.
/// </summary>
public abstract record Command;
