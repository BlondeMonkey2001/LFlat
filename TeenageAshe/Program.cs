#region

using System;
using System.Collections.Generic;
using System.Drawing;
using LeagueSharp;
using LeagueSharp.Common;

#endregion

namespace TeenageAshe
{
    internal class Program
    {
        //set champion name here
        private const string ChampionName = "Ashe";
        private static readonly Spell[] SpellArray = new Spell[4];

        private static Obj_AI_Hero player;
        private static Orbwalking.Orbwalker orbwalker;
        private static Spell q, w, e, r;
        private static Menu config;

        private static Behaviour combo;
        //private static Behaviour harass; not needed for ashe
        private static Behaviour ks;

        private static bool isQActive = false;
        private static Obj_AI_Hero target;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            try
            {
                if (ObjectManager.Player.BaseSkinName != ChampionName)
                {
                    return;
                }

                player = ObjectManager.Player;

                Console.WriteLine("TenageAshe[0]: Loading started");

                //set ranges
                q = new Spell(SpellSlot.Q);
                w = new Spell(SpellSlot.W, 1200);
                e = new Spell(SpellSlot.E);
                r = new Spell(SpellSlot.R, 20000);

                Console.WriteLine("TenageAshe[1]: Spells created");

                w.SetSkillshot(0.25f, (float)(6.0 * 9.58 * Math.PI / 180.0), 1500.0f, true, SkillshotType.SkillshotCone);
                r.SetSkillshot(0.25f, 250.0f, 1600.0f, false, SkillshotType.SkillshotLine);

                Console.WriteLine("TenageAshe[2]: Spells W and R skillshots defined");

                SpellArray[0] = q;
                SpellArray[1] = w;
                SpellArray[2] = e;
                SpellArray[3] = r;

                Console.WriteLine("TenageAshe[3]: All 4 spells added to the SpellArray");

                //the last valee determine if this is a root menu
                config = new Menu("Teenage Ashe", "TeenAshe", true);
                Console.WriteLine("TenageAshe[4]: Main Menu of the Mod created");

                //Adds a submenu named Orbwalker
                config.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));

                Console.WriteLine("TenageAshe[5]: Empty Orbwalker SubMenu created and added to Main Menu");

                //initializes the orbwalker and add it to the previously created Menu Orbwalker
                orbwalker = new Orbwalking.Orbwalker(config.SubMenu("Orbwalker"));

                Console.WriteLine("TenageAshe[6]: Orbwalker initialized and used to fill up Orbwalker Menu");


                config.AddSubMenu(new Menu("Combo", "Combo"));
                Console.WriteLine("TenageAshe[7]: Empty Combo SubMenu created");
                //Console.WriteLine("Finn has a new love: teenage Princess Ashe!");

                //adds toggable MenuItems to the submenu "Combo". The SetValue method sets the default value for those MenuItems
                config.SubMenu("Combo").AddItem(new MenuItem("UseQCombo", "Use Q").SetValue(true)).ValueChanged += onComboSpellValueChanged;
                config.SubMenu("Combo").AddItem(new MenuItem("UseWCombo", "Use W").SetValue(true)).ValueChanged += onComboSpellValueChanged;
                config.SubMenu("Combo").AddItem(new MenuItem("UseRCombo", "Use R").SetValue(true)).ValueChanged += onComboSpellValueChanged;
                config.SubMenu("Combo").AddItem(new MenuItem("UseICombo", "Use Items").SetValue(true));

                Console.WriteLine("TenageAshe[8]: Submenu Combo filled up with skills to use");

                //continue to define the submenu Combo, adds another MenuItem, this one activates on KeyPress 
                config.SubMenu("Combo").AddItem(new MenuItem("ComboActive", "Combo!").SetValue(new KeyBind(32, KeyBindType.Press)));

                Console.WriteLine("TenageAshe[9]: MenuItem ComboActive added to Menu Combo");

                //defining the Harass submenu. Note that you may want to remove a few MenuItems from there!
                config.AddSubMenu(new Menu("Harass", "Harass"));
                config.SubMenu("Harass").AddItem(new MenuItem("UseWHarass", "Use W").SetValue(true));

                //those are the two modes: harass on keypress, and toggable harass
                config.SubMenu("Harass").AddItem(new MenuItem("HarassPress", "Harass!").SetValue(new KeyBind("A".ToCharArray()[0], KeyBindType.Press)));
                config.SubMenu("Harass").AddItem(new MenuItem("HarassToggle", "Harass (toggle)!").SetValue(new KeyBind("Y".ToCharArray()[0], KeyBindType.Toggle)));

                config.AddSubMenu(new Menu("Killsteal", "Killsteal"));
                config.SubMenu("Killsteal").AddItem(new MenuItem("UseWKS", "Use W to KS").SetValue(true)).ValueChanged += onKsSpellValueChanged;
                config.SubMenu("Killsteal").AddItem(new MenuItem("UseRKS", "Use R to KS").SetValue(true)).ValueChanged += onKsSpellValueChanged;
                config.SubMenu("Killsteal").AddItem(new MenuItem("ShouldKS", "Should KS").SetValue(true)).ValueChanged += onKsSpellValueChanged;

                //defining the drawings submenu. Again, you may want to remove some of the MenuItems in case they make no sense
                config.AddSubMenu(new Menu("Drawings", "Drawings"));
                config.SubMenu("Drawings").AddItem(new MenuItem("WRange", "W range").SetValue(new Circle(true, Color.White)));
                config.SubMenu("Drawings").AddItem(new MenuItem("ERange", "E range").SetValue(new Circle(true, Color.White)));
                config.SubMenu("Drawings").AddItem(new MenuItem("RRange", "R range").SetValue(new Circle(true, Color.White)));


