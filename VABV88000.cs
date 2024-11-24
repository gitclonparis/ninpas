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

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies.ninpas
{
	public class VABV88000 : Strategy
	{
		private double sumPriceVolume;
        private double sumVolume;
        private double sumSquaredPriceVolume;
        private DateTime lastResetTime;
        private int barsSinceReset;
        private int upperBreakoutCount;
        private int lowerBreakoutCount;
        
        private ADX ADX1;
        private ATR ATR1;
        private VOL VOL1;
        private VOLMA VOLMA1;
		// private OrderFlowCumulativeDelta cumulativeDelta; // Cumulative Delta variable
        
        private double highestSTD3Upper;
        private double lowestSTD3Lower;
        private bool isFirstBarSinceReset;

        private class VolumetricParameters
        {
            public bool Enabled { get; set; }
            public double Min { get; set; }
            public double Max { get; set; }
        }

        private VolumetricParameters[] upParameters;
        private VolumetricParameters[] downParameters;
		
		private bool pocConditionEnabled;
		private int pocTicksDistance;
		private Series<double> pocSeries;
		
		// Nouveaux champs pour la condition CumulativeDelta
		private bool enableCumulativeDeltaConditionUP;
		private bool enableCumulativeDeltaConditionDOWN;
		private int cumulativeDeltaBarsRangeUP;
		private int cumulativeDeltaBarsRangeDOWN;
		private int cumulativeDeltaJumpUP;
		private int cumulativeDeltaJumpDOWN;
		// 
		private bool enableBarDeltaConditionUP;
		private bool enableBarDeltaConditionDOWN;
		private int barDeltaBarsRangeUP;
		private int barDeltaBarsRangeDOWN;
		private int barDeltaJumpUP;
		private int barDeltaJumpDOWN;
		//
		private bool enableDeltaPercentConditionUP;
		private bool enableDeltaPercentConditionDOWN;
		private int deltaPercentBarsRangeUP;
		private int deltaPercentBarsRangeDOWN;
		private double deltaPercentJumpUP;
		private double deltaPercentJumpDOWN;
		// IB
		private double ibHigh = double.MinValue;
		private double ibLow = double.MaxValue;
		private bool ibPeriod = true;
		private SessionIterator sessionIterator;
		private DateTime currentDate = DateTime.MinValue;
		
		private bool breakEvenActivated;
        private double breakEvenPrice;
		
		private double previousSessionHighStd1Upper;
		private double previousSessionLowStd1Lower;
		private DateTime currentSessionStartTime;
		private double highestStd1Upper;
		private double lowestStd1Lower;
		
		public enum ValueAreaMode
		{
			STD1,  // Utilise Standard Deviation 1
			STD2,  // Utilise Standard Deviation 2
			Both   // Utilise les deux
		}

		[NinjaScriptProperty]
		[Display(Name="Value Area Mode", Description="Choisir quelle Value Area utiliser", Order=1, GroupName="0.0_Value_Area_Settings")]
		public ValueAreaMode SelectedValueArea { get; set; }
	
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Strategy OFDeltaProcent avec delta %";
				Name										= "VABV88000";
				Calculate									= Calculate.OnBarClose;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				IsInstantiatedOnEachOptimizationIteration	= false;
				Period1Start = DateTime.Parse("15:30", System.Globalization.CultureInfo.InvariantCulture);
				Period1End = DateTime.Parse("17:30", System.Globalization.CultureInfo.InvariantCulture);
				Period2Start = DateTime.Parse("18:30", System.Globalization.CultureInfo.InvariantCulture);
				Period2End = DateTime.Parse("20:30", System.Globalization.CultureInfo.InvariantCulture);
				
				Qty											= 1;
				Sl											= 15;
				Pt											= 10;
				EnableDynamicStops = false;
				UseBarSizeForStops = false;
				UseParabolicStop = false;
				EnableBreakEven = false;
                BreakEvenTicks = 5;
				UsePOCBasedStopLoss = false;
				POCStopLossOffset = 5;
				// Paramètres VAB
                ResetPeriod = 120;
                SignalTimingMode = SignalTimeMode.Bars;
				MinBarsForSignal = 10;
				MaxBarsForSignal = 100;
				MinMinutesForSignal = 5;
				MaxMinutesForSignal = 30;
				
                MinEntryDistanceUP = 3;
                MaxEntryDistanceUP = 40;
                MaxUpperBreakouts = 3;
                MinEntryDistanceDOWN = 3;
                MaxEntryDistanceDOWN = 40;
                MaxLowerBreakouts = 3;
				
                FminADX = 0;
                FmaxADX = 0;
                FminATR = 0;
                FmaxATR = 0;
                FperiodVol = 9;

                // Paramètres Limusine
                MinimumTicks = 10;
                MaximumTicks = 30;
                ShowLimusineOpenCloseUP = true;
                ShowLimusineOpenCloseDOWN = true;
                ShowLimusineHighLowUP = true;
                ShowLimusineHighLowDOWN = true;

                // Nouveaux paramètres
                EnableSlopeFilterUP = false;
                MinSlopeValueUP = 0.0;
                SlopeBarsCountUP = 5;

                EnableSlopeFilterDOWN = false;
                MinSlopeValueDOWN = 0.0;
                SlopeBarsCountDOWN = 5;

                EnableDistanceFromVWAPCondition = false;
                MinDistanceFromVWAP = 10;
                MaxDistanceFromVWAP = 50;

                EnableSTD3HighLowTracking = false;
				EnablePreviousSessionRangeBreakout = false;
				EnableRangeSizeFilter = false;
				MinRangeSize = 10;  // Par exemple, 10 ticks
				MaxRangeSize = 100; // Par exemple, 100 ticks
				RangeAdjustment = 5; // Par exemple, 5 ticks
                // Paramètres Volumetric Filter
                UpArrowColor = Brushes.Green;
                DownArrowColor = Brushes.Red;
				POCColor = Brushes.Blue;
				pocConditionEnabled = false;
				pocTicksDistance = 2;
				// Initialize new parameter for Value Area condition
                useOpenForVAConditionUP = false;
				useOpenForVAConditionDown = false;
				useLowForVAConditionUP = false;
				useHighForVAConditionDown = false;
				// Initialize the new cumulative delta properties
                // EnableUPlimDeltaSession = false;
                // EnableDownLimDeltaSession = false;
                // MinVdeltaCsession = 200;
                // MaxVdeltaCsession = 10000;
				// Nouveaux paramètres pour la condition CumulativeDelta
				enableCumulativeDeltaConditionUP = false;
				enableCumulativeDeltaConditionDOWN = false;
				cumulativeDeltaBarsRangeUP = 3;
				cumulativeDeltaBarsRangeDOWN = 3;
				cumulativeDeltaJumpUP = 1000;
				cumulativeDeltaJumpDOWN = 1000;
				// 
				enableBarDeltaConditionUP = false;
				enableBarDeltaConditionDOWN = false;
				barDeltaBarsRangeUP = 3;
				barDeltaBarsRangeDOWN = 3;
				barDeltaJumpUP = 50;
				barDeltaJumpDOWN = 50;
				//
				enableDeltaPercentConditionUP = false;
				enableDeltaPercentConditionDOWN = false;
				deltaPercentBarsRangeUP = 3;
				deltaPercentBarsRangeDOWN = 3;
				deltaPercentJumpUP = 1.0;
				deltaPercentJumpDOWN = 1.0;
				//
				EnableMaxMinDeltaConditionUP = false;
				EnableMaxMinDeltaConditionDOWN = false;
				MinMaxDelta0UP = 100;
				MaxMinDelta0UP = -50;
				MaxMinDelta0DOWN = -100;
				MinMaxDelta0DOWN = 50;
				//
				EnableMaxVolumeRangeConditionUP = false;
				EnableMaxVolumeRangeConditionDOWN = false;
				MaxVolumeBarsRangeUP = 3;
				MaxVolumeBarsRangeDOWN = 3;
				MaxVolumeJumpUP = 1;
				MaxVolumeJumpDOWN = 1;
				//
				EnablePOCConditionUP = false;
				EnablePOCConditionDOWN = false;
                POCBarsLookBackUP = 2;
                POCBarsLookBackDOWN = 2;
				//
				EnableSTD3DistanceConditionUP = false;
				EnableSTD3DistanceConditionDOWN = false;
				STD3DistanceOffsetUP = 1;
				STD3DistanceOffsetDOWN = 1;
				//
				EnablePOCVolumeConditionUP = false;
				EnablePOCVolumeConditionDOWN = false;
				MinPOCVolumeUP = 100;
				MinPOCVolumeDOWN = 100;
				// Initial Balance
				EnableIBLogic = false;
				IBStartTime = DateTime.Parse("15:30", System.Globalization.CultureInfo.InvariantCulture);
				IBEndTime = DateTime.Parse("16:00", System.Globalization.CultureInfo.InvariantCulture);
				IBOffsetTicks = 0;
				//
				EnableVABreakoutReversalUP = false;
				EnableVABreakoutReversalDOWN = false;
				VABreakoutBarsRangeUP = 5;
				VABreakoutBarsRangeDOWN = 5;
				VABreakoutOffsetTicks = 2;
				//
				EnableVABreakoutReversalUP2 = false;
				EnableVABreakoutReversalDOWN2 = false;
				VABreakoutBarsRangeUP2 = 5;
				VABreakoutBarsRangeDOWN2 = 5;
				VABreakoutOffsetTicks2 = 2;
				//
				EnableVABreakoutReversalUP3 = false;
				EnableVABreakoutReversalDOWN3 = false;
				VABreakoutBarsRangeUP3 = 5;
				VABreakoutBarsRangeDOWN3 = 5;
				VABreakoutOffsetTicks3 = 2;
				//
				EnableVABreakoutReversalUP4 = false;
				EnableVABreakoutReversalDOWN4 = false;
				VABreakoutBarsRangeUP4 = 5;
				VABreakoutBarsRangeDOWN4 = 5;
				VABreakoutOffsetTicks4 = 2;
				//
				EnableVABreakoutReversalUP5 = false;
				EnableVABreakoutReversalDOWN5 = false;
				VABreakoutBarsRangeUP5 = 5;
				VABreakoutBarsRangeDOWN5 = 5;
				VABreakoutOffsetTicks5 = 2;
				UsePreviousBarUP5 = false;
				UsePreviousBarDOWN5 = false;
				//
				EnableSellersForBuy = false;
				EnableBuyersForSell = false;
				SellersDifferenceThreshold = 100; // Valeur par défaut à ajuster
				BuyersDifferenceThreshold = 100; // Valeur par défaut à ajuster

                InitializeVolumetricParameters();
				SelectedValueArea = ValueAreaMode.STD1;

                AddPlot(Brushes.Orange, "VWAP");
                AddPlot(Brushes.Red, "StdDev1Upper");
                AddPlot(Brushes.Red, "StdDev1Lower");
                AddPlot(Brushes.Green, "StdDev2Upper");
                AddPlot(Brushes.Green, "StdDev2Lower");
                AddPlot(Brushes.Blue, "StdDev3Upper");
                AddPlot(Brushes.Blue, "StdDev3Lower");
				AddPlot(new Stroke(POCColor, 2), PlotStyle.Dot, "POC");
			}
			else if (State == State.Configure)
			{
				ResetValues(DateTime.MinValue);
				// AddDataSeries(Data.BarsPeriodType.Tick, 1);
			}
			else if (State == State.DataLoaded)
            {
                ADX1 = ADX(Close, 14);
                ATR1 = ATR(Close, 14);
                VOL1 = VOL(Close);
                VOLMA1 = VOLMA(Close, Convert.ToInt32(FperiodVol));
				pocSeries = new Series<double>(this);
				sessionIterator = new SessionIterator(Bars);
				// cumulativeDelta = OrderFlowCumulativeDelta(CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0);
            }
		}
		private void InitializeVolumetricParameters()
        {
            upParameters = new VolumetricParameters[7];
            downParameters = new VolumetricParameters[7];

            for (int i = 0; i < 7; i++)
            {
                upParameters[i] = new VolumetricParameters();
                downParameters[i] = new VolumetricParameters();
            }

            // Set default values (you can adjust these as needed)
            SetDefaultParameterValues(upParameters[0], false, 200, 2000);    // BarDelta
            SetDefaultParameterValues(upParameters[1], false, 10, 50);       // DeltaPercent
            SetDefaultParameterValues(upParameters[2], false, 100, 1000);    // DeltaChange
            SetDefaultParameterValues(upParameters[3], false, 1000, 10000);  // TotalBuyingVolume
            SetDefaultParameterValues(upParameters[4], false, 0, 5000);      // TotalSellingVolume
            SetDefaultParameterValues(upParameters[5], false, 100, 1000);    // Trades
            SetDefaultParameterValues(upParameters[6], false, 2000, 20000);  // TotalVolume

            // Set default values for down parameters (adjust as needed)
            SetDefaultParameterValues(downParameters[0], false, 200, 2000);  // BarDelta (abs value)
            SetDefaultParameterValues(downParameters[1], false, 10, 50);     // DeltaPercent (abs value)
            SetDefaultParameterValues(downParameters[2], false, 100, 1000);  // DeltaChange (abs value)
            SetDefaultParameterValues(downParameters[3], false, 0, 5000);    // TotalBuyingVolume
            SetDefaultParameterValues(downParameters[4], false, 1000, 10000);// TotalSellingVolume
            SetDefaultParameterValues(downParameters[5], false, 100, 1000);  // Trades
            SetDefaultParameterValues(downParameters[6], false, 2000, 20000);// TotalVolume
        }
		
		private void SetDefaultParameterValues(VolumetricParameters param, bool enabled, double min, double max)
        {
            param.Enabled = enabled;
            param.Min = min;
            param.Max = max;
        }

		protected override void OnBarUpdate()
		{
			// if (BarsInProgress == 1)
            // {
                // cumulativeDelta.Update(cumulativeDelta.BarsArray[1].Count - 1, 1);
                // return;
            // }
			if (BarsInProgress != 0) 
				return;
			
			if (CurrentBar < 20 || !(Bars.BarsSeries.BarsType is NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType))
				return;
			
			var currentBarVolumes = barsType.Volumes[CurrentBar];
			// Calcul du POC
			double pocPrice;
			long maxVolume = currentBarVolumes.GetMaximumVolume(null, out pocPrice);
			pocSeries[0] = pocPrice;
			Values[0][0] = pocPrice;

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
					// Store the previous session's range
					previousSessionHighStd1Upper = highestStd1Upper;
					previousSessionLowStd1Lower = lowestStd1Lower;
					AdjustPreviousSessionRange();
				}
	
				ResetValues(currentBarTime);
				currentSessionStartTime = currentBarTime;
			}

            // if (Bars.IsFirstBarOfSession)
			// {
				// ResetValues(Time[0]);
			// }
			// else if (lastResetTime != DateTime.MinValue && (Time[0] - lastResetTime).TotalMinutes >= ResetPeriod)
			// {
				// ResetValues(Time[0]);
			// }

            // Calcul VWAP et écarts-types
            double typicalPrice = (High[0] + Low[0] + Close[0]) / 3;
            double volume = Volume[0];

            sumPriceVolume += typicalPrice * volume;
            sumVolume += volume;
            sumSquaredPriceVolume += typicalPrice * typicalPrice * volume;

            double vwap = sumPriceVolume / sumVolume;
            double variance = (sumSquaredPriceVolume / sumVolume) - (vwap * vwap);
            double stdDev = Math.Sqrt(variance);

            Values[0][0] = vwap;
            Values[1][0] = vwap + stdDev;
            Values[2][0] = vwap - stdDev;
            Values[3][0] = vwap + 2 * stdDev;
            Values[4][0] = vwap - 2 * stdDev;
            Values[5][0] = vwap + 3 * stdDev;
            Values[6][0] = vwap - 3 * stdDev;
			
			// Update session high/low for StdDev1
			if (EnablePreviousSessionRangeBreakout)
			{
				highestStd1Upper = Math.Max(highestStd1Upper, Values[1][0]);
				lowestStd1Lower = Math.Min(lowestStd1Lower, Values[2][0]);
			}

            // Update STD3 high/low tracking
            if (EnableSTD3HighLowTracking)
            {
                if (isFirstBarSinceReset)
                {
                    highestSTD3Upper = Values[5][0];
                    lowestSTD3Lower = Values[6][0];
                    isFirstBarSinceReset = false;
                }
                else
                {
                    highestSTD3Upper = Math.Max(highestSTD3Upper, Values[5][0]);
                    lowestSTD3Lower = Math.Min(lowestSTD3Lower, Values[6][0]);
                }
            }

            barsSinceReset++;
			// Vérifier si nous sommes dans une période de trading autorisée
			bool isInTradingPeriod = IsInTradingPeriod(Time[0]);
            // Vérification des conditions combinées
            bool showUpArrow = ShouldDrawUpArrow() && CheckVolumetricConditions(true);
            bool showDownArrow = ShouldDrawDownArrow() && CheckVolumetricConditions(false);
			// Appliquer la logique IB
			ApplyIBLogic(ref showUpArrow, ref showDownArrow);
			
			// Condition POC
			if (pocConditionEnabled)
			{
				double closePrice = Close[0];
				double tickSize = TickSize;
		
				showUpArrow = showUpArrow && (pocPrice <= closePrice - pocTicksDistance * tickSize);
				showDownArrow = showDownArrow && (pocPrice >= closePrice + pocTicksDistance * tickSize);
			}
			
			if (isInTradingPeriod)
			{
				// //			
				if (showUpArrow)
				{
					SetEntryParameters(true);
					EnterLong(Convert.ToInt32(Qty), @"Long");
					
					Draw.ArrowUp(this, "UpArrow" + CurrentBar, true, 0, Low[0] - 2 * TickSize, UpArrowColor);
					upperBreakoutCount++;
	
					// Calcul de la distance entre VWAP et StdDev1 Lower (StdDev-1)
					double distanceRed = Values[0][0] - Values[2][0]; // VWAP - StdDev1 Lower
					double priceForRedDot = Close[0] - distanceRed;
	
					// Dessiner le point rouge
					Draw.Dot(this, "RedDotUp" + CurrentBar, true, 0, priceForRedDot, Brushes.Red);
	
					// Calcul de la distance pour le point bleu (comme précédemment)
					double distanceBlue = Values[1][0] - Values[0][0]; // StdDev1 Upper - VWAP
					double priceForBlueDot = Close[0] + distanceBlue;
	
					// Dessiner le point bleu
					Draw.Dot(this, "BlueDotUp" + CurrentBar, true, 0, priceForBlueDot, Brushes.Blue);
	
					// Dessiner le point blanc au prix actuel
					Draw.Dot(this, "WhiteDotUp" + CurrentBar, true, 0, Close[0], Brushes.White);
					Draw.Dot(this, "POCUP" + CurrentBar, false, 0, pocPrice - pocTicksDistance * TickSize, POCColor);
				}
				else if (showDownArrow)
				{
					SetEntryParameters(false);
					EnterShort(Convert.ToInt32(Qty), @"Short");
					Draw.ArrowDown(this, "DownArrow" + CurrentBar, true, 0, High[0] + 2 * TickSize, DownArrowColor);
					lowerBreakoutCount++;
	
					// Calcul de la distance entre StdDev1 Upper (StdDev+1) et VWAP
					double distanceRed = Values[1][0] - Values[0][0]; // StdDev1 Upper - VWAP
					double priceForRedDot = Close[0] + distanceRed;
	
					// Dessiner le point rouge
					Draw.Dot(this, "RedDotDown" + CurrentBar, true, 0, priceForRedDot, Brushes.Red);
	
					// Calcul de la distance pour le point bleu (comme précédemment)
					double distanceBlue = Values[0][0] - Values[2][0]; // VWAP - StdDev1 Lower
					double priceForBlueDot = Close[0] - distanceBlue;
	
					// Dessiner le point bleu
					Draw.Dot(this, "BlueDotDown" + CurrentBar, true, 0, priceForBlueDot, Brushes.Blue);
	
					// Dessiner le point blanc au prix actuel
					Draw.Dot(this, "WhiteDotDown" + CurrentBar, true, 0, Close[0], Brushes.White);
					Draw.Dot(this, "POCDOWN" + CurrentBar, false, 0, pocPrice + pocTicksDistance * TickSize, POCColor);
				}
			}
			
			// Add Break Even logic
            if (EnableBreakEven && !breakEvenActivated)
            {
                foreach (Position position in Positions)
                {
                    if (position.MarketPosition == MarketPosition.Long)
                    {
                        if (position.GetUnrealizedProfitLoss(PerformanceUnit.Ticks) >= BreakEvenTicks)
                        {
                            breakEvenPrice = position.AveragePrice;
                            SetStopLoss(CalculationMode.Price, breakEvenPrice);
                            breakEvenActivated = true;
                        }
                    }
                    else if (position.MarketPosition == MarketPosition.Short)
                    {
                        if (position.GetUnrealizedProfitLoss(PerformanceUnit.Ticks) >= BreakEvenTicks)
                        {
                            breakEvenPrice = position.AveragePrice;
                            SetStopLoss(CalculationMode.Price, breakEvenPrice);
                            breakEvenActivated = true;
                        }
                    }
                }
            }
		}
		
		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            // Reset Break Even flag on new trades
            if (execution.Order.OrderState == OrderState.Filled)
            {
                breakEvenActivated = false;
            }
        }
		
		private bool IsInTradingPeriod(DateTime time)
		{
			TimeSpan currentTime = time.TimeOfDay;
			return (currentTime >= Period1Start.TimeOfDay && currentTime <= Period1End.TimeOfDay) ||
				(currentTime >= Period2Start.TimeOfDay && currentTime <= Period2End.TimeOfDay);
		}
		
		private bool CheckCumulativeDeltaConditionUP()
		{
			if (!enableCumulativeDeltaConditionUP || CurrentBar < cumulativeDeltaBarsRangeUP)
				return true;
	
			NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType = Bars.BarsSeries.BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
			if (barsType == null)
				return true;
	
			for (int i = 0; i < cumulativeDeltaBarsRangeUP - 1; i++)
			{
				if (barsType.Volumes[CurrentBar - i].CumulativeDelta <= 
					barsType.Volumes[CurrentBar - i - 1].CumulativeDelta + cumulativeDeltaJumpUP)
				{
					return false;
				}
			}
			return true;
		}
	
		private bool CheckCumulativeDeltaConditionDOWN()
		{
			if (!enableCumulativeDeltaConditionDOWN || CurrentBar < cumulativeDeltaBarsRangeDOWN)
				return true;
	
			NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType = Bars.BarsSeries.BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
			if (barsType == null)
				return true;
	
			for (int i = 0; i < cumulativeDeltaBarsRangeDOWN - 1; i++)
			{
				if (barsType.Volumes[CurrentBar - i].CumulativeDelta >= 
					barsType.Volumes[CurrentBar - i - 1].CumulativeDelta - cumulativeDeltaJumpDOWN)
				{
					return false;
				}
			}
			return true;
		}
		
		private bool CheckBarDeltaConditionUP()
		{
			if (!enableBarDeltaConditionUP || CurrentBar < barDeltaBarsRangeUP)
				return true;
		
			NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType = Bars.BarsSeries.BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
			if (barsType == null)
				return true;
		
			for (int i = 0; i < barDeltaBarsRangeUP - 1; i++)
			{
				if (barsType.Volumes[CurrentBar - i].BarDelta <= 
					barsType.Volumes[CurrentBar - i - 1].BarDelta + barDeltaJumpUP)
				{
					return false;
				}
			}
			return true;
		}
		
		private bool CheckBarDeltaConditionDOWN()
		{
			if (!enableBarDeltaConditionDOWN || CurrentBar < barDeltaBarsRangeDOWN)
				return true;
		
			NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType = Bars.BarsSeries.BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
			if (barsType == null)
				return true;
		
			for (int i = 0; i < barDeltaBarsRangeDOWN - 1; i++)
			{
				if (barsType.Volumes[CurrentBar - i].BarDelta >= 
					barsType.Volumes[CurrentBar - i - 1].BarDelta - barDeltaJumpDOWN)
				{
					return false;
				}
			}
			return true;
		}
		
		private bool CheckDeltaPercentConditionUP()
		{
			if (!enableDeltaPercentConditionUP || CurrentBar < deltaPercentBarsRangeUP)
				return true;
		
			NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType = Bars.BarsSeries.BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
			if (barsType == null)
				return true;
		
			for (int i = 0; i < deltaPercentBarsRangeUP - 1; i++)
			{
				if (barsType.Volumes[CurrentBar - i].GetDeltaPercent() <= 
					barsType.Volumes[CurrentBar - i - 1].GetDeltaPercent() + deltaPercentJumpUP)
				{
					return false;
				}
			}
			return true;
		}
		
		private bool CheckDeltaPercentConditionDOWN()
		{
			if (!enableDeltaPercentConditionDOWN || CurrentBar < deltaPercentBarsRangeDOWN)
				return true;
		
			NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType = Bars.BarsSeries.BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
			if (barsType == null)
				return true;
		
			for (int i = 0; i < deltaPercentBarsRangeDOWN - 1; i++)
			{
				if (barsType.Volumes[CurrentBar - i].GetDeltaPercent() >= 
					barsType.Volumes[CurrentBar - i - 1].GetDeltaPercent() - deltaPercentJumpDOWN)
				{
					return false;
				}
			}
			return true;
		}
		
		private bool CheckMaxMinDeltaConditionUP()
		{
			if (!EnableMaxMinDeltaConditionUP)
				return true;
		
			NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType = Bars.BarsSeries.BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
			if (barsType == null)
				return true;
		
			double maxDelta0 = barsType.Volumes[CurrentBar].GetMaximumPositiveDelta();
			double minDelta0 = barsType.Volumes[CurrentBar].GetMaximumNegativeDelta();
		
			return (maxDelta0 > MinMaxDelta0UP && minDelta0 > MaxMinDelta0UP);
		}
		
		private bool CheckMaxMinDeltaConditionDOWN()
		{
			if (!EnableMaxMinDeltaConditionDOWN)
				return true;
		
			NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType = Bars.BarsSeries.BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
			if (barsType == null)
				return true;
		
			double maxDelta0 = barsType.Volumes[CurrentBar].GetMaximumPositiveDelta();
			double minDelta0 = barsType.Volumes[CurrentBar].GetMaximumNegativeDelta();
		
			return (minDelta0 < MaxMinDelta0DOWN && maxDelta0 < MinMaxDelta0DOWN);
		}
		
		private bool CheckMaxVolumeCondition(bool isUp)
		{
			if (!(Bars.BarsSeries.BarsType is NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType))
				return true;
		
			double askPrice, bidPrice;
			long maxAskVolume = barsType.Volumes[CurrentBar].GetMaximumVolume(true, out askPrice);
			long maxBidVolume = barsType.Volumes[CurrentBar].GetMaximumVolume(false, out bidPrice);
		
			if (isUp)
			{
				return !EnableMaxVolumeConditionUP || (maxAskVolume - maxBidVolume >= MaxAskBidDifferenceUP);
			}
			else
			{
				return !EnableMaxVolumeConditionDOWN || (maxBidVolume - maxAskVolume >= MaxBidAskDifferenceDOWN);
			}
		}
		
		
		private bool CheckMaxVolumeRangeCondition(bool isUp)
		{
			if (!(Bars.BarsSeries.BarsType is NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType))
				return true;
		
			// Si la condition n'est pas activée, retournez true immédiatement
			if ((isUp && !EnableMaxVolumeRangeConditionUP) || (!isUp && !EnableMaxVolumeRangeConditionDOWN))
				return true;
		
			int barsRange = isUp ? MaxVolumeBarsRangeUP : MaxVolumeBarsRangeDOWN;
			if (CurrentBar < barsRange)
				return true;
		
			for (int i = 0; i < barsRange - 1; i++)
			{
				double askPrice1, bidPrice1, askPrice2, bidPrice2;
				long maxAskVolume1 = barsType.Volumes[CurrentBar - i].GetMaximumVolume(true, out askPrice1);
				long maxBidVolume1 = barsType.Volumes[CurrentBar - i].GetMaximumVolume(false, out bidPrice1);
				long maxAskVolume2 = barsType.Volumes[CurrentBar - i - 1].GetMaximumVolume(true, out askPrice2);
				long maxBidVolume2 = barsType.Volumes[CurrentBar - i - 1].GetMaximumVolume(false, out bidPrice2);
		
				long volumeDifference1 = maxAskVolume1 - maxBidVolume1;
				long volumeDifference2 = maxAskVolume2 - maxBidVolume2;
		
				if (isUp)
				{
					if (volumeDifference1 <= volumeDifference2 + MaxVolumeJumpUP)
					{
						return false;
					}
				}
				else
				{
					if (volumeDifference1 >= volumeDifference2 - MaxVolumeJumpDOWN)
					{
						return false;
					}
				}
			}
			return true;
		}
		
		private bool CheckPOCCondition(bool isUp)
        {
            if (!(Bars.BarsSeries.BarsType is NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType))
                return true;

            if ((isUp && !EnablePOCConditionUP) || (!isUp && !EnablePOCConditionDOWN))
                return true;

            int barsLookBack = isUp ? POCBarsLookBackUP : POCBarsLookBackDOWN;
            if (CurrentBar < barsLookBack)
                return true;

            double currentPOCPrice;
            barsType.Volumes[CurrentBar].GetMaximumVolume(null, out currentPOCPrice);

            // Vérifier que le POC actuel est plus haut/bas que tous les POCs précédents
            for (int i = 1; i <= barsLookBack; i++)
            {
                double previousPOCPrice;
                barsType.Volumes[CurrentBar - i].GetMaximumVolume(null, out previousPOCPrice);

                if (isUp)
                {
                    if (currentPOCPrice <= previousPOCPrice)
                        return false;
                }
                else
                {
                    if (currentPOCPrice >= previousPOCPrice)
                        return false;
                }
            }

            return true;
        }
		
		private bool CheckSTD3DistanceCondition(bool isUp)
		{
			if ((!isUp && !EnableSTD3DistanceConditionDOWN) || (isUp && !EnableSTD3DistanceConditionUP) || CurrentBar < 1)
				return true;
		
			double currentSTD3Distance = Math.Abs(Values[5][0] - Values[6][0]);
			double previousSTD3Distance = Math.Abs(Values[5][1] - Values[6][1]);
			int offset = isUp ? STD3DistanceOffsetUP : STD3DistanceOffsetDOWN;
			double requiredDistance = previousSTD3Distance + (offset * TickSize);
		
			return currentSTD3Distance > requiredDistance;
		}
		
		private bool CheckPOCVolumeCondition(bool isUp)
		{
			if (!(Bars.BarsSeries.BarsType is NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType))
				return true;
		
			double pocPrice;
			long maxVolume = barsType.Volumes[CurrentBar].GetMaximumVolume(null, out pocPrice);
		
			if (isUp)
			{
				return !EnablePOCVolumeConditionUP || maxVolume >= MinPOCVolumeUP;
			}
			else
			{
				return !EnablePOCVolumeConditionDOWN || maxVolume >= MinPOCVolumeDOWN;
			}
		}
		
		//
		private bool CheckVABreakoutReversalUP()
		{
			if (!EnableVABreakoutReversalUP || CurrentBar < VABreakoutBarsRangeUP)
				return true;
		
			double tickOffset = VABreakoutOffsetTicks * TickSize;
			double std1Upper = Values[1][0]; // StdDev1Upper actuel
		
			// Vérifier que les barres précédentes ont clôturé au-dessus de std1Upper + offset
			for (int i = 1; i <= VABreakoutBarsRangeUP; i++)
			{
				if (Close[i] <= Values[1][i] + tickOffset)
					return false;
			}
		
			// Vérifier les conditions pour la barre actuelle
			bool opensAboveVA = Open[0] > std1Upper;
			bool lowEntersVA = Low[0] <= std1Upper;
			bool closesAboveVA = Close[0] > std1Upper;
		
			return opensAboveVA && lowEntersVA && closesAboveVA;
		}
		
		private bool CheckVABreakoutReversalDOWN()
		{
			if (!EnableVABreakoutReversalDOWN || CurrentBar < VABreakoutBarsRangeDOWN)
				return true;
		
			double tickOffset = VABreakoutOffsetTicks * TickSize;
			double std1Lower = Values[2][0]; // StdDev1Lower actuel
		
			// Vérifier que les barres précédentes ont clôturé en-dessous de std1Lower - offset
			for (int i = 1; i <= VABreakoutBarsRangeDOWN; i++)
			{
				if (Close[i] >= Values[2][i] - tickOffset)
					return false;
			}
		
			// Vérifier les conditions pour la barre actuelle
			bool opensBelowVA = Open[0] < std1Lower;
			bool highEntersVA = High[0] >= std1Lower;
			bool closesBelowVA = Close[0] < std1Lower;
		
			return opensBelowVA && highEntersVA && closesBelowVA;
		}

		private bool CheckVABreakoutReversalUP2()
		{
			if (!EnableVABreakoutReversalUP2 || CurrentBar < VABreakoutBarsRangeUP2)
				return true;

			double tickOffset = VABreakoutOffsetTicks2 * TickSize;
			double std1Upper = Values[1][0]; // StdDev1Upper actuel

			// Vérifier qu'au moins une barre dans le range a dépassé std1Upper + offset
			bool hasExceededVA = false;
			for (int i = 1; i <= VABreakoutBarsRangeUP2; i++)
			{
				if (Math.Max(High[i], Low[i]) > Values[1][i] + tickOffset)
				{
					hasExceededVA = true;
					break;
				}
			}

			if (!hasExceededVA)
				return false;

			// Vérifier qu'au moins une barre dans le range entre dans la VA
			bool hasEnteredVA = false;
			for (int i = 0; i <= VABreakoutBarsRangeUP2; i++)
			{
				if (Math.Min(High[i], Low[i]) <= std1Upper)
				{
					hasEnteredVA = true;
					break;
				}
			}

			// Vérifier que le prix actuel est au-dessus de la VA
			bool isAboveVA = Close[0] > std1Upper;

			return hasExceededVA && hasEnteredVA && isAboveVA;
		}

		private bool CheckVABreakoutReversalDOWN2()
		{
			if (!EnableVABreakoutReversalDOWN2 || CurrentBar < VABreakoutBarsRangeDOWN2)
				return true;

			double tickOffset = VABreakoutOffsetTicks2 * TickSize;
			double std1Lower = Values[2][0]; // StdDev1Lower actuel

			// Vérifier qu'au moins une barre dans le range a dépassé std1Lower - offset
			bool hasExceededVA = false;
			for (int i = 1; i <= VABreakoutBarsRangeDOWN2; i++)
			{
				if (Math.Min(High[i], Low[i]) < Values[2][i] - tickOffset)
				{
					hasExceededVA = true;
					break;
				}
			}

			if (!hasExceededVA)
				return false;

			// Vérifier qu'au moins une barre dans le range entre dans la VA
			bool hasEnteredVA = false;
			for (int i = 0; i <= VABreakoutBarsRangeDOWN2; i++)
			{
				if (Math.Max(High[i], Low[i]) >= std1Lower)
				{
					hasEnteredVA = true;
					break;
				}
			}

			// Vérifier que le prix actuel est en-dessous de la VA
			bool isBelowVA = Close[0] < std1Lower;

			return hasExceededVA && hasEnteredVA && isBelowVA;
		}
		
		//
		private bool CheckSellersForBuyCondition()
		{
			if (!EnableSellersForBuy)
				return true;
		
			if (!(Bars.BarsSeries.BarsType is NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType))
				return true;
		
			// Calculer la différence
			double maxNegDelta = barsType.Volumes[CurrentBar].GetMaximumNegativeDelta();
			double barDelta = barsType.Volumes[CurrentBar].BarDelta;
			double difference = maxNegDelta - barDelta;
		
			// La condition est vérifiée si la différence est supérieure au seuil
			return Math.Abs(difference) > Math.Abs(SellersDifferenceThreshold);
		}
		
		private bool CheckBuyersForSellCondition()
		{
			if (!EnableBuyersForSell)
				return true;
		
			if (!(Bars.BarsSeries.BarsType is NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType))
				return true;
		
			// Calculer la différence
			double maxPosDelta = barsType.Volumes[CurrentBar].GetMaximumPositiveDelta();
			double barDelta = barsType.Volumes[CurrentBar].BarDelta;
			double difference = maxPosDelta - barDelta;
		
			// La condition est vérifiée si la différence est supérieure au seuil
			return Math.Abs(difference) > Math.Abs(BuyersDifferenceThreshold);
		}
		
		//
		private bool CheckVABreakoutReversalUP3()
		{
			if (!EnableVABreakoutReversalUP3 || CurrentBar < VABreakoutBarsRangeUP3)
				return true;
		
			double tickOffset = VABreakoutOffsetTicks3 * TickSize;
			double std1Upper = Values[1][0]; // StdDev1Upper actuel
		
			// Vérifier qu'au moins une barre dans le range a dépassé std1Upper + offset
			bool hasExceededVA = false;
			for (int i = 1; i <= VABreakoutBarsRangeUP3; i++)
			{
				if (Math.Max(High[i], Low[i]) > Values[1][i] + tickOffset)
				{
					hasExceededVA = true;
					break;
				}
			}
		
			if (!hasExceededVA)
				return false;
		
			// Vérifier qu'au moins une barre dans le range entre dans la VA
			bool hasEnteredVA = false;
			for (int i = 0; i <= VABreakoutBarsRangeUP3; i++)
			{
				if (Math.Min(High[i], Low[i]) <= std1Upper)
				{
					hasEnteredVA = true;
					break;
				}
			}
		
			// Vérifier que le prix actuel est au-dessus de la VA
			bool isAboveVA = Close[0] > std1Upper;
		
			return hasExceededVA && hasEnteredVA && isAboveVA;
		}
		
		private bool CheckVABreakoutReversalDOWN3()
		{
			if (!EnableVABreakoutReversalDOWN3 || CurrentBar < VABreakoutBarsRangeDOWN3)
				return true;
		
			double tickOffset = VABreakoutOffsetTicks3 * TickSize;
			double std1Lower = Values[2][0]; // StdDev1Lower actuel
		
			// Vérifier qu'au moins une barre dans le range a dépassé std1Lower - offset
			bool hasExceededVA = false;
			for (int i = 1; i <= VABreakoutBarsRangeDOWN3; i++)
			{
				if (Math.Min(High[i], Low[i]) < Values[2][i] - tickOffset)
				{
					hasExceededVA = true;
					break;
				}
			}
		
			if (!hasExceededVA)
				return false;
		
			// Vérifier qu'au moins une barre dans le range entre dans la VA
			bool hasEnteredVA = false;
			for (int i = 0; i <= VABreakoutBarsRangeDOWN3; i++)
			{
				if (Math.Max(High[i], Low[i]) >= std1Lower)
				{
					hasEnteredVA = true;
					break;
				}
			}
		
			// Vérifier que le prix actuel est en-dessous de la VA
			bool isBelowVA = Close[0] < std1Lower;
		
			return hasExceededVA && hasEnteredVA && isBelowVA;
		}
		
		//
		private bool CheckVABreakoutReversalUP4()
		{
			if (!EnableVABreakoutReversalUP4 || CurrentBar < VABreakoutBarsRangeUP4)
				return true;
		
			double tickOffset = VABreakoutOffsetTicks4 * TickSize;
			double std1Upper = Values[1][0]; // StdDev1Upper actuel
		
			// Vérifier que les barres précédentes ont clôturé au-dessus de std1Upper + offset
			for (int i = 1; i <= VABreakoutBarsRangeUP4; i++)
			{
				if (Close[i] <= Values[1][i] + tickOffset)
					return false;
			}
		
			// Vérifier les conditions pour la barre actuelle
			bool lowEntersVA = Low[0] <= std1Upper;
			bool closesAboveVA = Close[0] > std1Upper;
		
			return lowEntersVA && closesAboveVA;
		}
		
		private bool CheckVABreakoutReversalDOWN4()
		{
			if (!EnableVABreakoutReversalDOWN4 || CurrentBar < VABreakoutBarsRangeDOWN4)
				return true;
		
			double tickOffset = VABreakoutOffsetTicks4 * TickSize;
			double std1Lower = Values[2][0]; // StdDev1Lower actuel
		
			// Vérifier que les barres précédentes ont clôturé en-dessous de std1Lower - offset
			for (int i = 1; i <= VABreakoutBarsRangeDOWN4; i++)
			{
				if (Close[i] >= Values[2][i] - tickOffset)
					return false;
			}
		
			// Vérifier les conditions pour la barre actuelle
			bool highEntersVA = High[0] >= std1Lower;
			bool closesBelowVA = Close[0] < std1Lower;
		
			return highEntersVA && closesBelowVA;
		}
		
		// Méthodes de vérification pour VA Breakout Reversal 5
		private bool CheckVABreakoutReversalUP5()
		{
			if (!EnableVABreakoutReversalUP5 || CurrentBar < VABreakoutBarsRangeUP5)
				return true;
		
			double tickOffset = VABreakoutOffsetTicks5 * TickSize;
			double std1Upper = Values[1][0]; // StdDev1Upper actuel
		
			// Vérifier que les barres précédentes ont clôturé au-dessus de std1Upper + offset
			for (int i = 1; i <= VABreakoutBarsRangeUP5; i++)
			{
				if (Close[i] <= Values[1][i] + tickOffset)
					return false;
			}
		
			// Vérifier les conditions pour la barre actuelle
			bool lowEntersVA = UsePreviousBarUP5 ? Low[1] <= std1Upper : Low[0] <= std1Upper;
			bool closesAboveVA = Close[0] > std1Upper;
		
			return lowEntersVA && closesAboveVA;
		}
		
		private bool CheckVABreakoutReversalDOWN5()
		{
			if (!EnableVABreakoutReversalDOWN5 || CurrentBar < VABreakoutBarsRangeDOWN5)
				return true;
		
			double tickOffset = VABreakoutOffsetTicks5 * TickSize;
			double std1Lower = Values[2][0]; // StdDev1Lower actuel
		
			// Vérifier que les barres précédentes ont clôturé en-dessous de std1Lower - offset
			for (int i = 1; i <= VABreakoutBarsRangeDOWN5; i++)
			{
				if (Close[i] >= Values[2][i] - tickOffset)
					return false;
			}
		
			// Vérifier les conditions pour la barre actuelle
			bool highEntersVA = UsePreviousBarDOWN5 ? High[1] >= std1Lower : High[0] >= std1Lower;
			bool closesBelowVA = Close[0] < std1Lower;
		
			return highEntersVA && closesBelowVA;
		}


		// ########################################################################
		private bool ShouldDrawUpArrow()
        {
            // Calculate the distance from VWAP
            double vwap = Values[0][0];
            double distanceInTicks = (Close[0] - vwap) / TickSize;
			bool withinSignalTime;
			if (SignalTimingMode == SignalTimeMode.Minutes)
			{
				TimeSpan timeSinceReset = Time[0] - lastResetTime;
				withinSignalTime = timeSinceReset.TotalMinutes >= MinMinutesForSignal && timeSinceReset.TotalMinutes <= MaxMinutesForSignal;
			}
			else
			{
				withinSignalTime = barsSinceReset >= MinBarsForSignal && barsSinceReset <= MaxBarsForSignal;
			}

            bool bvaCondition = (Close[0] > Open[0]) &&
                   (!OKisADX || (ADX1[0] > FminADX && ADX1[0] < FmaxADX)) &&
                   (!OKisATR || (ATR1[0] > FminATR && ATR1[0] < FmaxATR)) &&
                   (!OKisVOL || (VOL1[0] > VOLMA1[0])) &&
                   (!OKisAfterBarsSinceResetUP || withinSignalTime) &&
                   (!OKisAboveUpperThreshold || Close[0] > (Values[1][0] + MinEntryDistanceUP * TickSize)) &&
                   (!OKisWithinMaxEntryDistance || Close[0] <= (Values[1][0] + MaxEntryDistanceUP * TickSize)) &&
                   (!OKisUpperBreakoutCountExceeded || upperBreakoutCount < MaxUpperBreakouts) &&
                   (!EnableDistanceFromVWAPCondition || (distanceInTicks >= MinDistanceFromVWAP && distanceInTicks <= MaxDistanceFromVWAP)) &&
				   (!useOpenForVAConditionUP || (Open[0] > Values[2][0] && Open[0] < Values[1][0])) &&
				   (!useLowForVAConditionUP || (Low[0] > Values[2][0] && Low[0] < Values[1][0]));

            double openCloseDiff = Math.Abs(Open[0] - Close[0]) / TickSize;
            double highLowDiff = Math.Abs(High[0] - Low[0]) / TickSize;
            bool limusineCondition = (ShowLimusineOpenCloseUP && openCloseDiff >= MinimumTicks && openCloseDiff <= MaximumTicks && Close[0] > Open[0]) ||
                                    (ShowLimusineHighLowUP && highLowDiff >= MinimumTicks && highLowDiff <= MaximumTicks && Close[0] > Open[0]);
									
            // New Slope Condition for UP Arrows
            if (EnableSlopeFilterUP)
            {
                if (CurrentBar < SlopeBarsCountUP)
                    return false; // Not enough bars to calculate slope

                double oldValue = Values[5][SlopeBarsCountUP - 1]; // StdDev3Upper value SlopeBarsCountUP bars ago
                double newValue = Values[5][0]; // Current StdDev3Upper value

                double slopePerBar = (newValue - oldValue) / SlopeBarsCountUP;

                if (slopePerBar < MinSlopeValueUP)
                    return false; // The slope is not steep enough upwards
            }
			// Nouvelle logique modifiée pour le delta cumulatif UP
            // bool cumulativeDeltaSessionCondition = true;
            // if (EnableUPlimDeltaSession)
            // {
                // double deltaDifference = Math.Abs(cumulativeDelta.DeltaClose[0] - cumulativeDelta.DeltaOpen[0]);
                // bool isUpwardMovement = cumulativeDelta.DeltaClose[0] > cumulativeDelta.DeltaOpen[0];
                // cumulativeDeltaSessionCondition = deltaDifference >= MinVdeltaCsession && 
                                                // deltaDifference <= MaxVdeltaCsession &&
                                                // isUpwardMovement;
            // }

            // New condition for STD3 Upper at its highest
            bool std3Condition = !EnableSTD3HighLowTracking || Values[5][0] >= highestSTD3Upper;
			bool cumulativeDeltaCondition = CheckCumulativeDeltaConditionUP();
			bool barDeltaCondition = CheckBarDeltaConditionUP();
			bool deltaPercentCondition = CheckDeltaPercentConditionUP();
			bool maxMinDeltaCondition = CheckMaxMinDeltaConditionUP();
			bool maxVolumeCondition = CheckMaxVolumeCondition(true);
			bool maxVolumeRangeCondition = CheckMaxVolumeRangeCondition(true);
			bool pocCondition = CheckPOCCondition(true);
			bool rangeBreakoutCondition = !EnablePreviousSessionRangeBreakout || (previousSessionHighStd1Upper != double.MinValue && Close[0] > previousSessionHighStd1Upper);
			bool std3DistanceCondition = CheckSTD3DistanceCondition(true);
			bool pocVolumeCondition = CheckPOCVolumeCondition(true);
			// return bvaCondition && limusineCondition && std3Condition && cumulativeDeltaCondition && barDeltaCondition && deltaPercentCondition && maxMinDeltaCondition && maxVolumeCondition && maxVolumeRangeCondition && pocCondition && rangeBreakoutCondition && std3DistanceCondition && pocVolumeCondition && cumulativeDeltaSessionCondition;
			bool vaBreakoutReversalCondition = CheckVABreakoutReversalUP();
			bool valueAreaCondition = true;
			bool sellersCondition = CheckSellersForBuyCondition();
			bool vaBreakoutReversalCondition2 = CheckVABreakoutReversalUP2();
			bool vaBreakoutReversalCondition3 = CheckVABreakoutReversalUP3();
			bool vaBreakoutReversalCondition4 = CheckVABreakoutReversalUP4();
			bool vaBreakoutReversalCondition5 = CheckVABreakoutReversalUP5();
			double upperThreshold, lowerThreshold;
			
			switch (SelectedValueArea)
			{
				case ValueAreaMode.STD1:
					upperThreshold = Values[1][0]; // StdDev1 Upper
					lowerThreshold = Values[2][0]; // StdDev1 Lower
					valueAreaCondition = (!OKisAboveUpperThreshold || Close[0] > (upperThreshold + MinEntryDistanceUP * TickSize)) &&
									(!OKisWithinMaxEntryDistance || Close[0] <= (upperThreshold + MaxEntryDistanceUP * TickSize)) &&
									(!useOpenForVAConditionUP || (Open[0] > lowerThreshold && Open[0] < upperThreshold)) &&
									(!useLowForVAConditionUP || (Low[0] > lowerThreshold && Low[0] < upperThreshold));
					break;
					
				case ValueAreaMode.STD2:
					upperThreshold = Values[3][0]; // StdDev2 Upper
					lowerThreshold = Values[4][0]; // StdDev2 Lower
					valueAreaCondition = (!OKisAboveUpperThreshold || Close[0] > (upperThreshold + MinEntryDistanceUP * TickSize)) &&
									(!OKisWithinMaxEntryDistance || Close[0] <= (upperThreshold + MaxEntryDistanceUP * TickSize)) &&
									(!useOpenForVAConditionUP || (Open[0] > lowerThreshold && Open[0] < upperThreshold)) &&
									(!useLowForVAConditionUP || (Low[0] > lowerThreshold && Low[0] < upperThreshold));
					break;
					
				case ValueAreaMode.Both:
					bool std1Condition = (!OKisAboveUpperThreshold || Close[0] > (Values[1][0] + MinEntryDistanceUP * TickSize)) &&
									(!OKisWithinMaxEntryDistance || Close[0] <= (Values[1][0] + MaxEntryDistanceUP * TickSize)) &&
									(!useOpenForVAConditionUP || (Open[0] > Values[2][0] && Open[0] < Values[1][0])) &&
									(!useLowForVAConditionUP || (Low[0] > Values[2][0] && Low[0] < Values[1][0]));
									
					bool std2Condition = (!OKisAboveUpperThreshold || Close[0] > (Values[3][0] + MinEntryDistanceUP * TickSize)) &&
									(!OKisWithinMaxEntryDistance || Close[0] <= (Values[3][0] + MaxEntryDistanceUP * TickSize)) &&
									(!useOpenForVAConditionUP || (Open[0] > Values[4][0] && Open[0] < Values[3][0])) &&
									(!useLowForVAConditionUP || (Low[0] > Values[4][0] && Low[0] < Values[3][0]));
									
					valueAreaCondition = std1Condition && std2Condition;  // Les deux conditions doivent être satisfaites
					break;
			}
			return bvaCondition && limusineCondition && std3Condition && 
					valueAreaCondition && vaBreakoutReversalCondition && // Ajoutez ici
					cumulativeDeltaCondition && barDeltaCondition && deltaPercentCondition && 
					maxMinDeltaCondition && maxVolumeCondition && maxVolumeRangeCondition && 
					pocCondition && rangeBreakoutCondition && std3DistanceCondition && 
					pocVolumeCondition && vaBreakoutReversalCondition2 && sellersCondition && vaBreakoutReversalCondition3 && vaBreakoutReversalCondition4 && vaBreakoutReversalCondition5;
        }
		
		private bool ShouldDrawDownArrow()
        {
			bool withinSignalTime;
			if (SignalTimingMode == SignalTimeMode.Minutes)
			{
				TimeSpan timeSinceReset = Time[0] - lastResetTime;
				withinSignalTime = timeSinceReset.TotalMinutes >= MinMinutesForSignal && timeSinceReset.TotalMinutes <= MaxMinutesForSignal;
			}
			else
			{
				withinSignalTime = barsSinceReset >= MinBarsForSignal && barsSinceReset <= MaxBarsForSignal;
			}
            // Calculate the distance from VWAP
            double vwap = Values[0][0];
            double distanceInTicks = (vwap - Close[0]) / TickSize;

            bool bvaCondition = (Close[0] < Open[0]) &&
                   (!OKisADX || (ADX1[0] > FminADX && ADX1[0] < FmaxADX)) &&
                   (!OKisATR || (ATR1[0] > FminATR && ATR1[0] < FmaxATR)) &&
                   (!OKisVOL || (VOL1[0] > VOLMA1[0])) &&
                   (!OKisAfterBarsSinceResetDown || withinSignalTime) &&
                   (!OKisBelovLowerThreshold || Close[0] < (Values[2][0] - MinEntryDistanceDOWN * TickSize)) &&
                   (!OKisWithinMaxEntryDistanceDown || Close[0] >= (Values[2][0] - MaxEntryDistanceDOWN * TickSize)) &&
                   (!OKisLowerBreakoutCountExceeded || lowerBreakoutCount < MaxLowerBreakouts) &&
                   (!EnableDistanceFromVWAPCondition || (distanceInTicks >= MinDistanceFromVWAP && distanceInTicks <= MaxDistanceFromVWAP)) &&
				   (!useOpenForVAConditionDown || (Open[0] > Values[2][0] && Open[0] < Values[1][0])) &&
				   (!useHighForVAConditionDown || (High[0] > Values[2][0] && High[0] < Values[1][0]));

            double openCloseDiff = Math.Abs(Open[0] - Close[0]) / TickSize;
            double highLowDiff = Math.Abs(High[0] - Low[0]) / TickSize;
            bool limusineCondition = (ShowLimusineOpenCloseDOWN && openCloseDiff >= MinimumTicks && openCloseDiff <= MaximumTicks && Close[0] < Open[0]) ||
                                    (ShowLimusineHighLowDOWN && highLowDiff >= MinimumTicks && highLowDiff <= MaximumTicks && Close[0] < Open[0]);

            // New Slope Condition for DOWN Arrows
            if (EnableSlopeFilterDOWN)
            {
                if (CurrentBar < SlopeBarsCountDOWN)
                    return false; // Not enough bars to calculate slope

                double oldValue = Values[6][SlopeBarsCountDOWN - 1]; // StdDev3Lower value SlopeBarsCountDOWN bars ago
                double newValue = Values[6][0]; // Current StdDev3Lower value

                double slopePerBar = (newValue - oldValue) / SlopeBarsCountDOWN;

                if (slopePerBar > -MinSlopeValueDOWN)
                    return false; // The slope is not steep enough downwards
            }
			// Nouvelle logique modifiée pour le delta cumulatif DOWN
            // bool cumulativeDeltaSessionCondition = true;
            // if (EnableDownLimDeltaSession)
            // {
                // double deltaDifference = Math.Abs(cumulativeDelta.DeltaClose[0] - cumulativeDelta.DeltaOpen[0]);
                // bool isDownwardMovement = cumulativeDelta.DeltaClose[0] < cumulativeDelta.DeltaOpen[0];
                // cumulativeDeltaSessionCondition = deltaDifference >= MinVdeltaCsession && 
                                                // deltaDifference <= MaxVdeltaCsession &&
                                                // isDownwardMovement;
            // }

            // New condition for STD3 Lower at its lowest
            bool std3Condition = !EnableSTD3HighLowTracking || Values[6][0] <= lowestSTD3Lower;
			bool cumulativeDeltaCondition = CheckCumulativeDeltaConditionDOWN();
			bool barDeltaCondition = CheckBarDeltaConditionDOWN();
			bool deltaPercentCondition = CheckDeltaPercentConditionDOWN();
			bool maxMinDeltaCondition = CheckMaxMinDeltaConditionDOWN();
			bool maxVolumeCondition = CheckMaxVolumeCondition(false);
			bool maxVolumeRangeCondition = CheckMaxVolumeRangeCondition(false);
			bool pocCondition = CheckPOCCondition(false);
			bool rangeBreakoutCondition = !EnablePreviousSessionRangeBreakout || (previousSessionLowStd1Lower != double.MaxValue && Close[0] < previousSessionLowStd1Lower);
			bool std3DistanceCondition = CheckSTD3DistanceCondition(false);
			bool pocVolumeCondition = CheckPOCVolumeCondition(false);
			// return bvaCondition && limusineCondition && std3Condition && cumulativeDeltaCondition && barDeltaCondition && deltaPercentCondition && maxMinDeltaCondition && maxVolumeCondition && maxVolumeRangeCondition && pocCondition && rangeBreakoutCondition && std3DistanceCondition && pocVolumeCondition && cumulativeDeltaSessionCondition;
			bool vaBreakoutReversalCondition = CheckVABreakoutReversalDOWN();
			bool valueAreaCondition = true;
			bool buyersCondition = CheckBuyersForSellCondition();
			bool vaBreakoutReversalCondition2 = CheckVABreakoutReversalDOWN2();
			bool vaBreakoutReversalCondition3 = CheckVABreakoutReversalDOWN3();
			bool vaBreakoutReversalCondition4 = CheckVABreakoutReversalDOWN4();
			bool vaBreakoutReversalCondition5 = CheckVABreakoutReversalDOWN5();
			double upperThreshold, lowerThreshold;
			
			switch (SelectedValueArea)
			{
				case ValueAreaMode.STD1:
					upperThreshold = Values[1][0]; // StdDev1 Upper
					lowerThreshold = Values[2][0]; // StdDev1 Lower
					valueAreaCondition = (!OKisBelovLowerThreshold || Close[0] < (lowerThreshold - MinEntryDistanceDOWN * TickSize)) &&
									(!OKisWithinMaxEntryDistanceDown || Close[0] >= (lowerThreshold - MaxEntryDistanceDOWN * TickSize)) &&
									(!useOpenForVAConditionDown || (Open[0] > lowerThreshold && Open[0] < upperThreshold)) &&
									(!useHighForVAConditionDown || (High[0] > lowerThreshold && High[0] < upperThreshold));
					break;
					
				case ValueAreaMode.STD2:
					upperThreshold = Values[3][0]; // StdDev2 Upper
					lowerThreshold = Values[4][0]; // StdDev2 Lower
					valueAreaCondition = (!OKisBelovLowerThreshold || Close[0] < (lowerThreshold - MinEntryDistanceDOWN * TickSize)) &&
									(!OKisWithinMaxEntryDistanceDown || Close[0] >= (lowerThreshold - MaxEntryDistanceDOWN * TickSize)) &&
									(!useOpenForVAConditionDown || (Open[0] > lowerThreshold && Open[0] < upperThreshold)) &&
									(!useHighForVAConditionDown || (High[0] > lowerThreshold && High[0] < upperThreshold));
					break;
					
				case ValueAreaMode.Both:
					bool std1Condition = (!OKisBelovLowerThreshold || Close[0] < (Values[2][0] - MinEntryDistanceDOWN * TickSize)) &&
									(!OKisWithinMaxEntryDistanceDown || Close[0] >= (Values[2][0] - MaxEntryDistanceDOWN * TickSize)) &&
									(!useOpenForVAConditionDown || (Open[0] > Values[2][0] && Open[0] < Values[1][0])) &&
									(!useHighForVAConditionDown || (High[0] > Values[2][0] && High[0] < Values[1][0]));
									
					bool std2Condition = (!OKisBelovLowerThreshold || Close[0] < (Values[4][0] - MinEntryDistanceDOWN * TickSize)) &&
									(!OKisWithinMaxEntryDistanceDown || Close[0] >= (Values[4][0] - MaxEntryDistanceDOWN * TickSize)) &&
									(!useOpenForVAConditionDown || (Open[0] > Values[4][0] && Open[0] < Values[3][0])) &&
									(!useHighForVAConditionDown || (High[0] > Values[4][0] && High[0] < Values[3][0]));
									
					valueAreaCondition = std1Condition && std2Condition;  // Les deux conditions doivent être satisfaites
					break;
			}
			
			return bvaCondition && limusineCondition && std3Condition && 
					valueAreaCondition && vaBreakoutReversalCondition && // Ajoutez ici
					cumulativeDeltaCondition && barDeltaCondition && deltaPercentCondition && 
					maxMinDeltaCondition && maxVolumeCondition && maxVolumeRangeCondition && 
					pocCondition && rangeBreakoutCondition && std3DistanceCondition && 
					pocVolumeCondition && vaBreakoutReversalCondition2 && buyersCondition && vaBreakoutReversalCondition3 && vaBreakoutReversalCondition4 && vaBreakoutReversalCondition5;
        }
		
		private void ResetSessionValues()
		{
			highestStd1Upper = double.MinValue;
			lowestStd1Lower = double.MaxValue;
		}
		
		private void AdjustPreviousSessionRange()
		{
			if (!EnableRangeSizeFilter || !EnablePreviousSessionRangeBreakout)
				return;
		
			double rangeSize = (previousSessionHighStd1Upper - previousSessionLowStd1Lower) / TickSize;
		
			if (rangeSize < MinRangeSize)
			{
				// Augmenter le range
				previousSessionHighStd1Upper += RangeAdjustment * TickSize;
				previousSessionLowStd1Lower -= RangeAdjustment * TickSize;
			}
			else if (rangeSize > MaxRangeSize)
			{
				// Diminuer le range
				previousSessionHighStd1Upper -= RangeAdjustment * TickSize;
				previousSessionLowStd1Lower += RangeAdjustment * TickSize;
			}
		}
		
		private bool CheckVolumetricConditions(bool isUpDirection)
        {
            if (!(Bars.BarsSeries.BarsType is NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType))
                return false;

            var currentBarVolumes = barsType.Volumes[CurrentBar];
            var previousBarVolumes = barsType.Volumes[CurrentBar - 1];

            double[] volumetricValues = new double[7];
            volumetricValues[0] = currentBarVolumes.BarDelta;
            volumetricValues[1] = currentBarVolumes.GetDeltaPercent();
            volumetricValues[2] = volumetricValues[0] - previousBarVolumes.BarDelta;
            volumetricValues[3] = currentBarVolumes.TotalBuyingVolume;
            volumetricValues[4] = currentBarVolumes.TotalSellingVolume;
            volumetricValues[5] = currentBarVolumes.Trades;
            volumetricValues[6] = currentBarVolumes.TotalVolume;

            VolumetricParameters[] parameters = isUpDirection ? upParameters : downParameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Enabled)
                {
                    if (isUpDirection)
                    {
                        switch (i)
                        {
                            case 0: // BarDelta
                            case 1: // DeltaPercent
                            case 2: // DeltaChange
                                if (volumetricValues[i] < parameters[i].Min || volumetricValues[i] > parameters[i].Max)
                                    return false;
                                break;
                            default:
                                if (volumetricValues[i] < parameters[i].Min || volumetricValues[i] > parameters[i].Max)
                                    return false;
                                break;
                        }
                    }
                    else // Down direction
                    {
                        switch (i)
                        {
                            case 0: // BarDelta
                            case 1: // DeltaPercent
                            case 2: // DeltaChange
                                if (volumetricValues[i] > -parameters[i].Min || volumetricValues[i] < -parameters[i].Max)
                                    return false;
                                break;
                            default:
                                if (volumetricValues[i] < parameters[i].Min || volumetricValues[i] > parameters[i].Max)
                                    return false;
                                break;
                        }
                    }
                }
            }
            return true;
        }
		
		private void SetDynamicStopsAndTargets(bool isLong)
        {
            double vwap = Values[0][0];
            double stdDev1Upper = Values[1][0];
            double stdDev1Lower = Values[2][0];

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

            ptDistance = Math.Max(1, Math.Round(Math.Abs(ptDistance)));
            slDistance = Math.Max(1, Math.Round(Math.Abs(slDistance)));

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
			else if (UsePOCBasedStopLoss)
			{
				SetPOCBasedStopLoss(isLong);
				SetProfitTarget(CalculationMode.Ticks, Pt);
			}
			else
			{
				SetStopLoss(CalculationMode.Ticks, Sl);
				SetProfitTarget(CalculationMode.Ticks, Pt);
			}
		}
		
		private void SetPOCBasedStopLoss(bool isLong)
		{
			if (!(Bars.BarsSeries.BarsType is NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType))
				return;
		
			var currentBarVolumes = barsType.Volumes[CurrentBar];
			double pocPrice;
			long maxVolume = currentBarVolumes.GetMaximumVolume(null, out pocPrice);
		
			double stopLossPrice;
			if (isLong)
			{
				stopLossPrice = pocPrice - (POCStopLossOffset * TickSize);
			}
			else
			{
				stopLossPrice = pocPrice + (POCStopLossOffset * TickSize);
			}
		
			SetStopLoss(CalculationMode.Price, stopLossPrice);
		}

        private void SetStopsBasedOnBarSize(bool isLong)
        {
            // Calculer la taille de la barre actuelle en ticks
            double barSizeTicks = (High[0] - Low[0]) / TickSize;
            barSizeTicks = Math.Max(1, Math.Round(Math.Abs(barSizeTicks)));

            // Définir le stop loss et le profit target en fonction de la taille de la barre
            SetStopLoss(CalculationMode.Ticks, barSizeTicks);
            SetProfitTarget(CalculationMode.Ticks, barSizeTicks);
        }
		
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
		
		private void ResetValues(DateTime resetTime)
        {
            sumPriceVolume = 0;
            sumVolume = 0;
            sumSquaredPriceVolume = 0;
            barsSinceReset = 0;
            upperBreakoutCount = 0;
            lowerBreakoutCount = 0;
            lastResetTime = resetTime;
            // Reset STD3 high/low tracking
            isFirstBarSinceReset = true;
            highestSTD3Upper = double.MinValue;
            lowestSTD3Lower = double.MaxValue;
			// Reset session values
			highestStd1Upper = double.MinValue;
			lowestStd1Lower = double.MaxValue;
        }
		
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
		
		[NinjaScriptProperty]
        [Display(Name = "Enable Dynamic Stops", Order = 4, GroupName = "0.02_Entry_Parameters")]
        public bool EnableDynamicStops { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Bar Size For Stops", Order = 5, GroupName = "0.02_Entry_Parameters")]
        public bool UseBarSizeForStops { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Parabolic Stop", Order = 6, GroupName = "0.02_Entry_Parameters")]
        public bool UseParabolicStop { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name="Enable Break Even", Description="Activate Break Even feature", Order=7, GroupName="0.02_Entry_Parameters")]
        public bool EnableBreakEven { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Break Even Ticks", Description="Number of ticks in profit before activating Break Even", Order=8, GroupName="0.02_Entry_Parameters")]
        public int BreakEvenTicks { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Use POC-based Stop Loss", Description="Use Point of Control for Stop Loss", Order=9, GroupName="0.02_Entry_Parameters")]
		public bool UsePOCBasedStopLoss { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="POC Stop Loss Offset", Description="Number of ticks to add to POC for Stop Loss", Order=10, GroupName="0.02_Entry_Parameters")]
		public int POCStopLossOffset { get; set; }
		
		// ###################################################### //
		
		 // Propriétés BVA
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Reset Period (Minutes)", Order = 1, GroupName = "0.1_BVA Parameters")]
        public int ResetPeriod { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Signal Time Mode", Description="Choose between Bars or Minutes for signal timing", Order=2, GroupName="0.1_BVA Parameters")]
		public SignalTimeMode SignalTimingMode { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Min Bars for Signal", Order=3, GroupName="0.1_BVA Parameters")]
		public int MinBarsForSignal { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Max Bars for Signal", Order=4, GroupName="0.1_BVA Parameters")]
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
		
		[NinjaScriptProperty]
        [Display(Name="use Open in VA Condition UP", Order=7, GroupName="0.1_BVA Parameters")]
        public bool useOpenForVAConditionUP { get; set; }

        [NinjaScriptProperty]
        [Display(Name="use Open in VA Condition Down", Order=8, GroupName="0.1_BVA Parameters")]
        public bool useOpenForVAConditionDown { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name="use Low in VA Condition UP", Order=9, GroupName="0.1_BVA Parameters")]
        public bool useLowForVAConditionUP { get; set; }

        [NinjaScriptProperty]
        [Display(Name="use High in VA Condition Down", Order=10, GroupName="0.1_BVA Parameters")]
        public bool useHighForVAConditionDown { get; set; }
		
		// Propriétés Limusine
        [NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Minimum Ticks", Description = "Nombre minimum de ticks pour une limusine", Order = 1, GroupName = "0.2_Limusine Parameters")]
		public int MinimumTicks { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Maximum Ticks", Description = "Nombre maximum de ticks pour une limusine", Order = 2, GroupName = "0.2_Limusine Parameters")]
		public int MaximumTicks { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Afficher Limusine Open-Close UP", Description = "Afficher les limusines Open-Close UP", Order = 3, GroupName = "0.2_Limusine Parameters")]
		public bool ShowLimusineOpenCloseUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Afficher Limusine Open-Close DOWN", Description = "Afficher les limusines Open-Close DOWN", Order = 4, GroupName = "0.2_Limusine Parameters")]
		public bool ShowLimusineOpenCloseDOWN { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Afficher Limusine High-Low UP", Description = "Afficher les limusines High-Low UP", Order = 5, GroupName = "0.2_Limusine Parameters")]
		public bool ShowLimusineHighLowUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Afficher Limusine High-Low DOWN", Description = "Afficher les limusines High-Low DOWN", Order = 6, GroupName = "0.2_Limusine Parameters")]
		public bool ShowLimusineHighLowDOWN { get; set; }

        // ############ Buy #############
		// Buy
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
		[Display(Name = "OKisAfterBarsSinceResetUP", Description = "Check Bars Since Reset UP", Order = 1, GroupName = "0.3_Buy")]
		public bool OKisAfterBarsSinceResetUP { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, 1)]
		[Display(Name = "OKisAboveUpperThreshold", Description = "Check Above Upper Threshold", Order = 1, GroupName = "0.3_Buy")]
		public bool OKisAboveUpperThreshold { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, 1)]
		[Display(Name = "OKisWithinMaxEntryDistance", Description = "Check Within Max Entry Distance", Order = 1, GroupName = "0.3_Buy")]
		public bool OKisWithinMaxEntryDistance { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, 1)]
		[Display(Name = "OKisUpperBreakoutCountExceeded", Description = "Check Upper Breakout Count Exceeded", Order = 1, GroupName = "0.3_Buy")]
		public bool OKisUpperBreakoutCountExceeded { get; set; }
		
		// ############ Sell #############
		// Sell
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
		[Display(Name = "OKisAfterBarsSinceResetDown", Description = "Check Bars Since Reset Down", Order = 1, GroupName = "0.4_Sell")]
		public bool OKisAfterBarsSinceResetDown { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, 1)]
		[Display(Name = "OKisBelovLowerThreshold", Description = "Check Below Lower Threshold", Order = 1, GroupName = "0.4_Sell")]
		public bool OKisBelovLowerThreshold { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, 1)]
		[Display(Name = "OKisWithinMaxEntryDistanceDown", Description = "Check Within Max Entry Distance Down", Order = 1, GroupName = "0.4_Sell")]
		public bool OKisWithinMaxEntryDistanceDown { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, 1)]
		[Display(Name = "OKisLowerBreakoutCountExceeded", Description = "Check Lower Breakout Count Exceeded", Order = 1, GroupName = "0.4_Sell")]
		public bool OKisLowerBreakoutCountExceeded { get; set; }
		
		// New Parameters for Slope Filter (updated)
        [Display(Name = "Enable Slope Filter UP", Order = 1, GroupName = "0.5_Slope Filter UP")]
        public bool EnableSlopeFilterUP { get; set; }

        [Display(Name = "Minimum Slope Value UP", Order = 2, GroupName = "0.5_Slope Filter UP")]
        public double MinSlopeValueUP { get; set; }

        [Display(Name = "Slope Bars Count UP", Order = 3, GroupName = "0.5_Slope Filter UP")]
        public int SlopeBarsCountUP { get; set; }

        [Display(Name = "Enable Slope Filter DOWN", Order = 1, GroupName = "0.6_Slope Filter DOWN")]
        public bool EnableSlopeFilterDOWN { get; set; }

        [Display(Name = "Minimum Slope Value DOWN", Order = 2, GroupName = "0.6_Slope Filter DOWN")]
        public double MinSlopeValueDOWN { get; set; }

        [Display(Name = "Slope Bars Count DOWN", Order = 3, GroupName = "0.6_Slope Filter DOWN")]
        public int SlopeBarsCountDOWN { get; set; }
		
		// Distance VWAP
		[NinjaScriptProperty]
		[Display(Name = "Enable Distance From VWAP Condition", Order = 1, GroupName = "0.7_Distance_VWAP")]
		public bool EnableDistanceFromVWAPCondition { get; set; }
		
		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Minimum Distance From VWAP (Ticks)", Order = 2, GroupName = "0.7_Distance_VWAP")]
		public int MinDistanceFromVWAP { get; set; }
		
		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Maximum Distance From VWAP (Ticks)", Order = 3, GroupName = "0.7_Distance_VWAP")]
		public int MaxDistanceFromVWAP { get; set; }
		
		// New cumulative delta session properties under group 0.8_Limusine CdeltaSession
        // [NinjaScriptProperty]
        // [Display(Name = "Enable UP Limusine Delta Session", Description = "Enable cumulative delta session condition for UP signals", Order = 1, GroupName = "0.8_Limusine CdeltaSession")]
        // public bool EnableUPlimDeltaSession { get; set; }

        // [NinjaScriptProperty]
        // [Display(Name = "Enable DOWN Limusine Delta Session", Description = "Enable cumulative delta session condition for DOWN signals", Order = 2, GroupName = "0.8_Limusine CdeltaSession")]
        // public bool EnableDownLimDeltaSession { get; set; }

        // [NinjaScriptProperty]
        // [Range(0, int.MaxValue)]
        // [Display(Name = "Min Vdelta Csession", Description = "Minimum cumulative delta session value", Order = 3, GroupName = "0.8_Limusine CdeltaSession")]
        // public int MinVdeltaCsession { get; set; }

        // [NinjaScriptProperty]
        // [Range(0, int.MaxValue)]
        // [Display(Name = "Max Vdelta Csession", Description = "Maximum cumulative delta session value", Order = 4, GroupName = "0.8_Limusine CdeltaSession")]
        // public int MaxVdeltaCsession { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name="Enable STD3 High/Low Tracking", Description="Track highest STD3 Upper and lowest STD3 Lower since last reset", Order=1000, GroupName="1.01_STD3 Tracking")]
        public bool EnableSTD3HighLowTracking { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable POC Condition", Description="Enable the Point of Control condition", Order=1, GroupName="1.02_POC Parameters")]
		public bool POCConditionEnabled
		{
			get { return pocConditionEnabled; }
			set { pocConditionEnabled = value; }
		}
		
		[NinjaScriptProperty]
		[Range(-10, 100)]
		[Display(Name="POC Ticks Distance", Description="Number of ticks for POC distance from close", Order=2, GroupName="1.02_POC Parameters")]
		public int POCTicksDistance
		{
			get { return pocTicksDistance; }
			set { pocTicksDistance = Math.Max(0, value); }
			// set { pocTicksDistance = value; }
			// set { pocTicksDistance = Math.Max(1, value); }
		}

		// ##################### Propriétés Volumetric Filter #################################
        // UP Parameters
        [NinjaScriptProperty]
        [Display(Name = "Bar Delta UP Enabled", Order = 1, GroupName = "2.01_BarDeltaUP")]
        public bool BarDeltaUPEnabled
        {
            get { return upParameters[0].Enabled; }
            set { upParameters[0].Enabled = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Min Bar Delta UP", Order = 2, GroupName = "2.01_BarDeltaUP")]
        public double MinBarDeltaUP
        {
            get { return upParameters[0].Min; }
            set { upParameters[0].Min = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Max Bar Delta UP", Order = 3, GroupName = "2.01_BarDeltaUP")]
        public double MaxBarDeltaUP
        {
            get { return upParameters[0].Max; }
            set { upParameters[0].Max = value; }
        }

        // Delta Percent UP
        [NinjaScriptProperty]
        [Display(Name = "Delta Percent UP Enabled", Order = 1, GroupName = "2.02_DeltaPercentUP")]
        public bool DeltaPercentUPEnabled
        {
            get { return upParameters[1].Enabled; }
            set { upParameters[1].Enabled = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Min Delta Percent UP", Order = 2, GroupName = "2.02_DeltaPercentUP")]
        public double MinDeltaPercentUP
        {
            get { return upParameters[1].Min; }
            set { upParameters[1].Min = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Max Delta Percent UP", Order = 3, GroupName = "2.02_DeltaPercentUP")]
        public double MaxDeltaPercentUP
        {
            get { return upParameters[1].Max; }
            set { upParameters[1].Max = value; }
        }

        // Delta Change UP
        [NinjaScriptProperty]
        [Display(Name = "Delta Change UP Enabled", Order = 1, GroupName = "2.03_DeltaChangeUP")]
        public bool DeltaChangeUPEnabled
        {
            get { return upParameters[2].Enabled; }
            set { upParameters[2].Enabled = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Min Delta Change UP", Order = 2, GroupName = "2.03_DeltaChangeUP")]
        public double MinDeltaChangeUP
        {
            get { return upParameters[2].Min; }
            set { upParameters[2].Min = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Max Delta Change UP", Order = 3, GroupName = "2.03_DeltaChangeUP")]
        public double MaxDeltaChangeUP
        {
            get { return upParameters[2].Max; }
            set { upParameters[2].Max = value; }
        }

        // Total Buying Volume UP
        [NinjaScriptProperty]
        [Display(Name = "Total Buying Volume UP Enabled", Order = 1, GroupName = "2.04_TotalBuyingVolumeUP")]
        public bool TotalBuyingVolumeUPEnabled
        {
            get { return upParameters[3].Enabled; }
            set { upParameters[3].Enabled = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Min Total Buying Volume UP", Order = 2, GroupName = "2.04_TotalBuyingVolumeUP")]
        public double MinTotalBuyingVolumeUP
        {
            get { return upParameters[3].Min; }
            set { upParameters[3].Min = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Max Total Buying Volume UP", Order = 3, GroupName = "2.04_TotalBuyingVolumeUP")]
        public double MaxTotalBuyingVolumeUP
        {
            get { return upParameters[3].Max; }
            set { upParameters[3].Max = value; }
        }

        // Total Selling Volume UP
        [NinjaScriptProperty]
        [Display(Name = "Total Selling Volume UP Enabled", Order = 1, GroupName = "2.05_TotalSellingVolumeUP")]
        public bool TotalSellingVolumeUPEnabled
        {
            get { return upParameters[4].Enabled; }
            set { upParameters[4].Enabled = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Min Total Selling Volume UP", Order = 2, GroupName = "2.05_TotalSellingVolumeUP")]
        public double MinTotalSellingVolumeUP
        {
            get { return upParameters[4].Min; }
            set { upParameters[4].Min = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Max Total Selling Volume UP", Order = 3, GroupName = "2.05_TotalSellingVolumeUP")]
        public double MaxTotalSellingVolumeUP
        {
            get { return upParameters[4].Max; }
            set { upParameters[4].Max = value; }
        }

        // Trades UP
        [NinjaScriptProperty]
        [Display(Name = "Trades UP Enabled", Order = 1, GroupName = "2.06_TradesUP")]
        public bool TradesUPEnabled
        {
            get { return upParameters[5].Enabled; }
            set { upParameters[5].Enabled = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Min Trades UP", Order = 2, GroupName = "2.06_TradesUP")]
        public double MinTradesUP
        {
            get { return upParameters[5].Min; }
            set { upParameters[5].Min = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Max Trades UP", Order = 3, GroupName = "2.06_TradesUP")]
        public double MaxTradesUP
        {
            get { return upParameters[5].Max; }
            set { upParameters[5].Max = value; }
        }

        // Total Volume UP
        [NinjaScriptProperty]
        [Display(Name = "Total Volume UP Enabled", Order = 1, GroupName = "2.07_TotalVolumeUP")]
        public bool TotalVolumeUPEnabled
        {
            get { return upParameters[6].Enabled; }
            set { upParameters[6].Enabled = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Min Total Volume UP", Order = 2, GroupName = "2.07_TotalVolumeUP")]
        public double MinTotalVolumeUP
        {
            get { return upParameters[6].Min; }
            set { upParameters[6].Min = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Max Total Volume UP", Order = 3, GroupName = "2.07_TotalVolumeUP")]
        public double MaxTotalVolumeUP
        {
            get { return upParameters[6].Max; }
            set { upParameters[6].Max = value; }
        }

        // DOWN Parameters
        // Bar Delta DOWN
        [NinjaScriptProperty]
        [Display(Name = "Bar Delta DOWN Enabled", Order = 1, GroupName = "3.01_BarDeltaDOWN")]
        public bool BarDeltaDOWNEnabled
        {
            get { return downParameters[0].Enabled; }
            set { downParameters[0].Enabled = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Min Bar Delta DOWN", Order = 2, GroupName = "3.01_BarDeltaDOWN")]
        public double MinBarDeltaDOWN
        {
            get { return downParameters[0].Min; }
            set { downParameters[0].Min = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Max Bar Delta DOWN", Order = 3, GroupName = "3.01_BarDeltaDOWN")]
        public double MaxBarDeltaDOWN
        {
            get { return downParameters[0].Max; }
            set { downParameters[0].Max = value; }
        }
        
        // Delta Percent DOWN
        [NinjaScriptProperty]
        [Display(Name = "Delta Percent DOWN Enabled", Order = 1, GroupName = "3.02_DeltaPercentDOWN")]
        public bool DeltaPercentDOWNEnabled
        {
            get { return downParameters[1].Enabled; }
            set { downParameters[1].Enabled = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Min Delta Percent DOWN", Order = 2, GroupName = "3.02_DeltaPercentDOWN")]
        public double MinDeltaPercentDOWN
        {
            get { return downParameters[1].Min; }
            set { downParameters[1].Min = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Max Delta Percent DOWN", Order = 3, GroupName = "3.02_DeltaPercentDOWN")]
        public double MaxDeltaPercentDOWN
        {
            get { return downParameters[1].Max; }
            set { downParameters[1].Max = value; }
        }
        
        // Delta Change DOWN
        [NinjaScriptProperty]
        [Display(Name = "Delta Change DOWN Enabled", Order = 1, GroupName = "3.03_DeltaChangeDOWN")]
        public bool DeltaChangeDOWNEnabled
        {
            get { return downParameters[2].Enabled; }
            set { downParameters[2].Enabled = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Min Delta Change DOWN", Order = 2, GroupName = "3.03_DeltaChangeDOWN")]
        public double MinDeltaChangeDOWN
        {
            get { return downParameters[2].Min; }
            set { downParameters[2].Min = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Max Delta Change DOWN", Order = 3, GroupName = "3.03_DeltaChangeDOWN")]
        public double MaxDeltaChangeDOWN
        {
            get { return downParameters[2].Max; }
            set { downParameters[2].Max = value; }
        }
        
        // Total Buying Volume DOWN
        [NinjaScriptProperty]
        [Display(Name = "Total Buying Volume DOWN Enabled", Order = 1, GroupName = "3.04_TotalBuyingVolumeDOWN")]
        public bool TotalBuyingVolumeDOWNEnabled
        {
            get { return downParameters[3].Enabled; }
            set { downParameters[3].Enabled = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Min Total Buying Volume DOWN", Order = 2, GroupName = "3.04_TotalBuyingVolumeDOWN")]
        public double MinTotalBuyingVolumeDOWN
        {
            get { return downParameters[3].Min; }
            set { downParameters[3].Min = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Max Total Buying Volume DOWN", Order = 3, GroupName = "3.04_TotalBuyingVolumeDOWN")]
        public double MaxTotalBuyingVolumeDOWN
        {
            get { return downParameters[3].Max; }
            set { downParameters[3].Max = value; }
        }
        
        // Total Selling Volume DOWN
        [NinjaScriptProperty]
        [Display(Name = "Total Selling Volume DOWN Enabled", Order = 1, GroupName = "3.05_TotalSellingVolumeDOWN")]
        public bool TotalSellingVolumeDOWNEnabled
        {
            get { return downParameters[4].Enabled; }
            set { downParameters[4].Enabled = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Min Total Selling Volume DOWN", Order = 2, GroupName = "3.05_TotalSellingVolumeDOWN")]
        public double MinTotalSellingVolumeDOWN
        {
            get { return downParameters[4].Min; }
            set { downParameters[4].Min = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Max Total Selling Volume DOWN", Order = 3, GroupName = "3.05_TotalSellingVolumeDOWN")]
        public double MaxTotalSellingVolumeDOWN
        {
            get { return downParameters[4].Max; }
            set { downParameters[4].Max = value; }
        }
        
        // Trades DOWN
        [NinjaScriptProperty]
        [Display(Name = "Trades DOWN Enabled", Order = 1, GroupName = "3.06_TradesDOWN")]
        public bool TradesDOWNEnabled
        {
            get { return downParameters[5].Enabled; }
            set { downParameters[5].Enabled = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Min Trades DOWN", Order = 2, GroupName = "3.06_TradesDOWN")]
        public double MinTradesDOWN
        {
            get { return downParameters[5].Min; }
            set { downParameters[5].Min = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Max Trades DOWN", Order = 3, GroupName = "3.06_TradesDOWN")]
        public double MaxTradesDOWN
        {
            get { return downParameters[5].Max; }
            set { downParameters[5].Max = value; }
        }
        
        // Total Volume DOWN
        [NinjaScriptProperty]
        [Display(Name = "Total Volume DOWN Enabled", Order = 1, GroupName = "3.07_TotalVolumeDOWN")]
        public bool TotalVolumeDOWNEnabled
        {
            get { return downParameters[6].Enabled; }
            set { downParameters[6].Enabled = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Min Total Volume DOWN", Order = 2, GroupName = "3.07_TotalVolumeDOWN")]
        public double MinTotalVolumeDOWN
        {
            get { return downParameters[6].Min; }
            set { downParameters[6].Min = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Max Total Volume DOWN", Order = 3, GroupName = "3.07_TotalVolumeDOWN")]
        public double MaxTotalVolumeDOWN
        {
            get { return downParameters[6].Max; }
            set { downParameters[6].Max = value; }
        }
		// ############# Cumulative Delta Condition ############### //
		[NinjaScriptProperty]
		[Display(Name="Enable Cumulative Delta Condition UP", Description="Enable the cumulative delta condition for up arrows", Order=1, GroupName="4.01_Cumulative Delta")]
		public bool EnableCumulativeDeltaConditionUP
		{
			get { return enableCumulativeDeltaConditionUP; }
			set { enableCumulativeDeltaConditionUP = value; }
		}
	
		[NinjaScriptProperty]
		[Display(Name="Enable Cumulative Delta Condition DOWN", Description="Enable the cumulative delta condition for down arrows", Order=2, GroupName="4.01_Cumulative Delta")]
		public bool EnableCumulativeDeltaConditionDOWN
		{
			get { return enableCumulativeDeltaConditionDOWN; }
			set { enableCumulativeDeltaConditionDOWN = value; }
		}
	
		[Range(2, 5)]
		[NinjaScriptProperty]
		[Display(Name="Cumulative Delta Bars Range UP", Description="Number of bars to check for up arrows (2-5)", Order=3, GroupName="4.01_Cumulative Delta")]
		public int CumulativeDeltaBarsRangeUP
		{
			get { return cumulativeDeltaBarsRangeUP; }
			set { cumulativeDeltaBarsRangeUP = Math.Max(2, Math.Min(5, value)); }
		}
	
		[Range(2, 5)]
		[NinjaScriptProperty]
		[Display(Name="Cumulative Delta Bars Range DOWN", Description="Number of bars to check for down arrows (2-5)", Order=4, GroupName="4.01_Cumulative Delta")]
		public int CumulativeDeltaBarsRangeDOWN
		{
			get { return cumulativeDeltaBarsRangeDOWN; }
			set { cumulativeDeltaBarsRangeDOWN = Math.Max(2, Math.Min(5, value)); }
		}
	
		[NinjaScriptProperty]
		[Display(Name="Cumulative Delta Jump UP", Description="Minimum jump in Cumulative Delta for up arrows", Order=5, GroupName="4.01_Cumulative Delta")]
		public int CumulativeDeltaJumpUP
		{
			get { return cumulativeDeltaJumpUP; }
			set { cumulativeDeltaJumpUP = value; }
		}
	
		[NinjaScriptProperty]
		[Display(Name="Cumulative Delta Jump DOWN", Description="Minimum jump in Cumulative Delta for down arrows", Order=6, GroupName="4.01_Cumulative Delta")]
		public int CumulativeDeltaJumpDOWN
		{
			get { return cumulativeDeltaJumpDOWN; }
			set { cumulativeDeltaJumpDOWN = value; }
		}
		// ############## Bar Delta ############## //
		[NinjaScriptProperty]
		[Display(Name="Enable Bar Delta Condition UP", Description="Enable the bar delta condition for up arrows", Order=1, GroupName="4.02_Bar Delta")]
		public bool EnableBarDeltaConditionUP
		{
			get { return enableBarDeltaConditionUP; }
			set { enableBarDeltaConditionUP = value; }
		}
		
		[NinjaScriptProperty]
		[Display(Name="Enable Bar Delta Condition DOWN", Description="Enable the bar delta condition for down arrows", Order=2, GroupName="4.02_Bar Delta")]
		public bool EnableBarDeltaConditionDOWN
		{
			get { return enableBarDeltaConditionDOWN; }
			set { enableBarDeltaConditionDOWN = value; }
		}
		
		[Range(2, 5)]
		[NinjaScriptProperty]
		[Display(Name="Bar Delta Bars Range UP", Description="Number of bars to check for up arrows (2-5)", Order=3, GroupName="4.02_Bar Delta")]
		public int BarDeltaBarsRangeUP
		{
			get { return barDeltaBarsRangeUP; }
			set { barDeltaBarsRangeUP = Math.Max(2, Math.Min(5, value)); }
		}
		
		[Range(2, 5)]
		[NinjaScriptProperty]
		[Display(Name="Bar Delta Bars Range DOWN", Description="Number of bars to check for down arrows (2-5)", Order=4, GroupName="4.02_Bar Delta")]
		public int BarDeltaBarsRangeDOWN
		{
			get { return barDeltaBarsRangeDOWN; }
			set { barDeltaBarsRangeDOWN = Math.Max(2, Math.Min(5, value)); }
		}
		
		[NinjaScriptProperty]
		[Display(Name="Bar Delta Jump UP", Description="Minimum jump in Bar Delta for up arrows", Order=5, GroupName="4.02_Bar Delta")]
		public int BarDeltaJumpUP
		{
			get { return barDeltaJumpUP; }
			set { barDeltaJumpUP = value; }
		}
		
		[NinjaScriptProperty]
		[Display(Name="Bar Delta Jump DOWN", Description="Minimum jump in Bar Delta for down arrows", Order=6, GroupName="4.02_Bar Delta")]
		public int BarDeltaJumpDOWN
		{
			get { return barDeltaJumpDOWN; }
			set { barDeltaJumpDOWN = value; }
		}
		
		// ################# Delta Percent ############## //
		[NinjaScriptProperty]
		[Display(Name="Enable Delta Percent Condition UP", Description="Enable the delta percent condition for up arrows", Order=1, GroupName="4.03_Delta Percent")]
		public bool EnableDeltaPercentConditionUP
		{
			get { return enableDeltaPercentConditionUP; }
			set { enableDeltaPercentConditionUP = value; }
		}
		
		[NinjaScriptProperty]
		[Display(Name="Enable Delta Percent Condition DOWN", Description="Enable the delta percent condition for down arrows", Order=2, GroupName="4.03_Delta Percent")]
		public bool EnableDeltaPercentConditionDOWN
		{
			get { return enableDeltaPercentConditionDOWN; }
			set { enableDeltaPercentConditionDOWN = value; }
		}
		
		[Range(2, 5)]
		[NinjaScriptProperty]
		[Display(Name="Delta Percent Bars Range UP", Description="Number of bars to check for up arrows (2-5)", Order=3, GroupName="4.03_Delta Percent")]
		public int DeltaPercentBarsRangeUP
		{
			get { return deltaPercentBarsRangeUP; }
			set { deltaPercentBarsRangeUP = Math.Max(2, Math.Min(5, value)); }
		}
		
		[Range(2, 5)]
		[NinjaScriptProperty]
		[Display(Name="Delta Percent Bars Range DOWN", Description="Number of bars to check for down arrows (2-5)", Order=4, GroupName="4.03_Delta Percent")]
		public int DeltaPercentBarsRangeDOWN
		{
			get { return deltaPercentBarsRangeDOWN; }
			set { deltaPercentBarsRangeDOWN = Math.Max(2, Math.Min(5, value)); }
		}
		
		[NinjaScriptProperty]
		[Display(Name="Delta Percent Jump UP", Description="Minimum jump in Delta Percent for up arrows", Order=5, GroupName="4.03_Delta Percent")]
		public double DeltaPercentJumpUP
		{
			get { return deltaPercentJumpUP; }
			set { deltaPercentJumpUP = value; }
		}
		
		[NinjaScriptProperty]
		[Display(Name="Delta Percent Jump DOWN", Description="Minimum jump in Delta Percent for down arrows", Order=6, GroupName="4.03_Delta Percent")]
		public double DeltaPercentJumpDOWN
		{
			get { return deltaPercentJumpDOWN; }
			set { deltaPercentJumpDOWN = value; }
		}
		
		[NinjaScriptProperty]
		[Display(Name="Enable Max/Min Delta Condition UP", Description="Enable the Max/Min Delta condition for up arrows", Order=1, GroupName="4.04_Max/Min Delta")]
		public bool EnableMaxMinDeltaConditionUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Max/Min Delta Condition DOWN", Description="Enable the Max/Min Delta condition for down arrows", Order=2, GroupName="4.04_Max/Min Delta")]
		public bool EnableMaxMinDeltaConditionDOWN { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Minimum MaxDelta0 for UP", Description="Minimum value for MaxDelta0 for up arrows", Order=3, GroupName="4.04_Max/Min Delta")]
		public double MinMaxDelta0UP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Maximum MinDelta0 for UP", Description="Maximum value for MinDelta0 for up arrows", Order=4, GroupName="4.04_Max/Min Delta")]
		public double MaxMinDelta0UP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Maximum MinDelta0 for DOWN", Description="Maximum (negative) value for MinDelta0 for down arrows", Order=5, GroupName="4.04_Max/Min Delta")]
		public double MaxMinDelta0DOWN { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Minimum MaxDelta0 for DOWN", Description="Minimum value for MaxDelta0 for down arrows", Order=6, GroupName="4.04_Max/Min Delta")]
		public double MinMaxDelta0DOWN { get; set; }
		// ############## 4.05_Max Bid Ask Volume ############## //
		[NinjaScriptProperty]
		[Display(Name="Enable Max Volume Condition UP", Description="Enable the Max Volume condition for up arrows", Order=1, GroupName="4.05_Max Bid Ask Volume")]
		public bool EnableMaxVolumeConditionUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Max Volume Condition DOWN", Description="Enable the Max Volume condition for down arrows", Order=2, GroupName="4.05_Max Bid Ask Volume")]
		public bool EnableMaxVolumeConditionDOWN { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Max Ask - Max Bid Difference UP", Description="Minimum difference between Max Ask and Max Bid for up arrows", Order=3, GroupName="4.05_Max Bid Ask Volume")]
		public int MaxAskBidDifferenceUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Max Bid - Max Ask Difference DOWN", Description="Minimum difference between Max Bid and Max Ask for down arrows", Order=4, GroupName="4.05_Max Bid Ask Volume")]
		public int MaxBidAskDifferenceDOWN { get; set; }
		
		// ############## 4.06_Max Bid Ask Volume Range ############## //
		[NinjaScriptProperty]
		[Display(Name="Enable Max Volume Range Condition UP", Description="Enable the Max Volume Range condition for up arrows", Order=1, GroupName="4.06_Max Bid Ask Volume Range")]
		public bool EnableMaxVolumeRangeConditionUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Max Volume Range Condition DOWN", Description="Enable the Max Volume Range condition for down arrows", Order=2, GroupName="4.06_Max Bid Ask Volume Range")]
		public bool EnableMaxVolumeRangeConditionDOWN { get; set; }
		
		[Range(2, 5)]
		[NinjaScriptProperty]
		[Display(Name="Max Volume Bars Range UP", Description="Number of bars to check for up arrows (2-5)", Order=3, GroupName="4.06_Max Bid Ask Volume Range")]
		public int MaxVolumeBarsRangeUP { get; set; }
		
		[Range(2, 5)]
		[NinjaScriptProperty]
		[Display(Name="Max Volume Bars Range DOWN", Description="Number of bars to check for down arrows (2-5)", Order=4, GroupName="4.06_Max Bid Ask Volume Range")]
		public int MaxVolumeBarsRangeDOWN { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Max Volume Jump UP", Description="Minimum jump in Max Volume difference for up arrows", Order=5, GroupName="4.06_Max Bid Ask Volume Range")]
		public int MaxVolumeJumpUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Max Volume Jump DOWN", Description="Minimum jump in Max Volume difference for down arrows", Order=6, GroupName="4.06_Max Bid Ask Volume Range")]
		public int MaxVolumeJumpDOWN { get; set; }
		// ############## 4.07_POC Condition ############## //
		[NinjaScriptProperty]
		[Display(Name="Enable POC Condition UP", Description="Enable the POC condition for up arrows", Order=1, GroupName="4.07_POC Condition")]
		public bool EnablePOCConditionUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable POC Condition DOWN", Description="Enable the POC condition for down arrows", Order=2, GroupName="4.07_POC Condition")]
		public bool EnablePOCConditionDOWN { get; set; }

        [NinjaScriptProperty]
		[Range(1, 5)]
		[Display(Name="POC Bars Look Back UP", Description="Number of previous bars to compare POC with for up arrows (1-5)", Order=3, GroupName="4.07_POC Condition")]
		public int POCBarsLookBackUP { get; set; }

		[NinjaScriptProperty]
		[Range(1, 5)]
		[Display(Name="POC Bars Look Back DOWN", Description="Number of previous bars to compare POC with for down arrows (1-5)", Order=4, GroupName="4.07_POC Condition")]
		public int POCBarsLookBackDOWN { get; set; }
		// ############## 4.08_STD3 Distance ############## //
		[NinjaScriptProperty]
		[Display(Name="Enable STD3 Distance Condition UP", Description="Enable the STD3 distance condition for up arrows", Order=1, GroupName="4.08_STD3 Distance")]
		public bool EnableSTD3DistanceConditionUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable STD3 Distance Condition DOWN", Description="Enable the STD3 distance condition for down arrows", Order=2, GroupName="4.08_STD3 Distance")]
		public bool EnableSTD3DistanceConditionDOWN { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="STD3 Distance Offset UP (ticks)", Description="Additional ticks required for STD3 distance increase for up arrows", Order=3, GroupName="4.08_STD3 Distance")]
		public int STD3DistanceOffsetUP { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="STD3 Distance Offset DOWN (ticks)", Description="Additional ticks required for STD3 distance increase for down arrows", Order=4, GroupName="4.08_STD3 Distance")]
		public int STD3DistanceOffsetDOWN { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable POC Volume Condition UP", Description="Enable the POC volume condition for up arrows", Order=1, GroupName="4.09_POC Volume")]
		public bool EnablePOCVolumeConditionUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable POC Volume Condition DOWN", Description="Enable the POC volume condition for down arrows", Order=2, GroupName="4.09_POC Volume")]
		public bool EnablePOCVolumeConditionDOWN { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Minimum POC Volume UP", Description="Minimum POC volume required for up arrows", Order=3, GroupName="4.09_POC Volume")]
		public int MinPOCVolumeUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Minimum POC Volume DOWN", Description="Minimum POC volume required for down arrows", Order=4, GroupName="4.09_POC Volume")]
		public int MinPOCVolumeDOWN { get; set; }
		
		// ############## 4.10_VA Breakout Reversal ############## //
		[NinjaScriptProperty]
		[Display(Name="Enable VA Breakout Reversal UP", Description="Enable the VA breakout reversal condition for up arrows", Order=1, GroupName="4.10_VA Breakout Reversal")]
		public bool EnableVABreakoutReversalUP { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable VA Breakout Reversal DOWN", Description="Enable the VA breakout reversal condition for down arrows", Order=2, GroupName="4.10_VA Breakout Reversal")]
		public bool EnableVABreakoutReversalDOWN { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name="VA Breakout Bars Range UP", Description="Number of bars to check for VA breakout (1-10)", Order=3, GroupName="4.10_VA Breakout Reversal")]
		public int VABreakoutBarsRangeUP { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name="VA Breakout Bars Range DOWN", Description="Number of bars to check for VA breakout (1-10)", Order=4, GroupName="4.10_VA Breakout Reversal")]
		public int VABreakoutBarsRangeDOWN { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="VA Breakout Offset Ticks", Description="Number of ticks for VA breakout offset", Order=5, GroupName="4.10_VA Breakout Reversal")]
		public int VABreakoutOffsetTicks { get; set; }

		// ############## 4.11_VA Breakout Reversal 2 ############## //
		[NinjaScriptProperty]
		[Display(Name="Enable VA Breakout Reversal UP 2", Description="Enable VA breakout reversal condition 2 for UP signals", Order=1, GroupName="4.11_VA Breakout Reversal 2")]
		public bool EnableVABreakoutReversalUP2 { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Enable VA Breakout Reversal DOWN 2", Description="Enable VA breakout reversal condition 2 for DOWN signals", Order=2, GroupName="4.11_VA Breakout Reversal 2")]
		public bool EnableVABreakoutReversalDOWN2 { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="VA Breakout Bars Range UP 2", Description="Number of bars to check for UP breakout pattern 2", Order=3, GroupName="4.11_VA Breakout Reversal 2")]
		public int VABreakoutBarsRangeUP2 { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="VA Breakout Bars Range DOWN 2", Description="Number of bars to check for DOWN breakout pattern 2", Order=4, GroupName="4.11_VA Breakout Reversal 2")]
		public int VABreakoutBarsRangeDOWN2 { get; set; }

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="VA Breakout Offset Ticks 2", Description="Number of ticks for offset in breakout pattern 2", Order=5, GroupName="4.11_VA Breakout Reversal 2")]
		public int VABreakoutOffsetTicks2 { get; set; }
		
		//
		[NinjaScriptProperty]
		[Display(Name="Enable Sellers For Buy", Description="Enable sellers condition for buy signals", Order=1, GroupName="4.12_Sellers Buyers Conditions")]
		public bool EnableSellersForBuy { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Buyers For Sell", Description="Enable buyers condition for sell signals", Order=2, GroupName="4.12_Sellers Buyers Conditions")]
		public bool EnableBuyersForSell { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Sellers Difference Threshold", Description="Minimum difference required for sellers condition", Order=3, GroupName="4.12_Sellers Buyers Conditions")]
		public double SellersDifferenceThreshold { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Buyers Difference Threshold", Description="Minimum difference required for buyers condition", Order=4, GroupName="4.12_Sellers Buyers Conditions")]
		public double BuyersDifferenceThreshold { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable VA Breakout Reversal UP 3", Description="Enable VA breakout reversal condition 3 for UP signals", Order=1, GroupName="4.13_VA Breakout Reversal 3")]
		public bool EnableVABreakoutReversalUP3 { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable VA Breakout Reversal DOWN 3", Description="Enable VA breakout reversal condition 3 for DOWN signals", Order=2, GroupName="4.13_VA Breakout Reversal 3")]
		public bool EnableVABreakoutReversalDOWN3 { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="VA Breakout Bars Range UP 3", Description="Number of bars to check for UP breakout pattern 3", Order=3, GroupName="4.13_VA Breakout Reversal 3")]
		public int VABreakoutBarsRangeUP3 { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="VA Breakout Bars Range DOWN 3", Description="Number of bars to check for DOWN breakout pattern 3", Order=4, GroupName="4.13_VA Breakout Reversal 3")]
		public int VABreakoutBarsRangeDOWN3 { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="VA Breakout Offset Ticks 3", Description="Number of ticks for offset in breakout pattern 3", Order=5, GroupName="4.13_VA Breakout Reversal 3")]
		public int VABreakoutOffsetTicks3 { get; set; }
		
		// Ajout des propriétés
		[NinjaScriptProperty]
		[Display(Name="Enable VA Breakout Reversal UP 4", Description="Enable VA breakout reversal condition 4 for UP signals", Order=1, GroupName="4.14_VA Breakout Reversal 4")]
		public bool EnableVABreakoutReversalUP4 { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable VA Breakout Reversal DOWN 4", Description="Enable VA breakout reversal condition 4 for DOWN signals", Order=2, GroupName="4.14_VA Breakout Reversal 4")]
		public bool EnableVABreakoutReversalDOWN4 { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="VA Breakout Bars Range UP 4", Description="Number of bars to check for UP breakout pattern 4", Order=3, GroupName="4.14_VA Breakout Reversal 4")]
		public int VABreakoutBarsRangeUP4 { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="VA Breakout Bars Range DOWN 4", Description="Number of bars to check for DOWN breakout pattern 4", Order=4, GroupName="4.14_VA Breakout Reversal 4")]
		public int VABreakoutBarsRangeDOWN4 { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="VA Breakout Offset Ticks 4", Description="Number of ticks for offset in breakout pattern 4", Order=5, GroupName="4.14_VA Breakout Reversal 4")]
		public int VABreakoutOffsetTicks4 { get; set; }
		
		// Ajout des propriétés pour VA Breakout Reversal 5
		[NinjaScriptProperty]
		[Display(Name="Enable VA Breakout Reversal UP 5", Description="Enable VA breakout reversal condition 5 for UP signals", Order=1, GroupName="4.15_VA Breakout Reversal 5")]
		public bool EnableVABreakoutReversalUP5 { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable VA Breakout Reversal DOWN 5", Description="Enable VA breakout reversal condition 5 for DOWN signals", Order=2, GroupName="4.15_VA Breakout Reversal 5")]
		public bool EnableVABreakoutReversalDOWN5 { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="VA Breakout Bars Range UP 5", Description="Number of bars to check for UP breakout pattern 5", Order=3, GroupName="4.15_VA Breakout Reversal 5")]
		public int VABreakoutBarsRangeUP5 { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="VA Breakout Bars Range DOWN 5", Description="Number of bars to check for DOWN breakout pattern 5", Order=4, GroupName="4.15_VA Breakout Reversal 5")]
		public int VABreakoutBarsRangeDOWN5 { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="VA Breakout Offset Ticks 5", Description="Number of ticks for offset in breakout pattern 5", Order=5, GroupName="4.15_VA Breakout Reversal 5")]
		public int VABreakoutOffsetTicks5 { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Use Previous Bar for UP", Description="Use previous bar (Low[1]) instead of current bar (Low[0]) for UP signals", Order=6, GroupName="4.15_VA Breakout Reversal 5")]
		public bool UsePreviousBarUP5 { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Use Previous Bar for DOWN", Description="Use previous bar (High[1]) instead of current bar (High[0]) for DOWN signals", Order=7, GroupName="4.15_VA Breakout Reversal 5")]
		public bool UsePreviousBarDOWN5 { get; set; }
		
		// ############ Initial Balance ################ //
		[NinjaScriptProperty]
		[Display(Name="Enable Initial Balance Logic", Description="Enable the Initial Balance logic", Order=1, GroupName="5.01_Initial Balance")]
		public bool EnableIBLogic { get; set; }
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="IB Start Time", Description="Start time of the Initial Balance period", Order=2, GroupName="5.01_Initial Balance")]
		public DateTime IBStartTime { get; set; }
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="IB End Time", Description="End time of the Initial Balance period", Order=3, GroupName="5.01_Initial Balance")]
		public DateTime IBEndTime { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="IB Offset Ticks", Description="Number of ticks to offset the IB levels", Order=4, GroupName="5.01_Initial Balance")]
		public int IBOffsetTicks { get; set; }
		
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Previous Session Range Breakout", Description = "Enable checking for breakouts of the previous session's StdDev1 range", Order = 1, GroupName = "6.01_Previous Session Range")]
		public bool EnablePreviousSessionRangeBreakout { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Enable Range Size Filter", Description="Enable filtering based on previous session range size", Order=2, GroupName="6.01_Previous Session Range")]
		public bool EnableRangeSizeFilter { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Minimum Range Size (ticks)", Description="Minimum size of the previous session range in ticks", Order=3, GroupName="6.01_Previous Session Range")]
		public int MinRangeSize { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Maximum Range Size (ticks)", Description="Maximum size of the previous session range in ticks", Order=4, GroupName="6.01_Previous Session Range")]
		public int MaxRangeSize { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Range Adjustment (ticks)", Description="Number of ticks to adjust the range when outside min/max bounds", Order=5, GroupName="6.01_Previous Session Range")]
		public int RangeAdjustment { get; set; }
		
		
		//########################//
		// ############ ATR #############
		// ATR
		[NinjaScriptProperty]
		[Range(0, 1)]
		[Display(Name = "OKisATR", Description = "Check ATR", Order = 0, GroupName = "9.01_ATR")]
		public bool OKisATR { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Fmin ATR", Order = 1, GroupName = "9.01_ATR")]
		public double FminATR { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Fmax ATR", Order = 2, GroupName = "9.01_ATR")]
		public double FmaxATR { get; set; }
		
		// ############ ADX #############
		// ADX
		[NinjaScriptProperty]
		[Range(0, 1)]
		[Display(Name = "OKisADX", Description = "Check ADX", Order = 0, GroupName = "9.02_ADX")]
		public bool OKisADX { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Fmin ADX", Order = 1, GroupName = "9.02_ADX")]
		public double FminADX { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Fmax ADX", Order = 2, GroupName = "9.02_ADX")]
		public double FmaxADX { get; set; }
		
		// ############ Volume #############
		// Volume
		[NinjaScriptProperty]
		[Range(0, 1)]
		[Display(Name = "OKisVOL", Description = "Check Volume", Order = 0, GroupName = "9.03_Volume")]
		public bool OKisVOL { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Fperiod Vol", Order = 1, GroupName = "9.03_Volume")]
		public int FperiodVol { get; set; }
		
		// ############# Propriétés Volumetric Filter ################ //
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Up Arrow Color", Order = 1, GroupName = "9.100_Visuals")]
        public Brush UpArrowColor { get; set; }

        [Browsable(false)]
        public string UpArrowColorSerializable
        {
            get { return Serialize.BrushToString(UpArrowColor); }
            set { UpArrowColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Down Arrow Color", Order = 2, GroupName = "9.100_Visuals")]
        public Brush DownArrowColor { get; set; }

        [Browsable(false)]
        public string DownArrowColorSerializable
        {
            get { return Serialize.BrushToString(DownArrowColor); }
            set { DownArrowColor = Serialize.StringToBrush(value); }
        }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="POC Color", Description="Color for POC", Order=3, GroupName="9.100_Visuals")]
		public Brush POCColor { get; set; }
		
		[Browsable(false)]
		public Series<double> POC
		{
			get { return Values[0]; }
		}
		
	   // ... (autres propriétés)
        [Browsable(false)]
        [XmlIgnore]
        public Series<double> VWAP => Values[0];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> StdDev1Upper => Values[1];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> StdDev1Lower => Values[2];
		
		#endregion
	}
}
