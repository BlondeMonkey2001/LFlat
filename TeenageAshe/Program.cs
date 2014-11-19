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
        //private static Orbwalking.Orbwalker orbwalker;
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
            if (ObjectManager.Player.BaseSkinName != ChampionName)
            {
                return;
            }

            player = ObjectManager.Player;

            Game.PrintChat("Message 1");
            //set ranges
            q = new Spell(SpellSlot.Q);
            w = new Spell(SpellSlot.W, 1200);
            e = new Spell(SpellSlot.E);
            r = new Spell(SpellSlot.R, 20000);
            Game.PrintChat("Message 2");
            w.SetSkillshot(0.25f, (float)(6.0 * 9.58 * Math.PI / 180.0), 1500.0f, true, SkillshotType.SkillshotCone);
            r.SetSkillshot(0.25f, 250.0f, 1600.0f, false, SkillshotType.SkillshotLine);
            Game.PrintChat("Message 3");
            SpellArray[0] = q;
            SpellArray[1] = w;
            SpellArray[2] = e;
            SpellArray[3] = r;
            Game.PrintChat("Message 4");
            //the last valee determine if this is a root menu
            config = new Menu("Teenage Ashe", "TeenAshe", true);
            Game.PrintChat("Message 5");
            //config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));
            Game.PrintChat("Message 6");
            var orbwalkerMenu = new Menu("Orbwalking", "Orbwalking");
            orbwalker = new Orbwalking.Orbwalker(orbwalkerMenu);
            config.AddSubMenu(orbwalkerMenu);
            //orbwalker = new Orbwalking.Orbwalker(config.SubMenu("Orbwalking"));
            Game.PrintChat("Message 7");
            config.AddSubMenu(new Menu("Combo", "Combo"));
            Game.PrintChat("Message 8");
            //Game.PrintChat("Finn has a new love: teenage Princess Ashe!");

            //adds toggable MenuItems to the submenu "Combo". The SetValue method sets the default value for those MenuItems
            config.SubMenu("Combo").AddItem(new MenuItem("UseQCombo", "Use Q").SetValue(true)).ValueChanged += onComboSpellValueChanged;
            config.SubMenu("Combo").AddItem(new MenuItem("UseWCombo", "Use W").SetValue(true)).ValueChanged += onComboSpellValueChanged;
            config.SubMenu("Combo").AddItem(new MenuItem("UseRCombo", "Use R").SetValue(true)).ValueChanged += onComboSpellValueChanged;
            config.SubMenu("Combo").AddItem(new MenuItem("UseICombo", "Use Items").SetValue(true));

            Game.PrintChat("Message 9");

            //continue to define the submenu Combo, adds another MenuItem, this one activates on KeyPress 
            config.SubMenu("Combo").AddItem(new MenuItem("ComboActive", "Combo!").SetValue(new KeyBind(32, KeyBindType.Press)));

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


            var ts = new Menu("Target Selector", "Target Selector");
            SimpleTs.AddToMenu(ts);
            config.AddSubMenu(ts);

            Game.PrintChat("Message 10");
            //this step adds the menu of our mod to the main L# menu
            config.AddToMainMenu();
            Game.PrintChat("Message 11");
            Drawing.OnDraw += onDraw;
            Game.OnGameUpdate += onGameUpdate;
            Orbwalking.AfterAttack += onAfterAttack;
            Orbwalking.BeforeAttack += onBeforeAttack;

            Game.PrintChat("Finish");
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