                Menu targetSelectorMenu = new Menu("Target Selector", "Target Selector");
                SimpleTs.AddToMenu(targetSelectorMenu);
                config.AddSubMenu(targetSelectorMenu);

                Console.WriteLine("Message 10");
                //this step adds the menu of our mod to the main L# menu
                config.AddToMainMenu();

                Console.WriteLine("Message 11");

                Drawing.OnDraw += onDraw;
                Game.OnGameUpdate += onGameUpdate;
                Orbwalking.AfterAttack += onAfterAttack;
                Orbwalking.BeforeAttack += onBeforeAttack;

                Console.WriteLine("Finish");
            }
            catch(Exception ex)
            {
                Console.WriteLine("An Exception has ocurred: ");
                Console.WriteLine(ex.Message);
            }
        }

        private static void onBeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            isQActive = player.HasBuff("FrostShot");
            if (args.Target is Obj_AI_Hero)
            {
                if (!isQActive)
                    q.Cast();
            }
            else
            {
                q.Cast();
            }
        }

        private static void onComboSpellValueChanged(object sender, OnValueChangeEventArgs args)
        {
            MenuItem senderAsMenuItem = sender as MenuItem;

            switch (senderAsMenuItem.Name)
            {
                case "UseQCombo":
                    combo.UseQ = args.GetNewValue<bool>();
                    break;
                case "UseWCombo":
                    combo.UseW = args.GetNewValue<bool>();
                    break;
                case "UseRCombo":
                    combo.UseR = args.GetNewValue<bool>();
                    break;
            }
        }

        private static void onKsSpellValueChanged(object sender, OnValueChangeEventArgs args)
        {
            MenuItem senderAsMenuItem = sender as MenuItem;

            switch (senderAsMenuItem.Name)
            {
                case "UseWKS":
                    ks.UseW = args.GetNewValue<bool>();
                    break;
                case "UseRKS":
                    ks.UseR = args.GetNewValue<bool>();
                    break;
            }
        }

        private static void onDraw(EventArgs args)
        {
            if (SpellArray == null)
                return;

            foreach (Spell spell in SpellArray)
            {
                Circle menuItem = config.Item(spell.Slot + "Range").GetValue<Circle>();

                if (menuItem.Active)
                    Utility.DrawCircle(ObjectManager.Player.Position, spell.Range, menuItem.Color);
            }
        }

        private static void onGameUpdate(EventArgs args)
        {
            if (config.Item("ComboActive").GetValue<KeyBind>().Active)
                doCombo();

            if (config.Item("ShouldKS").GetValue<bool>())
                doKillSteal();

            if ((config.Item("HarassPress").GetValue<KeyBind>().Active) || (config.Item("HarassToggle").GetValue<KeyBind>().Active))
                doHarass();
        }

        private static void onAfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            //don't know what to do here yet
        }

        private static void doHarass()
        {
            Obj_AI_Hero target = SimpleTs.GetTarget(w.Range, SimpleTs.DamageType.Physical);
            if (w.IsReady())
            {
                bool hit = w.CastIfHitchanceEquals(target, HitChance.Medium, true);
            }
        }

        private static void doKillSteal()
        {
            foreach (Obj_AI_Hero champion in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (champion.IsValidTarget())
                {

                    List<Tuple<SpellSlot, int>> spellsForDmgCalc = new List<Tuple<SpellSlot, int>>(2);

                    if (ks.UseW && w.IsReady() && w.WillHit(champion, player.ServerPosition, 10, HitChance.Medium))
                        spellsForDmgCalc.Add(Tuple.Create(w.Slot, 0));

                    if (ks.UseR && r.IsReady() && r.WillHit(champion, player.ServerPosition, 10, HitChance.Medium))
                        spellsForDmgCalc.Add(Tuple.Create(r.Slot, 0));

                    if (Damage.IsKillable(player, champion, spellsForDmgCalc))
                    {
                        foreach (Tuple<SpellSlot, int> spell in spellsForDmgCalc)
                        {
                            player.Spellbook.CastSpell(spell.Item1, champion.ServerPosition + new SharpDX.Vector3(champion.BoundingRadius));
                        }

                    }
                }
            }
        }

        private static void doCombo()
        {
            if (combo.UseQ && !isQActive && q.IsReady())
                q.Cast();

            if (combo.UseW && w.IsReady())
            {
                target = SimpleTs.GetTarget(w.Range, SimpleTs.DamageType.Physical);
                if (target != null)
                {
                    w.CastIfHitchanceEquals(target, HitChance.Medium, true);
                }
                else if (combo.UseR && r.IsReady())//target was null for W, try R, if possible
                {
                    target = SimpleTs.GetTarget(r.Range, SimpleTs.DamageType.Magical);
                    if (target != null)
                    {
                        r.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                }

                //target is escaping
                if (target.Distance(player) > player.AttackRange && target.MoveSpeed > player.MoveSpeed)
                {
                    if (combo.UseW && w.IsReady())
                        w.CastIfHitchanceEquals(target, HitChance.Medium, true);
                    else if (combo.UseR && r.IsReady())
                        r.CastIfHitchanceEquals(target, HitChance.Medium, true);
                }
            }
        }
    }
}
