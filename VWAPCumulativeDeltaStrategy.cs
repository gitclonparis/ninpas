#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion


namespace NinjaTrader.NinjaScript.Strategies.ninpas
{
    public class VWAPCumulativeDeltaStrategy : Strategy
    {
        #region Paramètres configurables
        [NinjaScriptProperty]
        [Display(Name = "Période de réinitialisation du VWAP (minutes)", Order = 1, GroupName = "Paramètres")]
        public int VWAPResetPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Niveaux des extensions std", Order = 2, GroupName = "Paramètres")]
        public double VWAPStdDeviationLevel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Stop Loss (ticks)", Order = 3, GroupName = "Gestion des risques")]
        public int StopLossTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Take Profit (ticks)", Order = 4, GroupName = "Gestion des risques")]
        public int TakeProfitTicks { get; set; }
        #endregion

        #region Variables
        private double vwapValue;
        private double vwapStdDev;
        private double vwapPlus1;
        private double vwapMinus1;

        private double lastCumulativeDeltaHigh;
        private double cumulativeDeltaValue;

        private DateTime vwapResetTime;
        private bool isVWAPReset;

        private Series<double> cumVolume;
        private Series<double> cumTypicalPriceVolume;
        private Series<double> cumTypicalPriceSquaredVolume;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                                     = "Stratégie basée sur le VWAP avec réinitialisation périodique et cumulative delta.";
                Name                                            = "VWAPCumulativeDeltaStrategy";
                Calculate                                       = Calculate.OnBarClose;
                EntriesPerDirection                             = 1;
                EntryHandling                                   = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy                    = true;
                ExitOnSessionCloseSeconds                       = 30;
                IsInstantiatedOnEachOptimizationIteration       = false;

                // Paramètres par défaut
                VWAPResetPeriod                                 = 120;
                VWAPStdDeviationLevel                           = 1;
                StopLossTicks                                   = 10;
                TakeProfitTicks                                 = 20;

                // Initialisation des variables
                lastCumulativeDeltaHigh                         = double.MinValue;
                isVWAPReset                                     = false;
            }
            else if (State == State.Configure)
            {
                // Rien à configurer pour le moment
            }
            else if (State == State.DataLoaded)
            {
                // Initialisation des séries pour les calculs cumulatifs
                cumVolume                       = new Series<double>(this);
                cumTypicalPriceVolume           = new Series<double>(this);
                cumTypicalPriceSquaredVolume    = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            // Vérifier que nous avons suffisamment de données
            if (CurrentBar < BarsRequiredToTrade)
                return;

            // Gérer la réinitialisation du VWAP
            ManageVWAPReset();

            // Calculer le VWAP et ses extensions
            CalculateVWAPAndExtensions();

            // Mettre à jour le cumulative delta
            UpdateCumulativeDelta();

            // Détecter la cassure du dernier swing haussier du cumulative delta
            bool cumulativeDeltaBreakout = DetectCumulativeDeltaBreakout();

            // Conditions d'entrée en position longue
            if (PriceBreaksAboveVWAPPlus1() && cumulativeDeltaBreakout)
            {
                EnterLong();
                Draw.ArrowUp(this, "EntryLong" + CurrentBar, false, 0, Low[0] - 2 * TickSize, Brushes.Green);
            }

            // Gestion des risques
            if (Position.MarketPosition == MarketPosition.Long)
            {
                SetStopLoss(CalculationMode.Ticks, StopLossTicks);
                SetProfitTarget(CalculationMode.Ticks, TakeProfitTicks);
            }

            // Visualisation
            PlotVWAPAndExtensions();

            // Journalisation
            LogData();
        }

        private void ManageVWAPReset()
        {
            // Initialiser le temps de réinitialisation du VWAP
            if (CurrentBar == 0)
            {
                vwapResetTime = Times[0][0].AddMinutes(VWAPResetPeriod);
            }

            // Vérifier si nous devons réinitialiser le VWAP
            if (Times[0][0] >= vwapResetTime)
            {
                isVWAPReset = true;
                vwapResetTime = vwapResetTime.AddMinutes(VWAPResetPeriod);
            }
            else
            {
                isVWAPReset = false;
            }
        }

        private void CalculateVWAPAndExtensions()
        {
            // Calculer le prix typique et le volume
            double typicalPrice = (High[0] + Low[0] + Close[0]) / 3;
            double volume = Volume[0];

            // Réinitialiser les sommes cumulatives si nécessaire
            if (isVWAPReset || CurrentBar == 0)
            {
                cumVolume[0] = volume;
                cumTypicalPriceVolume[0] = typicalPrice * volume;
                cumTypicalPriceSquaredVolume[0] = typicalPrice * typicalPrice * volume;
            }
            else
            {
                cumVolume[0] = cumVolume[1] + volume;
                cumTypicalPriceVolume[0] = cumTypicalPriceVolume[1] + typicalPrice * volume;
                cumTypicalPriceSquaredVolume[0] = cumTypicalPriceSquaredVolume[1] + typicalPrice * typicalPrice * volume;
            }

            // Calculer le VWAP
            vwapValue = cumTypicalPriceVolume[0] / cumVolume[0];

            // Calculer l'écart-type
            double meanOfSquares = cumTypicalPriceSquaredVolume[0] / cumVolume[0];
            double squareOfMean = vwapValue * vwapValue;
            vwapStdDev = Math.Sqrt(meanOfSquares - squareOfMean);

            // Calculer les extensions
            vwapPlus1 = vwapValue + VWAPStdDeviationLevel * vwapStdDev;
            vwapMinus1 = vwapValue - VWAPStdDeviationLevel * vwapStdDev;
        }

        private void UpdateCumulativeDelta()
        {
            // Calculer le delta du bar
            double delta = (Close[0] > Open[0]) ? Volume[0] : -Volume[0];

            // Mettre à jour le cumulative delta
            cumulativeDeltaValue = (CurrentBar == 0) ? delta : cumulativeDeltaValue + delta;
        }

        private bool DetectCumulativeDeltaBreakout()
        {
            // Détecter le dernier swing haussier
            if (cumulativeDeltaValue > lastCumulativeDeltaHigh)
            {
                lastCumulativeDeltaHigh = cumulativeDeltaValue;
                return true;
            }
            return false;
        }

        private bool PriceBreaksAboveVWAPPlus1()
        {
            // Vérifier si le prix casse au-dessus de l'extension +1 du VWAP
            return CrossAbove(Close, vwapPlus1, 1);
        }

       private void PlotVWAPAndExtensions()
		{
			// Afficher le VWAP et ses extensions sur le graphique
			Draw.Line(this, "VWAPLine", false, Time[1], vwapValue, Time[0], vwapValue, Brushes.Blue, DashStyleHelper.Solid, 2);
			Draw.Line(this, "VWAPPlus1Line", false, Time[1], vwapPlus1, Time[0], vwapPlus1, Brushes.Red, DashStyleHelper.Solid, 2);
			Draw.Line(this, "VWAPMinus1Line", false, Time[1], vwapMinus1, Time[0], vwapMinus1, Brushes.Green, DashStyleHelper.Solid, 2);
		}

        private void LogData()
        {
            // Journaliser les informations pour le débogage
            Print(string.Format("Bar {0}: VWAP={1}, VWAP+1={2}, VWAP-1={3}, Cumulative Delta={4}", CurrentBar, vwapValue, vwapPlus1, vwapMinus1, cumulativeDeltaValue));
        }
    }
}