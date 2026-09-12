namespace NebulaN.Roles.Modifier;

public class WIDE : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, RuntimeAssignableGenerator<RuntimeModifier>,HasCitation
{
    static FloatConfiguration zuiXiao = NebulaAPI.Configurations.Configuration("options.modifier.wide.min", (0.01f, 10.01f, 1f), 0.1f, FloatConfigurationDecorator.Ratio);
    static FloatConfiguration zuiDa = NebulaAPI.Configurations.Configuration("options.modifier.wide.max", (0.5f, 20f, 1f), 15f, FloatConfigurationDecorator.Ratio);
    static FloatConfiguration jianGe = NebulaAPI.Configurations.Configuration("options.modifier.wide.jiange", (0.01f, 1f, 0.01f), 0.05f, FloatConfigurationDecorator.Second);

    private WIDE() : base("WIDE", "W", new Virial.Color(0.5f, 0.8f, 1f), new IConfiguration[] { zuiXiao, zuiDa, jianGe })
    {
    }

    static public WIDE MyRole = new WIDE();

    public Citation Citation => Citations.hvtXsvc_hsg;
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/WIDEIcon.png")?.AsImage();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole as DefinedModifier ?? throw new System.InvalidOperationException();
        const string SizeTag = "HSG.WIDE.Size";

        public Instance(GamePlayer player) : base(player)
        {
        }

        System.Collections.IEnumerator? xieCheng;
        bool huoYue;

        void RuntimeAssignable.OnActivated()
        {
            huoYue = false;
            if (AmOwner)
            {
                huoYue = true;
                xieCheng = GengXinXieCheng();
                NebulaManager.Instance.StartCoroutine(xieCheng.WrapToIl2Cpp());
                SuiJiChange();
            }
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo || inEndScene)
            {
                name += " ↔".Color(new UnityEngine.Color(0.5f, 0.8f, 1f));
            }
        }

        void IGameOperator.OnReleased()
        {
            huoYue = false;
            xieCheng = null;
            MyPlayer.RemoveAttributeByTag(SizeTag);
        }

        System.Collections.IEnumerator GengXinXieCheng()
        {
            while (huoYue && !MyPlayer.IsDead)
            {
                yield return new WaitForSeconds(jianGe);
                if (huoYue && !MyPlayer.IsDead) SuiJiChange();
            }
        }

        void SuiJiChange()
        {
            float min = zuiXiao;
            float max = zuiDa;
            if (min > max) (min, max) = (max, min);
            float v = UnityEngine.Random.Range(min, max);
            // GainSizeAttribute 自带全网同步，且同 tag 会直接替换旧属性：
            // 不需要先 RemoveAttributeByTag（原实现每 0.05 秒发一次移除 RPC + 一次尺寸 RPC，纯浪费），
            // 也不需要绕道 HostSendRpc。HIGH / WIDE 使用不同 tag，同时持有时不会互相覆盖。
            MyPlayer.GainSizeAttribute(new Virial.Compat.Vector2(v, 1f), 1000f, true, 51, SizeTag);
        }
    }
}
