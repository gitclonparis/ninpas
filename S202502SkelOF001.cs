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
    public class S202502SkelOF001 : Strategy
    {
		// Breaking Even Module Constants
        private const string LongPos = "Open Long";
        private const string ShortPos = "Open Short";
        private const string ProfitLong1 = "Profit Long 1";
        private const string ProfitLong2 = "Profit Long 2";
        private const string StopLong = "Stop Long";
        private const string ProfitShort1 = "Profit Short 1";
        private const string StopShort = "Stop Short";
        private const string ProfitShort2 = "Profit Short 2";
        private const string BreakEvenShort = "BE Short";
        private const string BreakEvenLong = "BE Long";

        // Breaking Even Module Variables
        private int _posSize;
        private CurrentPos _currentPosition;
        private double _stop;
        private double _profit1;
        private double _profit2;
        private Order _stopOrder;
        private Order _profitOrder1;
        private Order _profitOrder2;
        private double _pos1;
        private double _pos2;
		// ################## //
        private double sumPriceVolume;
        private double sumVolume;
        private double sumSquaredPriceVolume;
        private DateTime lastResetTime;
        private int barsSinceReset;
        private int upperBreakoutCount;
        private int lowerBreakoutCount;
        
        private VOL VOL1;
        private VOLMA VOLMA1;
		private double volumeMaxS;					
		
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
		
		private double _entryBarOpen;
		private double _entryBarClose;
		
		public enum CurrentPos
        {
            Short,
            Long,
            None
        }
		
		public enum DynamicAreaLevel
		{
			STD05,  // Standard Deviation 0.5
			STD1,   // Standard Deviation 1
			STD2,   // Standard Deviation 2
			STD3    // Standard Deviation 3
		}
		
		// Ajoutez ces variables privées
		private double previousSessionDynamicUpperLevel = double.MinValue;
		private double previousSessionDynamicLowerLevel = double.MaxValue;
		private bool dynamicAreaPointsDrawn;
		private int dynamicAreaDrawDelayMinutes;
		private OrderFlowCumulativeDelta[] deltaIndicators;
		private OrderFlowCumulativeDelta cumulativeDelta;
		
		// Nouvelles variables privées
		private double _vaRef;
		private int _currentPosSize;
		private double _currentPosSplitPercent;
		private bool _currentBreakEvenIsOn;
		private double _currentBreakEvenOffset;
		
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Indicateur BVA-Limusine combiné";
                Name = "S202502SkelOF001";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsInstantiatedOnEachOptimizationIteration	= false;
				
				// Breaking Even Module Defaults
                PosSize = 10;
                PositionSplitPercent = 50;
                StopTicks = 20;
                ProfitOneTicks = 20;
                ProfitTwoTicks = 30;
				
				Period1Start = DateTime.Parse("15:30", System.Globalization.CultureInfo.InvariantCulture);
				Period1End = DateTime.Parse("17:30", System.Globalization.CultureInfo.InvariantCulture);
				Period2Start = DateTime.Parse("18:30", System.Globalization.CultureInfo.InvariantCulture);
				Period2End = DateTime.Parse("20:30", System.Globalization.CultureInfo.InvariantCulture);
				ClosePositionsOutsideHours = false;
				
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
				UseVolumeS = false;
				EnableVolumeAnalysisPeriod = false;	   
				UseVolumeIncrease = false;
				VolumeBarsToCompare = 1;

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
				
				SelectedDynamicArea = DynamicAreaLevel.STD1;
				BlockSignalsInPreviousDynamicArea = false;
				DynamicAreaOffsetTicks = 0;
				dynamicAreaPointsDrawn = false;
				DynamicAreaDrawDelayMinutes = 59;
				
				// VA Stop Target Module defaults
				StopTargetReference = StopTargetReferenceType.ValueArea;
				UseCustomStopTarget = false;
				PosSizeVA = 10;
				PositionSplitPercentVA = 50;
				StopMultiplierVA = 1.0;
				ProfitOneMultiplierVA = 1.0;
				ProfitTwoMultiplierVA = 1.5;
				
				UseTrailingStopVA = false;
				TrailingStopOffsetTicks = 2;
				TrailingStopMinBars = 0;
				
				SelectedBreakEvenMode = BreakEvenMode.Disabled;
				BreakEvenOffsetTicks = 5;
				BreakEvenOffsetMultiplierVA = 0.5;
				// Prior Day OHLC
				EnablePriorHiLowUpSignal = false;
				EnablePriorHiLowDownSignal = false;
				TicksOffsetHigh = 5;
				TicksOffsetLow = 5;
				BlockSignalHiLowPriorRange = false;
				// Prior VA Vwap
				UpperOffsetTicks = 5;
				LowerOffsetTicks = 5;
				UsePriorSvaUP = false;
				UsePriorSvaDown = false;
				BlockInPriorSVA = false;
				//POC barre
				pocConditionEnabled = false;
				pocTicksDistance = 2;
				
				// Initialiser le tableau upParameters
				upParameters = new VolumetricParameters[7]; // ou la taille dont vous avez besoin
				for (int i = 0; i < upParameters.Length; i++)
				{
					upParameters[i] = new VolumetricParameters();
				}
		
				// Définir les valeurs par défaut pour Delta Percent UP
				upParameters[1].Enabled = false;
				upParameters[1].Min = 10;
				upParameters[1].Max = 50;
				
				// Initialiser downParameters
				downParameters = new VolumetricParameters[7]; // ou la taille dont vous avez besoin
				for (int i = 0; i < downParameters.Length; i++)
				{
					downParameters[i] = new VolumetricParameters();
				}
		
				// Définir les valeurs par défaut pour Delta Percent DOWN
				downParameters[1].Enabled = false;
				downParameters[1].Min = 10;
				downParameters[1].Max = 50;
            }
            else if (State == State.Configure)
            {
                ResetValues(DateTime.MinValue);
				_posSize = PosSize;
				newSession = true;
            }
            else if (State == State.DataLoaded)
            {
                VOL1 = VOL(Close);
                VOLMA1 = VOLMA(Close, Convert.ToInt32(FperiodVol));
				sessionIterator = new SessionIterator(Bars);
				priorDayOHLC = PriorDayOHLC();
				vwap = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3);
				pocSeries = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBars[0] < 20)
                return;
			if (BarsInProgress != 0) return;
			
			if (CurrentBar < 20 || !(Bars.BarsSeries.BarsType is NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType))
				return;
			
			UpdatePriorSessionBands();
			if (!newSession) return;

			bool isInTradingPeriod = IsInTradingPeriod(Time[0]);
            if (ClosePositionsOutsideHours && !isInTradingPeriod)
            {
                if (Position.MarketPosition == MarketPosition.Long)
                    ExitLong();
                else if (Position.MarketPosition == MarketPosition.Short)
                    ExitShort();
                return;
            }
			
			// Calcul du POC
			var currentBarVolumes = barsType.Volumes[CurrentBar];
			double pocPrice;
			long maxVolume = currentBarVolumes.GetMaximumVolume(null, out pocPrice);
			pocSeries[0] = pocPrice;
			Values[0][0] = pocPrice;
			
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
			//
			// if (timeSinceReset.TotalMinutes >= figVA && !dynamicAreaPointsDrawn)
			if (timeSinceReset.TotalMinutes >= DynamicAreaDrawDelayMinutes && !dynamicAreaPointsDrawn)
			{
				double dynamicUpperLevel = 0;
				double dynamicLowerLevel = 0;
				
				switch (SelectedDynamicArea)
				{
					case DynamicAreaLevel.STD05:
						dynamicUpperLevel = Values[1][0];
						dynamicLowerLevel = Values[2][0];
						break;
					case DynamicAreaLevel.STD1:
						dynamicUpperLevel = Values[3][0];
						dynamicLowerLevel = Values[4][0];
						break;
					case DynamicAreaLevel.STD2:
						dynamicUpperLevel = Values[5][0];
						dynamicLowerLevel = Values[6][0];
						break;
					case DynamicAreaLevel.STD3:
						dynamicUpperLevel = Values[7][0];
						dynamicLowerLevel = Values[8][0];
						break;
				}
			
				dynamicUpperLevel += DynamicAreaOffsetTicks * TickSize;
				dynamicLowerLevel -= DynamicAreaOffsetTicks * TickSize;
				
				Draw.Dot(this, "DynamicAreaUpper" + CurrentBar, true, 0, dynamicUpperLevel, Brushes.Orange);
				Draw.Dot(this, "DynamicAreaLower" + CurrentBar, true, 0, dynamicLowerLevel, Brushes.Orange);
				previousSessionDynamicUpperLevel = dynamicUpperLevel;
				previousSessionDynamicLowerLevel = dynamicLowerLevel;
				dynamicAreaPointsDrawn = true;
			}
			
			// ####################################### //
			bool isWithinVolumeAnalysisPeriod;
			// TimeSpan timeSinceReset = Time[0] - lastResetTime;
			
			if (SignalTimingMode == SignalTimeMode.Minutes)
			{
				isWithinVolumeAnalysisPeriod = timeSinceReset.TotalMinutes >= MinMinutesForSignal + TrailingStopMinBars && 
										timeSinceReset.TotalMinutes <= MaxMinutesForSignal;
			}
			else
			{
				isWithinVolumeAnalysisPeriod = barsSinceReset >= MinBarsForSignal + TrailingStopMinBars && 
										barsSinceReset <= MaxBarsForSignal;
			}
			
			if (!EnableVolumeAnalysisPeriod || isWithinVolumeAnalysisPeriod)
			{
				if (Volume[0] > volumeMaxS)
				{
					volumeMaxS = Volume[0];
				}
			}
			
			// Appliquer le trailing stop uniquement pendant la période d'analyse
			if (UseTrailingStopVA && isWithinVolumeAnalysisPeriod && Position.MarketPosition != MarketPosition.Flat)
			{
				double trailingLevel;
				
				switch (SelectedTrailingStopType)
				{
					case TrailingStopType.STD05:
						trailingLevel = Position.MarketPosition == MarketPosition.Long ? Values[1][0] : Values[2][0];
						break;
					case TrailingStopType.STD1:
						trailingLevel = Position.MarketPosition == MarketPosition.Long ? Values[3][0] : Values[4][0];
						break;
					case TrailingStopType.VWAP:
						trailingLevel = Values[0][0]; // VWAP
						break;
					default:
						trailingLevel = Position.MarketPosition == MarketPosition.Long ? Values[3][0] : Values[4][0];
						break;
				}
				
				if (Position.MarketPosition == MarketPosition.Long)
				{
					double exitPrice = trailingLevel - (TrailingStopOffsetTicks * TickSize);
					if (Close[0] < exitPrice)
					{
						ExitLong();
						Draw.Dot(this, "TrailingExit" + CurrentBar, true, 0, High[0] + 2 * TickSize, Brushes.Yellow);
					}
				}
				else if (Position.MarketPosition == MarketPosition.Short)
				{
					double exitPrice = trailingLevel + (TrailingStopOffsetTicks * TickSize);
					if (Close[0] > exitPrice)
					{
						ExitShort();
						Draw.Dot(this, "TrailingExit" + CurrentBar, true, 0, Low[0] - 2 * TickSize, Brushes.Yellow);
					}
				}
			}
			
			// ####################################### //
            // Réinitialiser figVAPointsDrawn lors d'un reset
            if (shouldReset)
            {
                figVAPointsDrawn = false;
            }
			//
			if (isInTradingPeriod && Position.MarketPosition == MarketPosition.Flat)
			{
				_vaRef = CalculateVaRef();
				_currentPosSize = UseCustomStopTarget ? PosSizeVA : PosSize;
				_currentPosSplitPercent = UseCustomStopTarget ? PositionSplitPercentVA : PositionSplitPercent;
				
				// Nouvelle logique pour gérer le Break Even Mode
				if (UseCustomStopTarget)
				{
					_currentBreakEvenIsOn = (SelectedBreakEvenMode == BreakEvenMode.ValueArea || SelectedBreakEvenMode == BreakEvenMode.OpenClose);
				}
				else
				{
					_currentBreakEvenIsOn = SelectedBreakEvenMode == BreakEvenMode.Fixed;
				}
				
				if (ActiveBuy && ShouldDrawUpArrow())
				{
					EnterLong(_currentPosSize, LongPos);
					Draw.ArrowUp(this, "UpArrow" + CurrentBar, true, 0, Low[0] - 2 * TickSize, Brushes.Green);
					upperBreakoutCount++;
				}
				else if (ActiveSell && ShouldDrawDownArrow())
				{
					EnterShort(_currentPosSize, ShortPos);
					Draw.ArrowDown(this, "DownArrow" + CurrentBar, true, 0, High[0] + 2 * TickSize, Brushes.Red);
					lowerBreakoutCount++;
				}
			}
        }
		// ############################################################################################################### //
		// Nouvelle méthode pour calculer VaRef
		private double CalculateVaRef()
		{
			if (StopTargetReference == StopTargetReferenceType.ValueArea)
			{
				return Math.Abs(Values[3][0] - Values[4][0]); // STD1Upper - STD1Lower
			}
			else // StopTargetReferenceType.OpenClose
			{
				// Pour les positions longues (UP)
				if (Close[0] > Open[0])
				{
					return Math.Abs(Close[0] - Open[0]);
				}
				// Pour les positions courtes (DOWN)
				else
				{
					return Math.Abs(Open[0] - Close[0]);
				}
			}
		}
		// ############################################################################################################### //
		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity,
            MarketPosition marketPosition, string orderId, DateTime time)
        {
			if (IsEntryOrderFilled(execution))
			{
				IdentifyDirection(execution);
				SetStopProfit(execution);
			}
			if (IsProfitTargetOneFilled(execution) && SelectedBreakEvenMode != BreakEvenMode.Disabled)
			{
				BreakEven(execution);
			}
		}

        #region Breaking Even Module Methods
		//
		private void BreakEven(Execution execution)
		{
			if (SelectedBreakEvenMode == BreakEvenMode.Disabled)
				return;
		
			double breakEvenOffset;
			
			switch (SelectedBreakEvenMode)
			{
				case BreakEvenMode.ValueArea:
					breakEvenOffset = Math.Abs(Values[3][0] - Values[4][0]) * BreakEvenOffsetMultiplierVA;
					break;
					
				case BreakEvenMode.OpenClose:
					if (Position.MarketPosition == MarketPosition.Long)
					{
						breakEvenOffset = Math.Abs(_entryBarClose - _entryBarOpen) * BreakEvenOffsetMultiplierVA;
					}
					else
					{
						breakEvenOffset = Math.Abs(_entryBarOpen - _entryBarClose) * BreakEvenOffsetMultiplierVA;
					}
					break;
					
				case BreakEvenMode.Fixed:
					breakEvenOffset = BreakEvenOffsetTicks * TickSize;
					break;
					
				default:
					return;
			}
		
			if (execution.Order.Name == ProfitLong1)
			{
				double breakEvenPrice = Position.AveragePrice + breakEvenOffset;
				breakEvenPrice = RoundToTickSize(breakEvenPrice);
				_stopOrder = ExitLongStopMarket(0, true, (int)_pos2, breakEvenPrice, BreakEvenLong, LongPos);
			}
		
			if (execution.Order.Name == ProfitShort1)
			{
				double breakEvenPrice = Position.AveragePrice - breakEvenOffset;
				breakEvenPrice = RoundToTickSize(breakEvenPrice);
				_stopOrder = ExitShortStopMarket(0, true, (int)_pos2, breakEvenPrice, BreakEvenShort, ShortPos);
			}
		}
		
		//
		
        private bool IsProfitTargetOneFilled(Execution execution)
        {
            return execution.Order.OrderState == OrderState.Filled && IsProTargetOneOrder(execution.Order.Name);
        }

        private bool IsProTargetOneOrder(string orderName)
        {
            return orderName == ProfitLong1 || orderName == ProfitShort1;
        }

        private bool IsEntryOrderFilled(Execution exec)
        {
            return exec.Order.OrderState == OrderState.Filled && IsEntryOrder(exec.Order.Name);
        }

        private bool IsEntryOrder(string name)
        {
            return name == LongPos || name == ShortPos;
        }

        private void IdentifyDirection(Execution execution)
        {
			if (execution.Order.Name == LongPos)
			{
				_currentPosition = CurrentPos.Long;
				_entryBarOpen = Open[0];
				_entryBarClose = Close[0];
			}
		
			if (execution.Order.Name == ShortPos)
			{
				_currentPosition = CurrentPos.Short;
				_entryBarOpen = Open[0];
				_entryBarClose = Close[0];
			}
		}

		// Modification de SetStopProfit pour inclure la logique VA
		private void SetStopProfit(Execution execution)
		{
			var entryPrice = execution.Order.AverageFillPrice;
			_pos1 = Math.Round((_currentPosSize * _currentPosSplitPercent / 100), 0);
			_pos2 = _currentPosSize - _pos1;
		
			if (UseCustomStopTarget)
			{
				if (_currentPosition == CurrentPos.Long)
				{
					_stop = entryPrice - (_vaRef * StopMultiplierVA);
					_profit1 = entryPrice + (_vaRef * ProfitOneMultiplierVA);
					_profit2 = entryPrice + (_vaRef * ProfitTwoMultiplierVA);
				}
				else if (_currentPosition == CurrentPos.Short)
				{
					_stop = entryPrice + (_vaRef * StopMultiplierVA);
					_profit1 = entryPrice - (_vaRef * ProfitOneMultiplierVA);
					_profit2 = entryPrice - (_vaRef * ProfitTwoMultiplierVA);
				}
			}
			else
			{
				// Original logic
				if (_currentPosition == CurrentPos.Long)
				{
					_stop = entryPrice - StopTicks * TickSize;
					_profit1 = entryPrice + ProfitOneTicks * TickSize;
					_profit2 = entryPrice + ProfitTwoTicks * TickSize;
				}
				else if (_currentPosition == CurrentPos.Short)
				{
					_stop = entryPrice + StopTicks * TickSize;
					_profit1 = entryPrice - ProfitOneTicks * TickSize;
					_profit2 = entryPrice - ProfitTwoTicks * TickSize;
				}
			}
		
			if (_currentPosition == CurrentPos.Long)
			{
				_profitOrder1 = ExitLongLimit(0, true, (int)_pos1, _profit1, ProfitLong1, LongPos);
				_profitOrder2 = ExitLongLimit(0, true, (int)_pos2, _profit2, ProfitLong2, LongPos);
				_stopOrder = ExitLongStopMarket(0, true, _currentPosSize, _stop, StopLong, LongPos);
			}
			else if (_currentPosition == CurrentPos.Short)
			{
				_profitOrder1 = ExitShortLimit(0, true, (int)_pos1, _profit1, ProfitShort1, ShortPos);
				_profitOrder2 = ExitShortLimit(0, true, (int)_pos2, _profit2, ProfitShort2, ShortPos);
				_stopOrder = ExitShortStopMarket(0, true, _currentPosSize, _stop, StopShort, ShortPos);
			}
		}
		
		private double RoundToTickSize(double val)
        {
            return Instrument.MasterInstrument.RoundToTickSize(val);
        }
		
		#endregion
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

			double upperLevelWithOffset = previousSessionVAUpperLevel + (ValueAreaOffsetTicks * TickSize);
			double lowerLevelWithOffset = previousSessionVALowerLevel - (ValueAreaOffsetTicks * TickSize);
		
			return Close[0] <= upperLevelWithOffset && Close[0] >= lowerLevelWithOffset;
		}
		
		
		private bool IsPriceInPreviousDynamicArea()
		{
			if (!BlockSignalsInPreviousDynamicArea || previousSessionDynamicUpperLevel == double.MinValue || previousSessionDynamicLowerLevel == double.MaxValue)
				return false;
		
			double upperLevelWithOffset = previousSessionDynamicUpperLevel + (DynamicAreaOffsetTicks * TickSize);
			double lowerLevelWithOffset = previousSessionDynamicLowerLevel - (DynamicAreaOffsetTicks * TickSize);
		
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
		//
		private bool CheckVolumeIncrease()
		{
			if (!UseVolumeIncrease || CurrentBar < VolumeBarsToCompare)
				return true;
		
			double currentVolume = Volume[0];
			
			// Vérifier que le volume actuel est supérieur à tous les volumes précédents
			for (int i = 1; i <= VolumeBarsToCompare; i++)
			{
				if (currentVolume <= Volume[i])
					return false;
			}
			
			return true;
		}
		// ########################################### Prior Day OHLC #################################################################### //
		private bool CheckPriorHiLowUpSignal()
		{
			if (!EnablePriorHiLowUpSignal)
				return true;
			
			double priorHigh = priorDayOHLC.PriorHigh[0];
			double highOffset = TicksOffsetHigh * TickSize;
			return Close[0] > priorHigh + highOffset || 
				(Close[0] <= priorHigh + highOffset && 
					Close[0] >= priorDayOHLC.PriorLow[0] - TicksOffsetLow * TickSize);
		}
		
		private bool CheckPriorHiLowDownSignal()
		{
			if (!EnablePriorHiLowDownSignal)
				return true;
			
			double priorLow = priorDayOHLC.PriorLow[0];
			double lowOffset = TicksOffsetLow * TickSize;
			return Close[0] < priorLow - lowOffset || 
				(Close[0] >= priorLow - lowOffset && 
					Close[0] <= priorDayOHLC.PriorHigh[0] + TicksOffsetHigh * TickSize);
		}
		
		private bool IsPriceInPriorRange()
		{
			if (!BlockSignalHiLowPriorRange)
				return false;
				
			double priorHigh = priorDayOHLC.PriorHigh[0];
			double priorLow = priorDayOHLC.PriorLow[0];
			double highOffset = TicksOffsetHigh * TickSize;
			double lowOffset = TicksOffsetLow * TickSize;
			
			double upperLimit = priorHigh + highOffset;
			double lowerLimit = priorLow - lowOffset;
			
			return Close[0] >= lowerLimit && Close[0] <= upperLimit;
		}
		
		// ############################################## Prior Day OHLC ################################################################# //
		// ############################################## Prior VA Vwap ################################################################# //
		private void UpdatePriorSessionBands()
		{
			if (Bars.IsFirstBarOfSession)
			{
				newSession = true;
				priorSessionUpperBand = vwap.StdDev1Upper[1] + (TickSize * UpperOffsetTicks);
				priorSessionLowerBand = vwap.StdDev1Lower[1] - (TickSize * LowerOffsetTicks);
			}
		}
	
		private bool IsPriceWithinValueArea(double price)
		{
			return price >= priorSessionLowerBand && price <= priorSessionUpperBand;
		}
		
		private bool ShouldAllowSignals(double price, bool isUpSignal)
		{
			bool isWithinVA = IsPriceWithinValueArea(price);
			
			// Si BlockInPriorSVA est activé et que le prix est dans la Value Area, bloquer tous les signaux
			if (BlockInPriorSVA && isWithinVA)
				return false;
				
			// Si le prix est dans la Value Area et qu'aucune option spéciale n'est activée
			if (isWithinVA && !UsePriorSvaUP && !UsePriorSvaDown)
				return true;
				
			// Pour les signaux UP
			if (isUpSignal)
			{
				// Si UsePriorSvaUP est activé
				if (UsePriorSvaUP)
				{
					// Autoriser dans la Value Area ou au-dessus
					return isWithinVA || price > priorSessionUpperBand;
				}
				// Si UsePriorSvaDown est activé
				else if (UsePriorSvaDown)
				{
					// Autoriser seulement dans la Value Area
					return isWithinVA;
				}
			}
			// Pour les signaux DOWN
			else
			{
				// Si UsePriorSvaDown est activé
				if (UsePriorSvaDown)
				{
					// Autoriser dans la Value Area ou en-dessous
					return isWithinVA || price < priorSessionLowerBand;
				}
				// Si UsePriorSvaUP est activé
				else if (UsePriorSvaUP)
				{
					// Autoriser seulement dans la Value Area
					return isWithinVA;
				}
			}
			return true;
		}
		// ############################################## Prior VA Vwap ################################################################# //
		
        private bool ShouldDrawUpArrow()
        {
			if (!ShouldAllowSignals(Close[0], true))
				return false;
			
			if (IsPriceInPriorRange())
				return false;
			
			// Vérifier si le prix est dans la Value Area précédente
			if (BlockSignalsInPreviousValueArea && IsPriceInPreviousValueArea())
				return false;
			
		// Vérifier si le prix est dans la Dynamic Area précédente
			if (BlockSignalsInPreviousDynamicArea && IsPriceInPreviousDynamicArea())
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
            
			// Définir les seuils basés sur le DynamicAreaLevel sélectionné
			double dynamicUpperThreshold, dynamicLowerThreshold;
			switch (SelectedDynamicArea)
			{
				case DynamicAreaLevel.STD05:
					dynamicUpperThreshold = Values[1][0];
					dynamicLowerThreshold = Values[2][0];
					break;
				case DynamicAreaLevel.STD1:
					dynamicUpperThreshold = Values[3][0];
					dynamicLowerThreshold = Values[4][0];
					break;
				case DynamicAreaLevel.STD2:
					dynamicUpperThreshold = Values[5][0];
					dynamicLowerThreshold = Values[6][0];
					break;
				case DynamicAreaLevel.STD3:
					dynamicUpperThreshold = Values[7][0];
					dynamicLowerThreshold = Values[8][0];
					break;
				default:
					dynamicUpperThreshold = Values[3][0];
					dynamicLowerThreshold = Values[4][0];
					break;
			}
			
			// Ajuster les seuils avec l'offset
			dynamicUpperThreshold += DynamicAreaOffsetTicks * TickSize;
			dynamicLowerThreshold -= DynamicAreaOffsetTicks * TickSize;														  
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
				(!UseVolumeS || Volume[0] >= volumeMaxS) &&				
				(!UseVolumeIncrease || CheckVolumeIncrease()) &&
                (!OKisAfterBarsSinceResetUP || withinSignalTime) &&
				(!OKisAboveUpperThreshold || Close[0] > (selectedUpperLevel + MinEntryDistanceUP * TickSize)) &&
				(!OKisWithinMaxEntryDistance || Close[0] <= (selectedUpperLevel + MaxEntryDistanceUP * TickSize)) &&
                (!OKisUpperBreakoutCountExceeded || upperBreakoutCount < MaxUpperBreakouts) &&
                (!useOpenForVAConditionUP || (Open[0] > lowerThreshold && Open[0] < upperThreshold)) &&
                (!useLowForVAConditionUP || (Low[0] > lowerThreshold && Low[0] < upperThreshold)) &&
				(!UsePrevBarInVA || (Open[1] > lowerThreshold && Open[1] < upperThreshold)) &&
				//(!useOpenForVAConditionUP || (Open[0] > dynamicLowerThreshold && Open[0] < dynamicUpperThreshold)) &&
				//(!useLowForVAConditionUP || (Low[0] > dynamicLowerThreshold && Low[0] < dynamicUpperThreshold)) &&
				(!UsePrevBarInVA || (Open[1] > dynamicLowerThreshold && Open[1] < dynamicUpperThreshold)) &&
				(!EnableDistanceFromVWAPCondition || (distanceInTicks >= MinDistanceFromVWAP && distanceInTicks <= MaxDistanceFromVWAP)) &&
				(EnablePriorHiLowUpSignal ? CheckPriorHiLowUpSignal() : true);																						   

            double openCloseDiff = Math.Abs(Open[0] - Close[0]) / TickSize;
            double highLowDiff = Math.Abs(High[0] - Low[0]) / TickSize;
            bool limusineCondition = (ShowLimusineOpenCloseUP && openCloseDiff >= MinimumTicks && openCloseDiff <= MaximumTicks && Close[0] > Open[0]) ||
                                    (ShowLimusineHighLowUP && highLowDiff >= MinimumTicks && highLowDiff <= MaximumTicks && Close[0] > Open[0]);

            bool std3Condition = !EnableSTD3HighLowTracking || Values[7][0] >= highestSTD3Upper;
            bool rangeBreakoutCondition = !EnablePreviousSessionRangeBreakout || 
                (previousSessionHighStd1Upper != double.MinValue && Close[0] > previousSessionHighStd1Upper);
            
			bool pocCondition = true;
			if (pocConditionEnabled)
			{
				pocCondition = pocSeries[0] <= Close[0] - pocTicksDistance * TickSize;
			}
			
			bool deltaPercentCondition = true;
			if (DeltaPercentUPEnabled)
			{
				var barsType = Bars.BarsSeries.BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
				if (barsType != null)
				{
					double deltaPercent = barsType.Volumes[CurrentBar].GetDeltaPercent();
					deltaPercentCondition = (deltaPercent >= MinDeltaPercentUP && deltaPercent <= MaxDeltaPercentUP);
				}
			}
			
            // return bvaCondition && limusineCondition && std3Condition && rangeBreakoutCondition;
			bool showUpArrow = bvaCondition && limusineCondition && std3Condition && rangeBreakoutCondition && pocCondition && deltaPercentCondition;

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
			if (!ShouldAllowSignals(Close[0], false))
				return false;
			
			if (IsPriceInPriorRange())
				return false;
			
			// Vérifier si le prix est dans la Value Area précédente
			if (BlockSignalsInPreviousValueArea && IsPriceInPreviousValueArea())
				return false;
			
			// Vérifier si le prix est dans la Dynamic Area précédente
			if (BlockSignalsInPreviousDynamicArea && IsPriceInPreviousDynamicArea())
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
			
			// Définir les seuils basés sur le DynamicAreaLevel sélectionné
			double dynamicUpperThreshold, dynamicLowerThreshold;
			switch (SelectedDynamicArea)
			{
				case DynamicAreaLevel.STD05:
					dynamicUpperThreshold = Values[1][0];
					dynamicLowerThreshold = Values[2][0];
					break;
				case DynamicAreaLevel.STD1:
					dynamicUpperThreshold = Values[3][0];
					dynamicLowerThreshold = Values[4][0];
					break;
				case DynamicAreaLevel.STD2:
					dynamicUpperThreshold = Values[5][0];
					dynamicLowerThreshold = Values[6][0];
					break;
				case DynamicAreaLevel.STD3:
					dynamicUpperThreshold = Values[7][0];
					dynamicLowerThreshold = Values[8][0];
					break;
				default:
					dynamicUpperThreshold = Values[3][0];
					dynamicLowerThreshold = Values[4][0];
					break;
			}
			
			// Ajuster les seuils avec l'offset
			dynamicUpperThreshold += DynamicAreaOffsetTicks * TickSize;
			dynamicLowerThreshold -= DynamicAreaOffsetTicks * TickSize;
            
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
				(!UseVolumeS || Volume[0] >= volumeMaxS) &&						
				(!UseVolumeIncrease || CheckVolumeIncrease()) &&
                (!OKisAfterBarsSinceResetDown || withinSignalTime) &&
				(!OKisBelovLowerThreshold || Close[0] < (selectedLowerLevel - MinEntryDistanceDOWN * TickSize)) &&
				(!OKisWithinMaxEntryDistanceDown || Close[0] >= (selectedLowerLevel - MaxEntryDistanceDOWN * TickSize)) &&
                (!OKisLowerBreakoutCountExceeded || lowerBreakoutCount < MaxLowerBreakouts) &&
                (!useOpenForVAConditionDown || (Open[0] > lowerThreshold && Open[0] < upperThreshold)) &&
                (!useHighForVAConditionDown || (High[0] > lowerThreshold && High[0] < upperThreshold)) &&
				(!UsePrevBarInVA || (Open[1] > lowerThreshold && Open[1] < upperThreshold)) &&
				//(!useOpenForVAConditionDown || (Open[0] > dynamicLowerThreshold && Open[0] < dynamicUpperThreshold)) &&
				//(!useHighForVAConditionDown || (High[0] > dynamicLowerThreshold && High[0] < dynamicUpperThreshold)) &&
				(!UsePrevBarInVA || (Open[1] > dynamicLowerThreshold && Open[1] < dynamicUpperThreshold)) &&
				(!EnableDistanceFromVWAPCondition || (distanceInTicks >= MinDistanceFromVWAP && distanceInTicks <= MaxDistanceFromVWAP)) &&
				(EnablePriorHiLowDownSignal ? CheckPriorHiLowDownSignal() : true);																							 

            double openCloseDiff = Math.Abs(Open[0] - Close[0]) / TickSize;
            double highLowDiff = Math.Abs(High[0] - Low[0]) / TickSize;
            bool limusineCondition = (ShowLimusineOpenCloseDOWN && openCloseDiff >= MinimumTicks && openCloseDiff <= MaximumTicks && Close[0] < Open[0]) ||
                                    (ShowLimusineHighLowDOWN && highLowDiff >= MinimumTicks && highLowDiff <= MaximumTicks && Close[0] < Open[0]);

            bool std3Condition = !EnableSTD3HighLowTracking || Values[8][0] <= lowestSTD3Lower;
            bool rangeBreakoutCondition = !EnablePreviousSessionRangeBreakout || 
                (previousSessionLowStd1Lower != double.MaxValue && Close[0] < previousSessionLowStd1Lower);
            
			bool pocCondition = true;
			if (pocConditionEnabled)
			{
				pocCondition = pocSeries[0] >= Close[0] + pocTicksDistance * TickSize;
			}
			
			bool deltaPercentCondition = true;
			if (DeltaPercentDOWNEnabled)
			{
				var barsType = Bars.BarsSeries.BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
				if (barsType != null)
				{
					double deltaPercent = barsType.Volumes[CurrentBar].GetDeltaPercent();
					deltaPercentCondition = (deltaPercent >= MinDeltaPercentDOWN && deltaPercent <= MaxDeltaPercentDOWN);
				}
			}
			
            // return bvaCondition && limusineCondition && std3Condition && rangeBreakoutCondition;
			bool showDownArrow = bvaCondition && limusineCondition && std3Condition && rangeBreakoutCondition && pocCondition && deltaPercentCondition;
			
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
			
			dynamicAreaPointsDrawn = false;					  
			volumeMaxS = 0;	  
        }
		// ############################################################################# //
		
		#region Properties
		
		[NinjaScriptProperty]
        [Display(Name = "Position Size", GroupName = "0.02_Position Management", Order = 0)]
        public int PosSize { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Position Split %", GroupName = "0.02_Position Management", Order = 1)]
        public double PositionSplitPercent { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Stop Ticks", GroupName = "0.03_Exit Module", Order = 1)]
        public double StopTicks { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Profit Ticks", GroupName = "0.03_Exit Module", Order = 2)]
        public double ProfitOneTicks { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Profit Ticks 2", GroupName = "0.03_Exit Module", Order = 3)]
        public double ProfitTwoTicks { get; set; }
		
		// ############ //
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
		[Range(1,10)]
		[Display(Name="Up Breakout Bars", Description="Nombre de barres à considérer pour le plus haut (2 à 5)", Order=9, GroupName="0.3_Buy")]
		public int UpBreakoutBars { get; set; }		
		[NinjaScriptProperty]
		[Range(0,int.MaxValue)]
		[Display(Name="Up Breakout Offset Ticks", Description="Offset en ticks au-dessus du plus haut pour confirmer la cassure", Order=10, GroupName="0.3_Buy")]
		public int UpBreakoutOffsetTicks { get; set; }
        // ############ Sell #############
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
		[Range(1,10)]
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
		// ################## 1.05_Initial Balance #########################
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
		
		// ######################### 1.6_Dynamic Area Parameters ########################################## //
		[NinjaScriptProperty]
		[Display(Name="Dynamic Area Level", Description="Choose which Standard Deviation level to use for Dynamic Area", Order=1, GroupName="1.6_Dynamic Area Parameters")]
		public DynamicAreaLevel SelectedDynamicArea { get; set; }		
		[NinjaScriptProperty]
		[Display(Name="Block Signals in Previous Dynamic Area", Description="Block signals when price is inside previous session's Dynamic Area", Order=2, GroupName="1.6_Dynamic Area Parameters")]
		public bool BlockSignalsInPreviousDynamicArea { get; set; }		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="Dynamic Area Offset Ticks", Description="Offset en ticks pour la Dynamic Area précédente", Order=3, GroupName="1.6_Dynamic Area Parameters")]
		public int DynamicAreaOffsetTicks { get; set; }
		[NinjaScriptProperty]
        [Display(Name = "DynamicAreaDrawDelayMinutes", Order = 4, GroupName = "1.6_Dynamic Area Parameters")]
        public int DynamicAreaDrawDelayMinutes
        {
            get { return dynamicAreaDrawDelayMinutes; }
            set { dynamicAreaDrawDelayMinutes = value; }
        }
		
        // ############ Volume #############
        // Volume
        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Fperiod Vol", Order = 1, GroupName = "Volume")]
        public int FperiodVol { get; set; }       
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "OKisVOL", Description = "Check Volume", Order = 2, GroupName = "Volume")]
        public bool OKisVOL { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Use Volume S", Description="Active la comparaison avec le volume maximum de la période", Order=3, GroupName="Volume")]
		public bool UseVolumeS { get; set; }		
		[NinjaScriptProperty]
		[Display(Name="Enable Volume Analysis Period", Description="Active la période d'analyse du volume maximum", Order=4, GroupName="Volume")]
		public bool EnableVolumeAnalysisPeriod { get; set; }
		[NinjaScriptProperty]
		[Display(Name = "Use Volume Increase", Description = "Enable volume increase check", Order = 5, GroupName = "Volume")]
		public bool UseVolumeIncrease { get; set; }
		[NinjaScriptProperty]
		[Display(Name = "Volume Bars to Compare", Description = "Number of previous bars to compare volume with", Order = 6, GroupName = "Volume")]
		[Range(1, 10)]
		public int VolumeBarsToCompare { get; set; }

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
		
		public enum StopTargetReferenceType
		{
			ValueArea,
			OpenClose
		}
		
		[NinjaScriptProperty]
		[Display(Name = "Stop Target Reference", Description = "Choose reference type for Stop and Target calculations", Order = 0, GroupName = "0.04_VA Stop Target Module")]
		public StopTargetReferenceType StopTargetReference { get; set; }
		
		// Renommer la propriété existante pour plus de clarté
		[NinjaScriptProperty]
		[Display(Name = "Use Custom Stop Target", Description = "Use Custom calculations for Stop and Target", Order = 1, GroupName = "0.04_VA Stop Target Module")]
		public bool UseCustomStopTarget { get; set; } // Ancien UseVaStopTarget

		[NinjaScriptProperty]
		[Display(Name = "Position Size VA", GroupName = "0.04_VA Stop Target Module", Order = 2)]
		public int PosSizeVA { get; set; }
		[NinjaScriptProperty]
		[Display(Name = "Position Split % VA", GroupName = "0.04_VA Stop Target Module", Order = 3)]
		public double PositionSplitPercentVA { get; set; }
		[NinjaScriptProperty]
		[Display(Name = "Stop Multiplier VA", GroupName = "0.04_VA Stop Target Module", Order = 4)]
		public double StopMultiplierVA { get; set; }
		[NinjaScriptProperty]
		[Display(Name = "Profit One Multiplier VA", GroupName = "0.04_VA Stop Target Module", Order = 5)]
		public double ProfitOneMultiplierVA { get; set; }
		[NinjaScriptProperty]
		[Display(Name = "Profit Two Multiplier VA", GroupName = "0.04_VA Stop Target Module", Order = 6)]
		public double ProfitTwoMultiplierVA { get; set; }
		
		
		// ########################### 0.06_TrailingStop ######################### //
		[NinjaScriptProperty]
		[Display(Name = "Use Trailing Stop VA", Description = "Enable Trailing Stop", Order = 1, GroupName = "0.06_TrailingStop")]
		public bool UseTrailingStopVA { get; set; }
		[NinjaScriptProperty]
		[Display(Name = "Trailing Stop Type", Description = "Select the type of trailing stop", Order = 2, GroupName = "0.06_TrailingStop")]
		public TrailingStopType SelectedTrailingStopType { get; set; }
		public enum TrailingStopType
		{
			STD1,   // Standard Deviation 1
			STD05,  // Standard Deviation 0.5
			VWAP    // VWAP
		}
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Trailing Stop Offset Ticks", Description = "Offset in ticks for trailing stop", Order = 3, GroupName = "0.06_TrailingStop")]
		public int TrailingStopOffsetTicks { get; set; }
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Trailing Stop Min Bars", Description = "Minimum bars before trailing stop becomes active", Order = 4, GroupName = "0.06_TrailingStop")]
		public int TrailingStopMinBars { get; set; }
		
		// ############### 0.05_Break Even Parameters ####################### //
		public enum BreakEvenMode
		{
			Disabled,   // Break Even désactivé
			Fixed,      // Break Even avec ticks fixes
			ValueArea,    // Break Even basé sur la Value Area
			OpenClose
		}
		[NinjaScriptProperty]
		[Display(Name = "Break Even Mode", Description = "Choose the Break Even mode", Order = 1, GroupName = "0.05_Break Even Parameters")]
		public BreakEvenMode SelectedBreakEvenMode { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Fixed: Offset Ticks", Description = "Number of ticks to offset the break even price", Order = 2, GroupName = "0.05_Break Even Parameters")]
		public int BreakEvenOffsetTicks { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "VA/OC: Offset Multiplier", Description = "Multiplier for Value Area or Open-Close offset", Order = 3, GroupName = "0.05_Break Even Parameters")]
		public double BreakEvenOffsetMultiplierVA { get; set; }
		
		// ################################ Prior Day OHLC ############################################## //
		[NinjaScriptProperty]
		[Display(Name="Enable Prior High Low Up Signal", Description="Activer la condition Prior High pour le signal UP", Order=1, GroupName="Prior Day OHLC")]
		public bool EnablePriorHiLowUpSignal { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Prior High Low Down Signal", Description="Activer la condition Prior Low pour le signal DOWN", Order=2, GroupName="Prior Day OHLC")]
		public bool EnablePriorHiLowDownSignal { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="Ticks Offset High", Description="Nombre de ticks au-dessus du Prior High", Order=3, GroupName="Prior Day OHLC")]
		public int TicksOffsetHigh { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="Ticks Offset Low", Description="Nombre de ticks en-dessous du Prior Low", Order=4, GroupName="Prior Day OHLC")]
		public int TicksOffsetLow { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Block Signals In Prior Range", Description="Bloquer les signaux quand le prix est dans la range du Prior Day", Order=5, GroupName="Prior Day OHLC")]
		public bool BlockSignalHiLowPriorRange { get; set; }
		private PriorDayOHLC priorDayOHLC;
		// ################################ Prior Day OHLC ############################################## //
		// ############################ Prior VA Vwap ######################################### //
		private OrderFlowVWAP vwap;
		private double priorSessionUpperBand;
		private double priorSessionLowerBand;
		private bool newSession;
		
		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Upper Offset Ticks", Description = "Number of ticks above upper band", Order=1, GroupName="Prior VA Vwap")]
		public int UpperOffsetTicks { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Lower Offset Ticks", Description = "Number of ticks below lower band", Order=2, GroupName="Prior VA Vwap")]
		public int LowerOffsetTicks { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Use Prior SVA UP", Description = "Only up arrows when price above prior session VA", Order=3, GroupName="Prior VA Vwap")]
		public bool UsePriorSvaUP { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Use Prior SVA Down", Description = "Only down arrows when price below prior session VA", Order=4, GroupName="Prior VA Vwap")]
		public bool UsePriorSvaDown { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Block In Prior SVA", Description = "Block arrows inside prior session Value Area", Order=5, GroupName="Prior VA Vwap")]
		public bool BlockInPriorSVA { get; set; }
		// ############################ Prior VA Vwap ######################################### //
		// ############################ POC Parameters ######################################### //
		private bool pocConditionEnabled;
		private int pocTicksDistance;
		private Series<double> pocSeries;
		[NinjaScriptProperty]
		[Display(Name="Enable POC Condition", Description="Enable the Point of Control condition", Order=1, GroupName="POC Parameters")]
		public bool POCConditionEnabled
		{
			get { return pocConditionEnabled; }
			set { pocConditionEnabled = value; }
		}
		
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="POC Ticks Distance", Description="Number of ticks for POC distance from close", Order=2, GroupName="POC Parameters")]
		public int POCTicksDistance
		{
			get { return pocTicksDistance; }
			set { pocTicksDistance = Math.Max(0, value); }
		}
		// ############################ POC Parameters ######################################### //
		// ############################ DeltaPercentUP ######################################### //
		private class VolumetricParameters
		{
			public bool Enabled { get; set; }
			public double Min { get; set; }
			public double Max { get; set; }
		}
		private VolumetricParameters[] upParameters;
		
		[NinjaScriptProperty]
		[Display(Name = "Delta Percent UP Enabled", Order = 1, GroupName = "DeltaPercentUP")]
		public bool DeltaPercentUPEnabled
		{
			get { return upParameters[1].Enabled; }
			set { upParameters[1].Enabled = value; }
		}
		
		[NinjaScriptProperty]
		[Display(Name = "Min Delta Percent UP", Order = 2, GroupName = "DeltaPercentUP")]
		public double MinDeltaPercentUP
		{
			get { return upParameters[1].Min; }
			set { upParameters[1].Min = value; }
		}
		
		[NinjaScriptProperty]
		[Display(Name = "Max Delta Percent UP", Order = 3, GroupName = "DeltaPercentUP")]
		public double MaxDeltaPercentUP
		{
			get { return upParameters[1].Max; }
			set { upParameters[1].Max = value; }
		}
		// ############################ DeltaPercentUP ######################################### //
		// ############################ DeltaPercentDOWN ######################################### //
		private VolumetricParameters[] downParameters;
		
		[NinjaScriptProperty]
		[Display(Name = "Delta Percent DOWN Enabled", Order = 1, GroupName = "DeltaPercentDOWN")]
		public bool DeltaPercentDOWNEnabled
		{
			get { return downParameters[1].Enabled; }
			set { downParameters[1].Enabled = value; }
		}
		
		[NinjaScriptProperty]
		[Display(Name = "Min Delta Percent DOWN", Order = 2, GroupName = "DeltaPercentDOWN")]
		public double MinDeltaPercentDOWN
		{
			get { return downParameters[1].Min; }
			set { downParameters[1].Min = value; }
		}
		
		[NinjaScriptProperty]
		[Display(Name = "Max Delta Percent DOWN", Order = 3, GroupName = "DeltaPercentDOWN")]
		public double MaxDeltaPercentDOWN
		{
			get { return downParameters[1].Max; }
			set { downParameters[1].Max = value; }
		}
		// ############################ DeltaPercentDOWN ######################################### //
        #endregion
    }
}
