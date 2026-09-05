using System.Collections;
using System.Reflection;
using Nebula.Roles.Assignment;
using Virial.Assignable;
using Virial.Game;
using Virial.Text;

namespace hvtXsvc.GameMode.Framework;

/// <summary>游戏模式构建器。</summary>
public interface IGameModeBuilder
{
    IGameModeBuilder WithTranslationKey(string translationKey);
    IGameModeBuilder WithMinPlayers(int minPlayers);
    IGameModeBuilder WithModuleType(Type moduleType);
    IGameModeBuilder WithAllocator(Func<IRoleAllocator> allocator);
    IGameModeBuilder WithAlternativeRoutine(Func<bool, IEnumerator> routine);
    IGameModeBuilder WithoutRoleSettings();
    IGameModeBuilder WithoutAutoAdd();
    IGameModeRegistration Register();
}

/// <summary>通过 Nebula 内部模式定义实现注册的构建器。</summary>
public sealed class GameModeBuilder : IGameModeBuilder
{
    private string translationKey = "gamemode.custom";
    private int minPlayers = 1;
    private Type moduleType = typeof(IGameModeStandard);
    private Func<IRoleAllocator> allocator = static () => new StandardRoleAllocator();
    private Func<bool, IEnumerator>? alternativeRoutine;
    private bool withRoleSettings = true;
    private bool autoAdd = true;

    public static IGameModeBuilder Create() => new GameModeBuilder();

    public static IGameModeRegistration Register<TModule>(string translationKey, int minPlayers)
        where TModule : class
    {
        return Create()
            .WithTranslationKey(translationKey)
            .WithMinPlayers(minPlayers)
            .WithModuleType(typeof(TModule))
            .Register();
    }

    public IGameModeBuilder WithTranslationKey(string value) { translationKey = value; return this; }
    public IGameModeBuilder WithMinPlayers(int value) { minPlayers = value; return this; }
    public IGameModeBuilder WithModuleType(Type value) { moduleType = value; return this; }
    public IGameModeBuilder WithAllocator(Func<IRoleAllocator> value) { allocator = value; return this; }
    public IGameModeBuilder WithAlternativeRoutine(Func<bool, IEnumerator> value) { alternativeRoutine = value; return this; }
    public IGameModeBuilder WithoutRoleSettings() { withRoleSettings = false; return this; }
    public IGameModeBuilder WithoutAutoAdd() { autoAdd = false; return this; }

    public IGameModeRegistration Register()
    {
        var definitionType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("Nebula.Game.GameModeDefinitionImpl", false))
            .FirstOrDefault(type => type is not null)
            ?? throw new InvalidOperationException("找不到 Nebula 游戏模式定义实现。");
        if (autoAdd && alternativeRoutine == null && withRoleSettings)
        {
            var constructor = definitionType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(int), typeof(Type), typeof(Func<IRoleAllocator>) },
                null) ?? throw new InvalidOperationException("找不到 Nebula 游戏模式定义构造函数。");
            var standardDefinition = (GameModeDefinition)constructor.Invoke(new object?[]
            {
                translationKey, minPlayers, moduleType, allocator
            });
            return new GameModeRegistration(standardDefinition, translationKey, minPlayers);
        }

        var fullConstructor = definitionType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            new[] { typeof(string), typeof(int), typeof(Type), typeof(Func<IRoleAllocator>), typeof(Func<bool, IEnumerator>), typeof(bool), typeof(bool) },
            null) ?? throw new InvalidOperationException("找不到 Nebula 游戏模式定义构造函数。");
        var customDefinition = (GameModeDefinition)fullConstructor.Invoke(new object?[]
        {
            translationKey, minPlayers, moduleType, allocator, alternativeRoutine, withRoleSettings, !autoAdd
        });

        if (autoAdd)
        {
            var modes = typeof(GameModes).GetField("allGameModes", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("找不到 Nebula 游戏模式列表。");
            var list = (IList)modes.GetValue(null)!;
            if (!list.Contains(customDefinition)) list.Add(customDefinition);
        }

        var definition = customDefinition;

        return new GameModeRegistration(definition, translationKey, minPlayers);
    }

    private sealed class GameModeRegistration(GameModeDefinition definition, string translationKey, int minPlayers) : IGameModeRegistration
    {
        public GameModeDefinition Definition { get; } = definition;
        public string TranslationKey { get; } = translationKey;
        public int MinPlayers { get; } = minPlayers;
    }
}

/// <summary>模式模块的公开扩展基类。实际容器由 Nebula 管理。</summary>
public abstract class GameModeModuleBase : IGameModeProperties, IGameModeConfigurable, IGameModeLifecycle
{
    public virtual bool AllowSpecialGameEnd => true;
    public virtual bool ShowMap => true;
    public virtual bool ShowStatistics => true;
    public virtual bool ShowButtons => true;
    public virtual bool CanUseStampOnly => false;
    public virtual bool CanGetTitle => true;
    public virtual bool CanOpenHelpScreen => true;
    public virtual string? GetAlternativeWinOrLoseText() => null;
    public virtual string? GetAlternativePlayerStatusText() => null;
    public virtual void DefineConfigs() { }
    public virtual void OnGameStart() { }
    public virtual void OnGameEnd() { }
}

/// <summary>标准角色分配器工厂。</summary>
public static class GameModeAllocators
{
    public static IRoleAllocator Standard() => new StandardRoleAllocator();
    public static IRoleAllocator FreePlay() => new FreePlayRoleAllocator();
}
