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
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.ninpas
{
    public class VbvaV3 : Strategy
    {
        private double sumPriceVolume;
        private double sumVolume;
        private double sumSquaredPriceVolume;
        private DateTime lastResetTime;
        private int barsSinceReset;
        private int upperBreakoutCount;
        private int lowerBreakoutCount;
        
        private VOL VOL1;
        private VOLMA VOLMA1;
		
        // Variables for tracking STD3 highs and lows
        private double highestSTD3Upper;
        private double lowestSTD3Lower;
        private bool isFirstBarSinceReset;
		
        private double previousSessionHighStd1Upper;
        private double previousSessionLowStd1Lower;
        private DateTime currentSessionStartTime;
        private double highestStd1Upper;
        private double lowestStd1Lower;

        private int figVA;
        private bool figVAPointsDrawn;
		
		private double previousSessionVAUpperLevel = double.MinValue;
		private double previousSessionVALowerLevel = double.MaxValue;
		
		// Ajoutez ces variables au début de la classe VvabSkel03
		private double ibHigh = double.MinValue;
		private double ibLow = double.MaxValue;
		private bool ibPeriod = true;
		private SessionIterator sessionIterator;
		private DateTime currentDate = DateTime.MinValue;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Indicateur BVA-Limusine combiné";
                Name = "VbvaV3";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsInstantiatedOnEachOptimizationIteration	= false;
				
				Period1Start = DateTime.Parse("15:30", System.Globalization.CultureInfo.InvariantCulture);
				Period1End = DateTime.Parse("17:30", System.Globalization.CultureInfo.InvariantCulture);
				Period2Start = DateTime.Parse("18:30", System.Globalization.CultureInfo.InvariantCulture);
				Period2End = DateTime.Parse("20:30", System.Globalization.CultureInfo.InvariantCulture);
				ClosePositionsOutsideHours = false;
				Qty											= 5;
				Sl											= 15;
				Pt											= 10;
				EnableDynamicStops = false;
				EnableDynamicStopLossMultiplier = false;
				DynamicStopLossMultiplier = 1.0;
				EnableDynamicTargetMultiplier = false;
				DynamicTargetMultiplier = 1.0;
				UseBarSizeForStops = false;
				EnableBarStopLossMultiplier = false;
				BarStopLossMultiplier = 1.0;
				EnableBarTargetMultiplier = false;
				BarTargetMultiplier = 1.0;
				
				
				UseParabolicStop = false;
				PreventMultiplePositions = false;
				

                // Paramètres BVA
                ResetPeriod = 60;
				figVA = ResetPeriod - 1;
                figVAPointsDrawn = false;
                MinBarsForSignal = 5;
                MaxBarsForSignal = 60;
                MinEntryDistanceUP = 3;
                MaxEntryDistanceUP = 40;
                MaxUpperBreakouts = 3;
                MinEntryDistanceDOWN = 3;
                MaxEntryDistanceDOWN = 40;
                MaxLowerBreakouts = 3;
				BlockSignalsInPreviousValueArea = false;
				ValueAreaOffsetTicks = 0;
				UsePrevBarInVA = false;
                FperiodVol = 9;

                // Paramètres Limusine
				ActiveBuy = true;
				ActiveSell = true;
                MinimumTicks = 10;
                MaximumTicks = 30;
                ShowLimusineOpenCloseUP = true;
                ShowLimusineOpenCloseDOWN = true;
                ShowLimusineHighLowUP = false;
                ShowLimusineHighLowDOWN = false;
                
                EnableDistanceFromVWAPCondition = false;
                MinDistanceFromVWAP = 10;
                MaxDistanceFromVWAP = 50;
                
                EnableSTD3HighLowTracking = false;
                EnablePreviousSessionRangeBreakout = false;
				EnableSTD1RangeCheck = false;
				MinSTD1Range = 20;
				MaxSTD1Range = 100;

                AddPlot(Brushes.Orange, "VWAP");
                AddPlot(Brushes.Purple, "StdDev0.5Upper");
                AddPlot(Brushes.Purple, "StdDev0.5Lower");
                AddPlot(Brushes.Red, "StdDev1Upper");
                AddPlot(Brushes.Red, "StdDev1Lower");
                AddPlot(Brushes.Green, "StdDev2Upper");
                AddPlot(Brushes.Green, "StdDev2Lower");
                AddPlot(Brushes.Blue, "StdDev3Upper");
                AddPlot(Brushes.Blue, "StdDev3Lower");

                SignalTimingMode = SignalTimeMode.Bars;
                MinMinutesForSignal = 5;
                MaxMinutesForSignal = 30;
                SelectedValueArea = ValueAreaLevel.STD1;
                useOpenForVAConditionUP = false;
                useOpenForVAConditionDown = false;
                useLowForVAConditionUP = false;
                useHighForVAConditionDown = false;
				SelectedEntryLevelUp = EntryLevelChoice.STD1;    // Au lieu de STD05
				SelectedEntryLevelDown = EntryLevelChoice.STD1;  // Au lieu de STD05
				
				// Ajoutez les valeurs par défaut pour Initial Balance
				EnableIBLogic = false;
				IBStartTime = DateTime.Parse("15:30", System.Globalization.CultureInfo.InvariantCulture);
				IBEndTime = DateTime.Parse("16:00", System.Globalization.CultureInfo.InvariantCulture);
				IBOffsetTicks = 0;
				
				// Paramètres par défaut pour les breakout checks
				EnableUpBreakoutCheck = false;
				UpBreakoutBars = 2;
				UpBreakoutOffsetTicks = 1; // Doit être >= 1
				
				EnableDownBreakoutCheck = false;
				DownBreakoutBars = 2;
				DownBreakoutOffsetTicks = 1; // Doit être >= 1
				
				// Dots
				SelectedDotPlotMode = DotPlotMode.STD1; // Valeur par défaut
				SelectedBarSizeType = BarSizeType.CloseOpen;
				RedDotMultiplier = 1.0;
				BlueDotMultiplier = 1.0;
				STD1RedDotMultiplier = 0.5;
				STD1BlueDotMultiplier = 1.5;
            }
            else if (State == State.Configure)
            {
                ResetValues(DateTime.MinValue);
            }
            else if (State == State.DataLoaded)
            {
                VOL1 = VOL(Close);
                VOLMA1 = VOLMA(Close, Convert.ToInt32(FperiodVol));
				sessionIterator = new SessionIterator(Bars);
            }
        }

        protected override void OnBarUpdate()
        {
			// if (BarsInProgress != 0) 
				// return;
            if (CurrentBars[0] < 20)
                return;
			
			bool isInTradingPeriod = IsInTradingPeriod(Time[0]);
			if (ClosePositionsOutsideHours && !isInTradingPeriod)
			{
				foreach (Position position in Positions)
				{
					if (position.MarketPosition == MarketPosition.Long)
					{
						ExitLong();
					}
					else if (position.MarketPosition == MarketPosition.Short)
					{
						ExitShort();
					}
				}
			}
			
			figVA = ResetPeriod - 1;
            DateTime currentBarTime = Time[0];
            bool shouldReset = false;

            if (Bars.IsFirstBarOfSession)
            {
                shouldReset = true;
            }
            else if (currentSessionStartTime != DateTime.MinValue && (currentBarTime - currentSessionStartTime).TotalMinutes >= ResetPeriod)
            {
                shouldReset = true;
            }
            
            if (shouldReset)
            {
                if (EnablePreviousSessionRangeBreakout)
                {
                    previousSessionHighStd1Upper = highestStd1Upper;
                    previousSessionLowStd1Lower = lowestStd1Lower;
                }
    
                ResetValues(currentBarTime);
                currentSessionStartTime = currentBarTime;
            }
            
            // Calcul VWAP et écarts-types
            double typicalPrice = (High[0] + Low[0] + Close[0]) / 3;
            double volume = Volume[0];

            sumPriceVolume += typicalPrice * volume;
            sumVolume += volume;
            sumSquaredPriceVolume += typicalPrice * typicalPrice * volume;

            double vwap = sumPriceVolume / sumVolume;
            double variance = (sumSquaredPriceVolume / sumVolume) - (vwap * vwap);
            double stdDev = Math.Sqrt(variance);

            // Assigner les valeurs aux plots
            Values[0][0] = vwap;
            Values[1][0] = vwap + 0.5 * stdDev;
            Values[2][0] = vwap - 0.5 * stdDev;
            Values[3][0] = vwap + stdDev;
            Values[4][0] = vwap - stdDev;
            Values[5][0] = vwap + 2 * stdDev;
            Values[6][0] = vwap - 2 * stdDev;
            Values[7][0] = vwap + 3 * stdDev;
            Values[8][0] = vwap - 3 * stdDev;
            
            if (EnablePreviousSessionRangeBreakout)
            {
                highestStd1Upper = Math.Max(highestStd1Upper, Values[3][0]);
                lowestStd1Lower = Math.Min(lowestStd1Lower, Values[4][0]);
            }

            if (EnableSTD3HighLowTracking)
            {
                if (isFirstBarSinceReset)
                {
                    highestSTD3Upper = Values[7][0];
                    lowestSTD3Lower = Values[8][0];
                    isFirstBarSinceReset = false;
                }
                else
                {
                    highestSTD3Upper = Math.Max(highestSTD3Upper, Values[7][0]);
                    lowestSTD3Lower = Math.Min(lowestSTD3Lower, Values[8][0]);
                }
            }
            
            barsSinceReset++;

            TimeSpan timeSinceReset = Time[0] - lastResetTime;
            if (timeSinceReset.TotalMinutes >= figVA && !figVAPointsDrawn)
            {
                double upperLevel = 0;
                double lowerLevel = 0;
                
                // Sélectionner les niveaux en fonction de ValueAreaLevel
                switch (SelectedValueArea)
                {
                    case ValueAreaLevel.STD05:
                        upperLevel = Values[1][0]; // StdDev0.5 Upper
                        lowerLevel = Values[2][0]; // StdDev0.5 Lower
                        break;
                    case ValueAreaLevel.STD1:
                        upperLevel = Values[3][0]; // StdDev1 Upper
                        lowerLevel = Values[4][0]; // StdDev1 Lower
                        break;
                    case ValueAreaLevel.STD2:
                        upperLevel = Values[5][0]; // StdDev2 Upper
                        lowerLevel = Values[6][0]; // StdDev2 Lower
                        break;
                    case ValueAreaLevel.STD3:
                        upperLevel = Values[7][0]; // StdDev3 Upper
                        lowerLevel = Values[8][0]; // StdDev3 Lower
                        break;
                }
				// Appliquer l'offset aux niveaux
				upperLevel += ValueAreaOffsetTicks * TickSize;
				lowerLevel -= ValueAreaOffsetTicks * TickSize;
                // Dessiner les points
                Draw.Dot(this, "FigVAUpper" + CurrentBar, true, 0, upperLevel, Brushes.Yellow);
                Draw.Dot(this, "FigVALower" + CurrentBar, true, 0, lowerLevel, Brushes.Yellow);
                previousSessionVAUpperLevel = upperLevel;
				previousSessionVALowerLevel = lowerLevel;
                figVAPointsDrawn = true;
            }
            
            // Réinitialiser figVAPointsDrawn lors d'un reset
            if (shouldReset)
            {
                figVAPointsDrawn = false;
            }
			
			if (isInTradingPeriod && !HasExistingPositions())
			{
				//
				if (ActiveBuy && ShouldDrawUpArrow())
				{
					SetEntryParameters(true);
					EnterLong(Convert.ToInt32(Qty), @"Long");
					Draw.ArrowUp(this, "UpArrow" + CurrentBar, true, 0, Low[0] - 2 * TickSize, Brushes.Green);
					upperBreakoutCount++;
					var (redDotPrice, blueDotPrice) = CalculateDotLevels(true, Close[0]);
					Draw.Dot(this, "RedDotUp" + CurrentBar, true, 0, redDotPrice, Brushes.Red);
					Draw.Dot(this, "BlueDotUp" + CurrentBar, true, 0, blueDotPrice, Brushes.Blue);
					Draw.Dot(this, "WhiteDotUp" + CurrentBar, true, 0, Close[0], Brushes.White);
				}
				else if (ActiveSell && ShouldDrawDownArrow())
				{
					SetEntryParameters(false);
					EnterShort(Convert.ToInt32(Qty), @"Short");
					Draw.ArrowDown(this, "DownArrow" + CurrentBar, true, 0, High[0] + 2 * TickSize, Brushes.Red);
					lowerBreakoutCount++;
					var (redDotPrice, blueDotPrice) = CalculateDotLevels(false, Close[0]);
					Draw.Dot(this, "RedDotDown" + CurrentBar, true, 0, redDotPrice, Brushes.Red);
					Draw.Dot(this, "BlueDotDown" + CurrentBar, true, 0, blueDotPrice, Brushes.Blue);
					Draw.Dot(this, "WhiteDotDown" + CurrentBar, true, 0, Close[0], Brushes.White);
				}
			}
        }
		// ############################################################################################################### //
		// ############################################################################################################### //
		
		private bool IsInTradingPeriod(DateTime time)
		{
			//
			// Obtenir la session de trading actuelle
			var tradingDay = sessionIterator.GetTradingDay(time);
			
			// Construire les dates/heures complètes pour les périodes de trading
			var period1StartTime = tradingDay.Date + Period1Start.TimeOfDay;
			var period1EndTime = tradingDay.Date + Period1End.TimeOfDay;
			var period2StartTime = tradingDay.Date + Period2Start.TimeOfDay;
			var period2EndTime = tradingDay.Date + Period2End.TimeOfDay;
			
			// Gérer le cas où la période se termine le jour suivant
			if (Period1End.TimeOfDay < Period1Start.TimeOfDay)
				period1EndTime = period1EndTime.AddDays(1);
			if (Period2End.TimeOfDay < Period2Start.TimeOfDay)
				period2EndTime = period2EndTime.AddDays(1);
			
			// Vérifier si le temps actuel est dans l'une des périodes de trading
			bool inPeriod1 = time >= period1StartTime && time <= period1EndTime;
			bool inPeriod2 = time >= period2StartTime && time <= period2EndTime;
			
			return inPeriod1 || inPeriod2;
		}
		
		// Ajouter une nouvelle fonction de vérification des positions
		private bool HasExistingPositions()
		{
			if (!PreventMultiplePositions)
				return false;
		
			foreach (Position position in Positions)
			{
				// Si une position existe déjà (longue ou courte), retourner true
				if (position.MarketPosition == MarketPosition.Long || 
					position.MarketPosition == MarketPosition.Short)
				{
					return true;
				}
			}
			return false;
		}
		
		//
		private void SetDynamicStopsAndTargets(bool isLong)
		{
			double vwap = Values[0][0];
			double stdDev1Upper = Values[3][0];
			double stdDev1Lower = Values[4][0];
		
			double ptDistance;
			double slDistance;
		
			if (isLong)
			{
				ptDistance = (stdDev1Upper - vwap) / TickSize;
				slDistance = (vwap - stdDev1Lower) / TickSize;
			}
			else
			{
				ptDistance = (vwap - stdDev1Lower) / TickSize;
				slDistance = (stdDev1Upper - vwap) / TickSize;
			}
		
			// Appliquer les multiplicateurs si activés
			if (EnableDynamicStopLossMultiplier)
			{
				slDistance = Math.Max(1, Math.Round(slDistance * DynamicStopLossMultiplier));
			}
			else
			{
				slDistance = Math.Max(1, Math.Round(Math.Abs(slDistance)));
			}
		
			if (EnableDynamicTargetMultiplier)
			{
				ptDistance = Math.Max(1, Math.Round(ptDistance * DynamicTargetMultiplier));
			}
			else
			{
				ptDistance = Math.Max(1, Math.Round(Math.Abs(ptDistance)));
			}
		
			// Définir le stop loss et le profit target dynamiques
			SetStopLoss(CalculationMode.Ticks, slDistance);
			SetProfitTarget(CalculationMode.Ticks, ptDistance);
		}
		
		private void SetEntryParameters(bool isLong)
		{
			if (UseParabolicStop)
			{
				SetParabolicStop(isLong ? "Long" : "Short", CalculationMode.Ticks, Sl, true, 0.09, 0.9, 0.09);
				SetProfitTarget(isLong ? "Long" : "Short", CalculationMode.Ticks, Pt);
			}
			else if (UseBarSizeForStops)
			{
				SetStopsBasedOnBarSize(isLong);
			}
			else if (EnableDynamicStops)
			{
				SetDynamicStopsAndTargets(isLong);
			}
			else
			{
				SetStopLoss(CalculationMode.Ticks, Sl);
				SetProfitTarget(CalculationMode.Ticks, Pt);
			}
		}
		
		private void SetStopsBasedOnBarSize(bool isLong)
		{
			// Calculer la taille de la barre actuelle en ticks
			double barSizeTicks = (High[0] - Low[0]) / TickSize;
			barSizeTicks = Math.Max(1, Math.Round(Math.Abs(barSizeTicks)));
		
			// Appliquer les multiplicateurs si activés
			double stopLossTicks = barSizeTicks;
			double targetTicks = barSizeTicks;
		
			if (EnableBarStopLossMultiplier)
			{
				stopLossTicks = Math.Max(1, Math.Round(barSizeTicks * BarStopLossMultiplier));
			}
		
			if (EnableBarTargetMultiplier)
			{
				targetTicks = Math.Max(1, Math.Round(barSizeTicks * BarTargetMultiplier));
			}
		
			// Définir le stop loss et le profit target
			SetStopLoss(CalculationMode.Ticks, stopLossTicks);
			SetProfitTarget(CalculationMode.Ticks, targetTicks);
		}
		
		// ############################################################################################################### //
		// ############################################################################################################### //
		//
		 private double GetSelectedLevel(EntryLevelChoice choice, bool isUpper)
		{
			switch (choice)
			{
				case EntryLevelChoice.STD05:
					return isUpper ? Values[1][0] : Values[2][0];
				case EntryLevelChoice.STD1:
					return isUpper ? Values[3][0] : Values[4][0];
				case EntryLevelChoice.STD2:
					return isUpper ? Values[5][0] : Values[6][0];
				case EntryLevelChoice.STD3:
					return isUpper ? Values[7][0] : Values[8][0];
				default:
					return isUpper ? Values[3][0] : Values[4][0];
			}
		}
		//
		private bool IsPriceInPreviousValueArea()
		{
			if (!BlockSignalsInPreviousValueArea || previousSessionVAUpperLevel == double.MinValue || previousSessionVALowerLevel == double.MaxValue)
				return false;
		
			// Ajoute l'offset à la Value Area
			double upperLevelWithOffset = previousSessionVAUpperLevel + (ValueAreaOffsetTicks * TickSize);
			double lowerLevelWithOffset = previousSessionVALowerLevel - (ValueAreaOffsetTicks * TickSize);
		
			return Close[0] <= upperLevelWithOffset && Close[0] >= lowerLevelWithOffset;
		}

		// ############################################################################################################### //
		// Ajoutez cette nouvelle méthode pour gérer la logique IB
		private void ApplyIBLogic(ref bool showUpArrow, ref bool showDownArrow)
		{
			if (!EnableIBLogic)
				return;
		
			DateTime barTime = Time[0];
			DateTime tradingDay = sessionIterator.GetTradingDay(barTime);
		
			// Détection du début de session
			if (currentDate != tradingDay)
			{
				currentDate = tradingDay;
				ibHigh = double.MinValue;
				ibLow = double.MaxValue;
				ibPeriod = true;
			}
		
			// Déterminer l'heure de début et de fin de l'IB pour la session actuelle
			DateTime ibStart = tradingDay.AddHours(IBStartTime.Hour).AddMinutes(IBStartTime.Minute);
			DateTime ibEnd = tradingDay.AddHours(IBEndTime.Hour).AddMinutes(IBEndTime.Minute);
		
			// Gérer le cas où l'heure de fin est inférieure à l'heure de début (IB traversant minuit)
			if (ibEnd <= ibStart)
				ibEnd = ibEnd.AddDays(1);
		
			// Pendant la période IB
			if (barTime >= ibStart && barTime <= ibEnd)
			{
				ibHigh = Math.Max(ibHigh, High[0]);
				ibLow = Math.Min(ibLow, Low[0]);
			}
			// Après la période IB
			else if (barTime > ibEnd && ibPeriod)
			{
				ibPeriod = false;
			}
		
			// Appliquer la logique IB aux conditions existantes
			if (!ibPeriod && ibHigh != double.MinValue && ibLow != double.MaxValue)
			{
				double upperBreak = ibHigh + IBOffsetTicks * TickSize;
				double lowerBreak = ibLow - IBOffsetTicks * TickSize;
		
				// Modifier les conditions showUpArrow et showDownArrow
				showUpArrow = showUpArrow && (Close[0] >= lowerBreak);
				showDownArrow = showDownArrow && (Close[0] <= upperBreak);
			}
		}
		// ############################################################################################################### //

		
		// ############################################################################################################### //
		// ############################################################################################################### //
		// ############################################################################################################### //

        private bool ShouldDrawUpArrow()
        {
			// Vérifier si le prix est dans la Value Area précédente
			if (BlockSignalsInPreviousValueArea && IsPriceInPreviousValueArea())
				return false;
			
			// Vérifier la condition de range STD1 si activée
			if (EnableSTD1RangeCheck)
			{
				double std1Range = (Values[3][0] - Values[4][0]) / TickSize; // STD1Upper - STD1Lower en ticks
				if (std1Range < MinSTD1Range || std1Range > MaxSTD1Range)
					return false;
			}
			
            double vwap = Values[0][0];
            double distanceInTicks = (Close[0] - vwap) / TickSize;
			double selectedUpperLevel = GetSelectedLevel(SelectedEntryLevelUp, true);
            double upperThreshold, lowerThreshold;
            
            switch (SelectedValueArea)
            {
                case ValueAreaLevel.STD05:
                    upperThreshold = Values[1][0]; // StdDev0.5 Upper
                    lowerThreshold = Values[2][0]; // StdDev0.5 Lower
                    break;
                case ValueAreaLevel.STD1:
                    upperThreshold = Values[3][0]; // StdDev1 Upper
                    lowerThreshold = Values[4][0]; // StdDev1 Lower
                    break;
                case ValueAreaLevel.STD2:
                    upperThreshold = Values[5][0]; // StdDev2 Upper
                    lowerThreshold = Values[6][0]; // StdDev2 Lower
                    break;
                case ValueAreaLevel.STD3:
                    upperThreshold = Values[7][0]; // StdDev3 Upper
                    lowerThreshold = Values[8][0]; // StdDev3 Lower
                    break;
                default:
                    upperThreshold = Values[3][0]; // Par défaut StdDev1 Upper
                    lowerThreshold = Values[4][0]; // Par défaut StdDev1 Lower
                    break;
            }

            bool withinSignalTime;
            if (SignalTimingMode == SignalTimeMode.Minutes)
            {
                TimeSpan timeSinceReset = Time[0] - lastResetTime;
                withinSignalTime = timeSinceReset.TotalMinutes >= MinMinutesForSignal && 
                                  timeSinceReset.TotalMinutes <= MaxMinutesForSignal;
            }
            else
            {
                withinSignalTime = barsSinceReset >= MinBarsForSignal && 
                                  barsSinceReset <= MaxBarsForSignal;
            }

            double selectedUpperThreshold;
            switch (SelectedValueArea)
            {
                case ValueAreaLevel.STD05:
                    selectedUpperThreshold = Values[1][0]; // StdDev0.5 Upper
                    break;
                case ValueAreaLevel.STD1:
                    selectedUpperThreshold = Values[3][0]; // StdDev1 Upper
                    break;
                case ValueAreaLevel.STD2:
                    selectedUpperThreshold = Values[5][0]; // StdDev2 Upper
                    break;
                case ValueAreaLevel.STD3:
                    selectedUpperThreshold = Values[7][0]; // StdDev3 Upper
                    break;
                default:
                    selectedUpperThreshold = Values[3][0]; // Par défaut StdDev1 Upper
                    break;
            }

            bool bvaCondition = (Close[0] > Open[0]) &&
                (!OKisVOL || (VOL1[0] > VOLMA1[0])) &&
                (!OKisAfterBarsSinceResetUP || withinSignalTime) &&
				(!OKisAboveUpperThreshold || Close[0] > (selectedUpperLevel + MinEntryDistanceUP * TickSize)) &&
				(!OKisWithinMaxEntryDistance || Close[0] <= (selectedUpperLevel + MaxEntryDistanceUP * TickSize)) &&
                (!OKisUpperBreakoutCountExceeded || upperBreakoutCount < MaxUpperBreakouts) &&
                (!useOpenForVAConditionUP || (Open[0] > lowerThreshold && Open[0] < upperThreshold)) &&
                (!useLowForVAConditionUP || (Low[0] > lowerThreshold && Low[0] < upperThreshold)) &&
				(!UsePrevBarInVA || (Open[1] > lowerThreshold && Open[1] < upperThreshold));

            double openCloseDiff = Math.Abs(Open[0] - Close[0]) / TickSize;
            double highLowDiff = Math.Abs(High[0] - Low[0]) / TickSize;
            bool limusineCondition = (ShowLimusineOpenCloseUP && openCloseDiff >= MinimumTicks && openCloseDiff <= MaximumTicks && Close[0] > Open[0]) ||
                                    (ShowLimusineHighLowUP && highLowDiff >= MinimumTicks && highLowDiff <= MaximumTicks && Close[0] > Open[0]);

            bool std3Condition = !EnableSTD3HighLowTracking || Values[7][0] >= highestSTD3Upper;
            bool rangeBreakoutCondition = !EnablePreviousSessionRangeBreakout || 
                (previousSessionHighStd1Upper != double.MinValue && Close[0] > previousSessionHighStd1Upper);
            
            // return bvaCondition && limusineCondition && std3Condition && rangeBreakoutCondition;
			bool showUpArrow = bvaCondition && limusineCondition && std3Condition && rangeBreakoutCondition;
			
			//
			// Condition de cassure du plus haut des X dernières barres si activé
			if (EnableUpBreakoutCheck)
			{
				double highestHigh = double.MinValue;
				// Récupérer le plus haut des UpBreakoutBars dernières barres (par ex. de High[1] à High[UpBreakoutBars])
				for (int i = 1; i <= UpBreakoutBars; i++)
				{
					if (CurrentBar - i < 0) break; 
				
					// On prend la valeur la plus haute entre High, Open, Close, Low
					double barHighest = Math.Max(High[i], Math.Max(Open[i], Math.Max(Close[i], Low[i])));
					highestHigh = Math.Max(highestHigh, barHighest);
				}
		
				// Vérifier que Close[0] casse ce plus haut + offset en ticks
				if (!(Close[0] > highestHigh + UpBreakoutOffsetTicks * TickSize))
				{
					showUpArrow = false;
				}
			}
			
			// Appliquer la logique IB
			bool showDownArrow = false; // dummy variable nécessaire pour la méthode
			ApplyIBLogic(ref showUpArrow, ref showDownArrow);
		
			return showUpArrow;
        }

        private bool ShouldDrawDownArrow()
        {
			// Vérifier si le prix est dans la Value Area précédente
			if (BlockSignalsInPreviousValueArea && IsPriceInPreviousValueArea())
				return false;
			
			// Vérifier la condition de range STD1 si activée
			if (EnableSTD1RangeCheck)
			{
				double std1Range = (Values[3][0] - Values[4][0]) / TickSize; // STD1Upper - STD1Lower en ticks
				if (std1Range < MinSTD1Range || std1Range > MaxSTD1Range)
					return false;
			}
			
            double vwap = Values[0][0];
            double distanceInTicks = (vwap - Close[0]) / TickSize;
            double selectedLowerLevel = GetSelectedLevel(SelectedEntryLevelDown, false);
            double upperThreshold, lowerThreshold;
            
            switch (SelectedValueArea)
            {
                case ValueAreaLevel.STD05:
                    upperThreshold = Values[1][0]; // StdDev0.5 Upper
                    lowerThreshold = Values[2][0]; // StdDev0.5 Lower
                    break;
                case ValueAreaLevel.STD1:
                    upperThreshold = Values[3][0]; // StdDev1 Upper
                    lowerThreshold = Values[4][0]; // StdDev1 Lower
                    break;
                case ValueAreaLevel.STD2:
                    upperThreshold = Values[5][0]; // StdDev2 Upper
                    lowerThreshold = Values[6][0]; // StdDev2 Lower
                    break;
                case ValueAreaLevel.STD3:
                    upperThreshold = Values[7][0]; // StdDev3 Upper
                    lowerThreshold = Values[8][0]; // StdDev3 Lower
                    break;
                default:
                    upperThreshold = Values[3][0]; // Par défaut StdDev1 Upper
                    lowerThreshold = Values[4][0]; // Par défaut StdDev1 Lower
                    break;
            }

            bool withinSignalTime;
            if (SignalTimingMode == SignalTimeMode.Minutes)
            {
                TimeSpan timeSinceReset = Time[0] - lastResetTime;
                withinSignalTime = timeSinceReset.TotalMinutes >= MinMinutesForSignal && 
                                  timeSinceReset.TotalMinutes <= MaxMinutesForSignal;
            }
            else
            {
                withinSignalTime = barsSinceReset >= MinBarsForSignal && 
                                  barsSinceReset <= MaxBarsForSignal;
            }

            double selectedLowerThreshold;
            switch (SelectedValueArea)
            {
                case ValueAreaLevel.STD05:
                    selectedLowerThreshold = Values[2][0]; // StdDev0.5 Lower
                    break;
                case ValueAreaLevel.STD1:
                    selectedLowerThreshold = Values[4][0]; // StdDev1 Lower
                    break;
                case ValueAreaLevel.STD2:
                    selectedLowerThreshold = Values[6][0]; // StdDev2 Lower
                    break;
                case ValueAreaLevel.STD3:
                    selectedLowerThreshold = Values[8][0]; // StdDev3 Lower
                    break;
                default:
                    selectedLowerThreshold = Values[4][0]; // Par défaut StdDev1 Lower
                    break;
            }

            bool bvaCondition = (Close[0] < Open[0]) &&
                (!OKisVOL || (VOL1[0] > VOLMA1[0])) &&
                (!OKisAfterBarsSinceResetDown || withinSignalTime) &&
				(!OKisBelovLowerThreshold || Close[0] < (selectedLowerLevel - MinEntryDistanceDOWN * TickSize)) &&
				(!OKisWithinMaxEntryDistanceDown || Close[0] >= (selectedLowerLevel - MaxEntryDistanceDOWN * TickSize)) &&
                (!OKisLowerBreakoutCountExceeded || lowerBreakoutCount < MaxLowerBreakouts) &&
                (!useOpenForVAConditionDown || (Open[0] > lowerThreshold && Open[0] < upperThreshold)) &&
                (!useHighForVAConditionDown || (High[0] > lowerThreshold && High[0] < upperThreshold)) &&
				(!UsePrevBarInVA || (Open[1] > lowerThreshold && Open[1] < upperThreshold));

            double openCloseDiff = Math.Abs(Open[0] - Close[0]) / TickSize;
            double highLowDiff = Math.Abs(High[0] - Low[0]) / TickSize;
            bool limusineCondition = (ShowLimusineOpenCloseDOWN && openCloseDiff >= MinimumTicks && openCloseDiff <= MaximumTicks && Close[0] < Open[0]) ||
                                    (ShowLimusineHighLowDOWN && highLowDiff >= MinimumTicks && highLowDiff <= MaximumTicks && Close[0] < Open[0]);

            bool std3Condition = !EnableSTD3HighLowTracking || Values[8][0] <= lowestSTD3Lower;
            bool rangeBreakoutCondition = !EnablePreviousSessionRangeBreakout || 
                (previousSessionLowStd1Lower != double.MaxValue && Close[0] < previousSessionLowStd1Lower);
            
            // return bvaCondition && limusineCondition && std3Condition && rangeBreakoutCondition;
			bool showDownArrow = bvaCondition && limusineCondition && std3Condition && rangeBreakoutCondition;
			
			//
			if (EnableDownBreakoutCheck)
			{
				double lowestLow = double.MaxValue;
				// Récupérer le plus bas des DownBreakoutBars dernières barres (par ex. de Low[1] à Low[DownBreakoutBars])
				for (int i = 1; i <= DownBreakoutBars; i++)
				{
					if (CurrentBar - i < 0) break;
				
					// On prend la valeur la plus basse parmi Open, High, Low, Close
					double barLowest = Math.Min(Low[i], Math.Min(Open[i], Math.Min(Close[i], High[i])));
					lowestLow = Math.Min(lowestLow, barLowest);
				}
		
				// Vérifier que Close[0] casse ce plus bas - offset en ticks
				if (!(Close[0] < lowestLow - DownBreakoutOffsetTicks * TickSize))
				{
					showDownArrow = false;
				}
			}
			
			// Appliquer la logique IB
			bool showUpArrow = false; // dummy variable nécessaire pour la méthode
			ApplyIBLogic(ref showUpArrow, ref showDownArrow);
		
			return showDownArrow;
        }
        
        private void ResetSessionValues()
        {
            highestStd1Upper = double.MinValue;
            lowestStd1Lower = double.MaxValue;
        }

        private void ResetValues(DateTime resetTime)
        {
            sumPriceVolume = 0;
            sumVolume = 0;
            sumSquaredPriceVolume = 0;
            barsSinceReset = 0;
            upperBreakoutCount = 0;
            lowerBreakoutCount = 0;
            lastResetTime = resetTime;
            
            highestStd1Upper = double.MinValue;
            lowestStd1Lower = double.MaxValue;
    
            isFirstBarSinceReset = true;
            highestSTD3Upper = double.MinValue;
            lowestSTD3Lower = double.MaxValue;
			
			// previousVAUpperLevel = double.MinValue;
			// previousVALowerLevel = double.MaxValue;
        }
		
		// ############################################################################# //
		private (double redDotPrice, double blueDotPrice) CalculateDotLevels(bool isUpSignal, double closePrice)
		{
			double redDotPrice, blueDotPrice;
			
			switch (SelectedDotPlotMode)
			{
				case DotPlotMode.STD1:
					if (isUpSignal)
					{
						redDotPrice = Close[0] - (Values[0][0] - Values[4][0]); // VWAP - (STD1Upper - STD1Lower)
						blueDotPrice = Close[0] + (Values[3][0] -Values[0][0]); // VWAP + (STD1Upper - VWAP)
					}
					else
					{
						redDotPrice = Close[0] + (Values[3][0] - Values[0][0]); // VWAP + (STD1Upper - STD1Lower)
						blueDotPrice = Close[0] - (Values[0][0] - Values[4][0]); // VWAP - (VWAP - STD1Lower)
					}
					break;
					
				case DotPlotMode.STD2:
					if (isUpSignal)
					{
						redDotPrice = Close[0] - (Values[0][0] - Values[6][0]); // VWAP - (STD2Upper - STD2Lower)
						blueDotPrice = Close[0] + (Values[5][0] - Values[0][0]); // VWAP + (STD2Upper - VWAP)
					}
					else
					{
						redDotPrice = Close[0] + (Values[5][0] - Values[0][0]); // VWAP + (STD2Upper - STD2Lower)
						blueDotPrice = Close[0] - (Values[0][0] - Values[6][0]); // VWAP - (VWAP - STD2Lower)
					}
					break;
					
				//
				case DotPlotMode.UseSTD1Multiple:
					double std1Range = Values[3][0] - Values[4][0]; // STD1Upper - STD1Lower
					if (isUpSignal)
					{
						redDotPrice = Close[0] - (std1Range * STD1RedDotMultiplier); // Close - (STD1 range * multiplier)
						blueDotPrice = Close[0] + (std1Range * STD1BlueDotMultiplier); // Close + (STD1 range * multiplier)
					}
					else
					{
						redDotPrice = Close[0] + (std1Range * STD1RedDotMultiplier); // Close + (STD1 range * multiplier)
						blueDotPrice = Close[0] - (std1Range * STD1BlueDotMultiplier); // Close - (STD1 range * multiplier)
					}
					break;
				// 
				case DotPlotMode.BarSize:
					double barSize;
					
					// Calculer la taille de la barre selon le type sélectionné
					if (SelectedBarSizeType == BarSizeType.CloseOpen)
					{
						barSize = Math.Abs(Close[0] - Open[0]);
					}
					else // HighLow
					{
						barSize = Math.Abs(High[0] - Low[0]);
					}
					
					if (isUpSignal)
					{
						redDotPrice = closePrice - (barSize * RedDotMultiplier); // Close - (bar size * multiplier) (stoploss)
						blueDotPrice = closePrice + (barSize * BlueDotMultiplier); // Close + (bar size * multiplier) (takeprofit)
					}
					else
					{
						redDotPrice = closePrice + (barSize * RedDotMultiplier); // Close + (bar size * multiplier) (stoploss)
						blueDotPrice = closePrice - (barSize * BlueDotMultiplier); // Close - (bar size * multiplier) (takeprofit)
					}
					break;
				
				default:
					redDotPrice = closePrice;
					blueDotPrice = closePrice;
					break;
			}
			
			return (redDotPrice, blueDotPrice);
		}
		// ############################################################################# //
		
		#region Properties
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Période 1 - Début", Order=1, GroupName="0.01_Time_Parameters")]
		public DateTime Period1Start { get; set; }
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Période 1 - Fin", Order=2, GroupName="0.01_Time_Parameters")]
		public DateTime Period1End { get; set; }
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Période 2 - Début", Order=3, GroupName="0.01_Time_Parameters")]
		public DateTime Period2Start { get; set; }
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Période 2 - Fin", Order=4, GroupName="0.01_Time_Parameters")]
		public DateTime Period2End { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Close Positions Outside Trading Hours", Description="Automatically close all positions outside trading hours", Order=5, GroupName="0.01_Time_Parameters")]
		public bool ClosePositionsOutsideHours { get; set; }
		
		// ############# 1.Etry_Parameters ####################
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Qty", Order=1, GroupName="0.02_Entry_Parameters")]
		public int Qty
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Sl", Order=2, GroupName="0.02_Entry_Parameters")]
		public int Sl
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Pt", Order=3, GroupName="0.02_Entry_Parameters")]
		public int Pt
		{ get; set; }
		// ======================================
		[NinjaScriptProperty]
        [Display(Name = "Enable Dynamic Stops", Order = 4, GroupName = "0.02_Entry_Parameters")]
        public bool EnableDynamicStops { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Dynamic Stop Loss Multiplier", Order=5, GroupName="0.02_Entry_Parameters")]
		public bool EnableDynamicStopLossMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.5, 10.0)]
		[Display(Name="Dynamic Stop Loss Multiplier", Order=6, GroupName="0.02_Entry_Parameters")]
		public double DynamicStopLossMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Dynamic Target Multiplier", Order=7, GroupName="0.02_Entry_Parameters")]
		public bool EnableDynamicTargetMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.5, 10.0)]
		[Display(Name="Dynamic Target Multiplier", Order=8, GroupName="0.02_Entry_Parameters")]
		public double DynamicTargetMultiplier { get; set; }
		// ===================================
		[NinjaScriptProperty]
        [Display(Name = "Use Bar Size For Stops", Order = 9, GroupName = "0.02_Entry_Parameters")]
        public bool UseBarSizeForStops { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Bar Stop Loss Multiplier", Order=10, GroupName="0.02_Entry_Parameters")]
		public bool EnableBarStopLossMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.5, 10.0)]
		[Display(Name="Bar Stop Loss Multiplier", Order=11, GroupName="0.02_Entry_Parameters")]
		public double BarStopLossMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Bar Target Multiplier", Order=12, GroupName="0.02_Entry_Parameters")]
		public bool EnableBarTargetMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.5, 10.0)]
		[Display(Name="Bar Target Multiplier", Order=13, GroupName="0.02_Entry_Parameters")]
		public double BarTargetMultiplier { get; set; }
		
		
		[NinjaScriptProperty]
        [Display(Name = "Use Parabolic Stop", Order = 16, GroupName = "0.02_Entry_Parameters")]
        public bool UseParabolicStop { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Prevent Multiple Positions", Description="Prevent entering new positions if a position already exists", Order=19, GroupName="0.02_Entry_Parameters")]
		public bool PreventMultiplePositions { get; set; }

        // ###################### Propriétés BVA ###############################
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Reset Period (Minutes)", Order = 1, GroupName = "0.1_BVA Parameters")]
        public int ResetPeriod { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name="Signal Time Mode", Description="Choose between Bars or Minutes for signal timing", Order=2, GroupName="0.1_BVA Parameters")]
        public SignalTimeMode SignalTimingMode { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Min Bars for Signal", Order = 3, GroupName = "0.1_BVA Parameters")]
        public int MinBarsForSignal { get; set; }
        
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Bars for Signal", Description = "Nombre maximum de barres depuis la réinitialisation pour un signal", Order = 4, GroupName = "0.1_BVA Parameters")]
        public int MaxBarsForSignal { get; set; }
		
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Min Minutes for Signal", Order=5, GroupName="0.1_BVA Parameters")]
        public int MinMinutesForSignal { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Max Minutes for Signal", Order=6, GroupName="0.1_BVA Parameters")]
        public int MaxMinutesForSignal { get; set; }
		
		public enum SignalTimeMode
        {
            Bars,
            Minutes
        }
		
		public enum ValueAreaLevel
        {
            STD05,  // Standard Deviation 0.5
            STD1,   // Standard Deviation 1
            STD2,   // Standard Deviation 2
            STD3    // Standard Deviation 3
        }
		
        [NinjaScriptProperty]
        [Display(Name="Value Area Level", Description="Choose which Standard Deviation level to use", Order=7, GroupName="0.1_BVA Parameters")]
        public ValueAreaLevel SelectedValueArea { get; set; }

        [NinjaScriptProperty]
        [Display(Name="use Open in VA Condition UP", Order=8, GroupName="0.1_BVA Parameters")]
        public bool useOpenForVAConditionUP { get; set; }

        [NinjaScriptProperty]
        [Display(Name="use Open in VA Condition Down", Order=9, GroupName="0.1_BVA Parameters")]
        public bool useOpenForVAConditionDown { get; set; }

        [NinjaScriptProperty]
        [Display(Name="use Low in VA Condition UP", Order=10, GroupName="0.1_BVA Parameters")]
        public bool useLowForVAConditionUP { get; set; }

        [NinjaScriptProperty]
        [Display(Name="use High in VA Condition Down", Order=11, GroupName="0.1_BVA Parameters")]
        public bool useHighForVAConditionDown { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Block Signals in Previous Value Area", Description="Block signals when price is inside previous session's Value Area", Order=12, GroupName="0.1_BVA Parameters")]
		public bool BlockSignalsInPreviousValueArea { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="Value Area Offset Ticks", Description="Offset en ticks pour la Value Area précédente", Order=13, GroupName="0.1_BVA Parameters")]
		public int ValueAreaOffsetTicks { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Use Previous Bar Open in VA", Description="Check if previous bar's Open is within Value Area", Order=14, GroupName="0.1_BVA Parameters")]
		public bool UsePrevBarInVA { get; set; }
        
        // ################# Propriétés Limusine ##########################
		[NinjaScriptProperty]
		[Display(Name = "Active Buy", Description = "Activer les signaux d'achat (flèches UP)", Order = 1, GroupName = "0.2_Limusine Parameters")]
		public bool ActiveBuy { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Active Sell", Description = "Activer les signaux de vente (flèches DOWN)", Order = 2, GroupName = "0.2_Limusine Parameters")]
		public bool ActiveSell { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Minimum Ticks", Description = "Nombre minimum de ticks pour une limusine", Order = 3, GroupName = "0.2_Limusine Parameters")]
        public int MinimumTicks { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Maximum Ticks", Description = "Nombre maximum de ticks pour une limusine", Order = 4, GroupName = "0.2_Limusine Parameters")]
        public int MaximumTicks { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Afficher Limusine Open-Close UP", Description = "Afficher les limusines Open-Close UP", Order = 5, GroupName = "0.2_Limusine Parameters")]
        public bool ShowLimusineOpenCloseUP { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Afficher Limusine Open-Close DOWN", Description = "Afficher les limusines Open-Close DOWN", Order = 6, GroupName = "0.2_Limusine Parameters")]
        public bool ShowLimusineOpenCloseDOWN { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Afficher Limusine High-Low UP", Description = "Afficher les limusines High-Low UP", Order = 7, GroupName = "0.2_Limusine Parameters")]
        public bool ShowLimusineHighLowUP { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Afficher Limusine High-Low DOWN", Description = "Afficher les limusines High-Low DOWN", Order = 8, GroupName = "0.2_Limusine Parameters")]
        public bool ShowLimusineHighLowDOWN { get; set; }
		
		// ############ Buy & Sell #############
		public enum EntryLevelChoice
		{
			STD05,
			STD1,
			STD2,
			STD3
		}
        // ############# Buy ############## //
		[NinjaScriptProperty]
		[Display(Name="Entry Level Choice UP", Description="Choose which Standard Deviation level to use for UP entries", Order=0, GroupName="0.3_Buy")]
		public EntryLevelChoice SelectedEntryLevelUp { get; set; }
	
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Min Entry Distance UP", Order = 1, GroupName = "0.3_Buy")]
        public int MinEntryDistanceUP { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Entry Distance UP", Order = 2, GroupName = "0.3_Buy")]
        public int MaxEntryDistanceUP { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Upper Breakouts", Order = 3, GroupName = "0.3_Buy")]
        public int MaxUpperBreakouts { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "OKisAfterBarsSinceResetUP", Description = "Check Bars Since Reset UP", Order = 4, GroupName = "0.3_Buy")]
        public bool OKisAfterBarsSinceResetUP { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "OKisAboveUpperThreshold", Description = "Check Above Upper Threshold", Order = 5, GroupName = "0.3_Buy")]
        public bool OKisAboveUpperThreshold { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "OKisWithinMaxEntryDistance", Description = "Check Within Max Entry Distance", Order = 6, GroupName = "0.3_Buy")]
        public bool OKisWithinMaxEntryDistance { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "OKisUpperBreakoutCountExceeded", Description = "Check Upper Breakout Count Exceeded", Order = 7, GroupName = "0.3_Buy")]
        public bool OKisUpperBreakoutCountExceeded { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Up Breakout Check", Description="Active la condition de cassure du plus haut des dernières barres pour un signal UP", Order=8, GroupName="0.3_Buy")]
		public bool EnableUpBreakoutCheck { get; set; }
		
		[NinjaScriptProperty]
		[Range(1,5)]
		[Display(Name="Up Breakout Bars", Description="Nombre de barres à considérer pour le plus haut (2 à 5)", Order=9, GroupName="0.3_Buy")]
		public int UpBreakoutBars { get; set; }
		
		[NinjaScriptProperty]
		[Range(0,int.MaxValue)]
		[Display(Name="Up Breakout Offset Ticks", Description="Offset en ticks au-dessus du plus haut pour confirmer la cassure", Order=10, GroupName="0.3_Buy")]
		public int UpBreakoutOffsetTicks { get; set; }
        
        // ############ Sell #############
        // Sell
		[NinjaScriptProperty]
		[Display(Name="Entry Level Choice DOWN", Description="Choose which Standard Deviation level to use for DOWN entries", Order=0, GroupName="0.4_Sell")]
		public EntryLevelChoice SelectedEntryLevelDown { get; set; }
	
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Min Entry Distance DOWN", Order = 1, GroupName = "0.4_Sell")]
        public int MinEntryDistanceDOWN { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Entry Distance DOWN", Order = 2, GroupName = "0.4_Sell")]
        public int MaxEntryDistanceDOWN { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Lower Breakouts", Order = 3, GroupName = "0.4_Sell")]
        public int MaxLowerBreakouts { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "OKisAfterBarsSinceResetDown", Description = "Check Bars Since Reset Down", Order = 4, GroupName = "0.4_Sell")]
        public bool OKisAfterBarsSinceResetDown { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "OKisBelovLowerThreshold", Description = "Check Below Lower Threshold", Order = 5, GroupName = "0.4_Sell")]
        public bool OKisBelovLowerThreshold { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "OKisWithinMaxEntryDistanceDown", Description = "Check Within Max Entry Distance Down", Order = 6, GroupName = "0.4_Sell")]
        public bool OKisWithinMaxEntryDistanceDown { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "OKisLowerBreakoutCountExceeded", Description = "Check Lower Breakout Count Exceeded", Order = 7, GroupName = "0.4_Sell")]
        public bool OKisLowerBreakoutCountExceeded { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Down Breakout Check", Description="Active la condition de cassure du plus bas des dernières barres pour un signal DOWN", Order=8, GroupName="0.4_Sell")]
		public bool EnableDownBreakoutCheck { get; set; }
		
		[NinjaScriptProperty]
		[Range(1,5)]
		[Display(Name="Down Breakout Bars", Description="Nombre de barres à considérer pour le plus bas (2 à 5)", Order=9, GroupName="0.4_Sell")]
		public int DownBreakoutBars { get; set; }
		
		[NinjaScriptProperty]
		[Range(0,int.MaxValue)]
		[Display(Name="Down Breakout Offset Ticks", Description="Offset en ticks en-dessous du plus bas pour confirmer la cassure", Order=10, GroupName="0.4_Sell")]
		public int DownBreakoutOffsetTicks { get; set; }
        
        // ################ Distance VWAP ####################### // 
        [NinjaScriptProperty]
        [Display(Name = "Enable Distance From VWAP Condition", Order = 1, GroupName = "1.01_Distance_VWAP")]
        public bool EnableDistanceFromVWAPCondition { get; set; }
        
        [Range(1, int.MaxValue)]
        [NinjaScriptProperty]
        [Display(Name = "Minimum Distance From VWAP (Ticks)", Order = 2, GroupName = "1.01_Distance_VWAP")]
        public int MinDistanceFromVWAP { get; set; }
        
        [Range(1, int.MaxValue)]
        [NinjaScriptProperty]
        [Display(Name = "Maximum Distance From VWAP (Ticks)", Order = 3, GroupName = "1.01_Distance_VWAP")]
        public int MaxDistanceFromVWAP { get; set; }
		
		// #################### 1.02_STD1_Range ################### //
		[NinjaScriptProperty]
		[Display(Name = "Enable STD1 Range Check", Description = "Enable checking for minimum/maximum range between STD1 Upper and Lower", Order = 1, GroupName = "1.02_STD1_Range")]
		public bool EnableSTD1RangeCheck { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Min STD1 Range (Ticks)", Description = "Minimum range between STD1 Upper and Lower in ticks", Order = 2, GroupName = "1.02_STD1_Range")]
		public int MinSTD1Range { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Max STD1 Range (Ticks)", Description = "Maximum range between STD1 Upper and Lower in ticks", Order = 3, GroupName = "1.02_STD1_Range")]
		public int MaxSTD1Range { get; set; }
		// ################ 1.03_STD3 Tracking ############# //
        [NinjaScriptProperty]
        [Display(Name="Enable STD3 High/Low Tracking", Description="Track highest STD3 Upper and lowest STD3 Lower since last reset", Order=1000, GroupName="1.03_STD3 Tracking")]
        public bool EnableSTD3HighLowTracking { get; set; }
		// ################## 1.04_Enable Previous Session RangeBreakout ########################
		[NinjaScriptProperty]
        [Display(Name = "Enable Previous Session Range Breakout", Description = "Enable checking for breakouts of the previous session's StdDev1 range", Order = 1, GroupName = "1.04_Enable Previous Session RangeBreakout")]
        public bool EnablePreviousSessionRangeBreakout { get; set; }
		
		//
		// Ajoutez ces propriétés dans la région Properties
		[NinjaScriptProperty]
		[Display(Name="Enable Initial Balance Logic", Description="Enable the Initial Balance logic", Order=1, GroupName="1.05_Initial Balance")]
		public bool EnableIBLogic { get; set; }
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="IB Start Time", Description="Start time of the Initial Balance period", Order=2, GroupName="1.05_Initial Balance")]
		public DateTime IBStartTime { get; set; }
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="IB End Time", Description="End time of the Initial Balance period", Order=3, GroupName="1.05_Initial Balance")]
		public DateTime IBEndTime { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="IB Offset Ticks", Description="Number of ticks to offset the IB levels", Order=4, GroupName="1.05_Initial Balance")]
		public int IBOffsetTicks { get; set; }
		
		// 
		// Ajoutez cette énumération après les autres énumérations existantes
		public enum DotPlotMode
		{
			STD1,
			STD2,
			BarSize,
			UseSTD1Multiple 
		}
		
		// Ajoutez ces propriétés dans la région Properties, dans un nouveau groupe "Dot Plot Settings"
		[NinjaScriptProperty]
		[Display(Name="Dot Plot Mode", Description="Choose the mode for plotting dots", Order=1, GroupName="Dot Plot Settings")]
		public DotPlotMode SelectedDotPlotMode { get; set; }
		
		public enum BarSizeType
		{
			CloseOpen,
			HighLow
		}
		
		// Ajoutez ces propriétés dans la région Properties
		[NinjaScriptProperty]
		[Display(Name="Bar Size Type", Description="Choose between Close-Open or High-Low for bar size calculation", Order=2, GroupName="Dot Plot Settings")]
		public BarSizeType SelectedBarSizeType { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.1, 10.0)]
		[Display(Name="Red Dot Multiplier", Description="Multiplier for the red dot distance", Order=3, GroupName="Dot Plot Settings")]
		public double RedDotMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.1, 10.0)]
		[Display(Name="Blue Dot Multiplier", Description="Multiplier for the blue dot distance", Order=4, GroupName="Dot Plot Settings")]
		public double BlueDotMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.1, 10.0)]
		[Display(Name="STD1 Red Dot Multiplier", Description="Multiplier for the red dot distance when using STD1 Multiple mode", Order=5, GroupName="Dot Plot Settings")]
		public double STD1RedDotMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.1, 10.0)]
		[Display(Name="STD1 Blue Dot Multiplier", Description="Multiplier for the blue dot distance when using STD1 Multiple mode", Order=6, GroupName="Dot Plot Settings")]
		public double STD1BlueDotMultiplier { get; set; }
        
        // ############ Volume #############
        // Volume
        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Fperiod Vol", Order = 1, GroupName = "Volume")]
        public int FperiodVol { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "OKisVOL", Description = "Check Volume", Order = 1, GroupName = "Volume")]
        public bool OKisVOL { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> VWAP => Values[0];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> StdDev0_5Upper => Values[1];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> StdDev0_5Lower => Values[2];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> StdDev1Upper => Values[3];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> StdDev1Lower => Values[4];
        #endregion
    }
}
