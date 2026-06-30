using Godot;

/// <summary>
/// 通用数值曲线 Resource。
/// 包装 Godot 内置 Curve，附加 ItemId 用于 ConfigManager 索引。
/// </summary>
[GlobalClass]
public partial class LevelCurve : Resource
{
    /// <summary>曲线唯一 ID，如 "curve_stamina_cost"</summary>
    [Export] public string ItemId { get; set; }

    /// <summary>Godot 内置曲线资源。X=输入, Y=输出。Sample() 自动钳制越界。</summary>
    [Export] public Curve ValueCurve { get; set; }

    /// <summary>采样曲线值，曲线为 null 返回 0</summary>
    public float Sample(float x)
    {
        if (ValueCurve == null) return 0f;
        return ValueCurve.Sample(x);
    }

    /// <summary>带默认值的采样，曲线为 null 返回 defaultValue</summary>
    public float SampleOrDefault(float x, float defaultValue = 1.0f)
    {
        if (ValueCurve == null) return defaultValue;
        return ValueCurve.Sample(x);
    }
}