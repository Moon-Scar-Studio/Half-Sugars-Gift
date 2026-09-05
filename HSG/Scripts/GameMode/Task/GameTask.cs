using Virial.Game;

namespace hvtXsvc.GameMode.Task;

/// <summary>任务定义。</summary>
public interface IGameTaskDefinition
{
    string TaskKey { get; }
    string DisplayNameKey { get; }
    int MinCount { get; }
    int MaxCount { get; }
    bool IsAvailable(GameModeDefinition gameMode);
    IGameTaskInstance CreateInstance(byte playerId);
}

/// <summary>任务实例。</summary>
public interface IGameTaskInstance
{
    IGameTaskDefinition Definition { get; }
    byte PlayerId { get; }
}

/// <summary>任务实例工厂。</summary>
public interface IGameTaskFactory
{
    IGameTaskInstance Create(byte playerId);
}

/// <summary>任务定义的基础实现，仅负责定义和实例创建。</summary>
public abstract class GameTaskDefinitionBase : IGameTaskDefinition, IGameTaskFactory
{
    public abstract string TaskKey { get; }
    public virtual string DisplayNameKey => $"task.{TaskKey}.name";
    public virtual int MinCount => 0;
    public virtual int MaxCount => 1;
    public virtual bool IsAvailable(GameModeDefinition gameMode) => true;
    public virtual IGameTaskInstance CreateInstance(byte playerId) => new GameTaskInstance(this, playerId);
    IGameTaskInstance IGameTaskFactory.Create(byte playerId) => CreateInstance(playerId);

    private sealed class GameTaskInstance(IGameTaskDefinition definition, byte playerId) : IGameTaskInstance
    {
        public IGameTaskDefinition Definition { get; } = definition;
        public byte PlayerId { get; } = playerId;
    }
}
