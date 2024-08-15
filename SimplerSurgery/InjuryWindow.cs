using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;
using HarmonyLib;
using RimWorld;

namespace SimplerSurgery
{
    public class InjuryWindow : Window
    {
        private Hediff hediff;

        public Hediff Hediff { get { return hediff; } }

        public InjuryWindow(Hediff hediff)
        {
            this.hediff = hediff;
            this.doCloseButton = true;
            this.closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(300f, 200f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f),
                $"Injury: {hediff.LabelCap}");

            Widgets.Label(new Rect(0f, 40f, inRect.width, 30f),
                $"Part: {hediff.Part.LabelCap}");
            Widgets.Label(new Rect(0f, 80f, inRect.width, 30f),
                $"Severity: {hediff.Severity}");

        }
    }
}
