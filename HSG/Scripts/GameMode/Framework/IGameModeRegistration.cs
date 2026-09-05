using System.Collections;
using Virial.Game;
using Virial.Text;

namespace hvtXsvc.GameMode.Framework;

/// <summary>
/// 游戏模式注册结果，持有 Nebula 内部模式定义与元数据。
/// </summary>
public interface IGameModeRegistration
{
    /// <summary>Nebula 内部模式定义（位掩码 / 显示名 / 最小人数）。</summary>
    GameModeDefinition Definition { get; }

    /// <summary>本地化键（如 gamemode.xxx）。</summary>
    string TranslationKey { get; }

    /// <summary>创建房间时允许的最小玩家数。</summary>
    int MinPlayers { get; }
}

/// <summary>
/// 模式模块类型提供者：返回能被 Nebula 容器识别的模块类型。
/// </summary>
public interface IGameModeModuleProvider
{
    /// <summary>Nebula 可实例化的模块类型（通常指向 Nebula 已注册的 IGameModeStandard 等）。</summary>
    Type ModuleType { get; }
}

/// <summary>
/// 分配器工厂：每次游戏开局时创建角色分配器。
/// </summary>
public interface IGameModeAllocatorFactory
{
    /// <summary>创建角色分配器实例。</summary>
    IRoleAllocator CreateAllocator();
}

/// <summary>
/// 模式属性：决定模式下的 HUD / 结算 / 称号行为。
/// </summary>
public interface IGameModeProperties
{
    /// <summary>允许特殊游戏结束（如内鬼全灭）。</summary>
    bool AllowSpecialGameEnd { get; }

    /// <summary>显示小地图按钮。</summary>
    bool ShowMap { get; }

    /// <summary>结算时显示统计面板。</summary>
    bool ShowStatistics { get; }

    /// <summary>显示使用按钮（UseButton）。</summary>
    bool ShowButtons { get; }

    /// <summary>只能使用印记（绘图模式用）。</summary>
    bool CanUseStampOnly { get; }

    /// <summary>该模式能否获取称号。</summary>
    bool CanGetTitle { get; }

    /// <summary>能否打开帮助界面。</summary>
    bool CanOpenHelpScreen { get; }
}

/// <summary>
/// 本地化键提供。
/// </summary>
public interface IGameModeLocalizable
{
    /// <summary>模式名称本地化键。</summary>
    string TranslationKey { get; }
}

/// <summary>
/// 配置项注册：模式模块可在创建阶段定义专属配置。
/// </summary>
public interface IGameModeConfigurable
{
    /// <summary>注册该模式专属的配置项。</summary>
    void DefineConfigs();
}

/// <summary>
/// 生命周期钩子：模式模块随游戏开始 / 结束触发。
/// </summary>
public interface IGameModeLifecycle
{
    /// <summary>游戏开始（intro 结束）时调用。</summary>
    void OnGameStart();

    /// <summary>游戏结束时调用。</summary>
    void OnGameEnd();
}
