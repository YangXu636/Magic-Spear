using BepInEx.Logging;
using UnityEngine;
using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using RWCustom;
using IL.Menu;

namespace MagicSpear
{
    public class ModOptionMenu : OptionInterface
    {
        public readonly Configurable<bool> color;
        public readonly Configurable<bool> ifdestroy;
        public readonly Configurable<KeyCode> unequip;
        public readonly Configurable<KeyCode> craftable;
        public readonly Configurable<float> ringradius;
        public readonly Configurable<float> examrange;
        private UIelement[] UIArrPlayerOptions;
        public ModOptionMenu()
        {
            this.color = this.config.Bind<bool>("Owl_MagicSpear_Bool_Color", true, (ConfigurableInfo)null);
            this.ifdestroy = this.config.Bind<bool>("Owl_MagicSpear_Bool_IfDestroy", true, (ConfigurableInfo)null);
            this.craftable = this.config.Bind<KeyCode>("Owl_MagicSpear_KeyCode_Crafeable", KeyCode.N, (ConfigurableInfo) null);
            this.unequip = this.config.Bind<KeyCode>("Owl_MagicSpear_KeyCode_Unequip", KeyCode.U, (ConfigurableInfo)null);
            this.ringradius = this.config.Bind<float>("Owl_MagicSpear_Float_RingRadius", 100f, new ConfigAcceptableRange<float>(50f, 150f));
            this.examrange = this.config.Bind<float>("Owl_MagicSpear_Float_ExamRange", 600f, new ConfigAcceptableRange<float>(100f,900f));
        }

        public override void Initialize()
        {

            var opTab = new OpTab(this, "Options");
            this.Tabs = new[]
            {
            opTab
        };
            UIArrPlayerOptions = new UIelement[]
            {
            new OpLabel(10f, 550f, "Options", true),
            new OpLabel(10f, 520f, Custom.rainWorld.inGameTranslator.Translate("The radius of spear ring"), true),
            new OpUpdown(ringradius, new Vector2(10f,490f), 100f, 1),
            new OpLabel(10f, 460f, Custom.rainWorld.inGameTranslator.Translate("The radius of danger exame"), true),
            new OpUpdown(examrange, new Vector2(10f, 430f),100f, 1),
            new OpLabel(10f, 400f, Custom.rainWorld.inGameTranslator.Translate("The key to make spears into magic spears"), true),
            new OpKeyBinder(this.craftable, new Vector2(10f, 370f), new Vector2(120f, 20f), true, OpKeyBinder.BindController.AnyController),
            new OpLabel(10f, 340f, Custom.rainWorld.inGameTranslator.Translate("The key to revert magic spears back to normal spears"), true),
            new OpKeyBinder(this.unequip, new Vector2(10f, 310f), new Vector2(120f, 20f), true, OpKeyBinder.BindController.AnyController),
            new OpLabel(10f, 280f, Custom.rainWorld.inGameTranslator.Translate("Color the spears"), true),
            new OpCheckBox(this.color,10f, 250f),
/*            new OpLabel(10f, 220f,Custom.rainWorld.inGameTranslator.Translate("Destroy the spears after shooting"), true),
            new OpCheckBox(this.ifdestroy, 10f, 190f)*/
            };
            opTab.AddItems(UIArrPlayerOptions);
        }

        public override void Update()
        {
            base.Update();
        }

    }
}
