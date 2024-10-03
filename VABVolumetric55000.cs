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
	public class VABVolumetric55000 : Strategy
	{
		private OrderFlowCumulativeDelta OFCD1;
		private Swing Swing1;
		private Swing Swing2;
		private Swing Swing3;
		private OBV OBV1;
		private SMA SMA1;
		private Series<double> deltaCloseSeries;
		public Calculate MyCalculateMode { get; set; }
		private int[] touches = new int[300];
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Strategy OFDeltaProcent avec delta %";
				Name										= "VABVolumetric55000";
				Calculate = MyCalculateMode;
				// Calculate									= Calculate.OnBarClose;
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
				BarsRequiredToTrade							= 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= false;
				StartTime						= DateTime.Parse("15:30", System.Globalization.CultureInfo.InvariantCulture);
				EndTime							= DateTime.Parse("22:00", System.Globalization.CultureInfo.InvariantCulture);
				
				Qty											= 1;
				Sl											= 15;
				Pt											= 70;
				BeTarget									= 20;
				BeOfSet										= 25;
				
				Vol0T										= 2000;
				Vol1T										= 2000;
				Vol2T										= 2000;
				seuilVolume 								= 10000;
				seuilVolumeMax 								= 10000000;
				
				Delta0T										= 500;
				Delta1T										= 500;
				Delta2T										= 200;
				Dsize										= 20;
				DsizeT										= 60;
				
				PDelta0T									= 3;
				PDelta1T									= 0;
				PDelta2T									= 2;
				
				FSelAbsortion								= 1000;
				FdeltaLow0									= 500;
				FBuyAbsortion								= 1000;
				FdeltaHigh0									= 500;
				FmultipleDelta								= 3;
				
				VolumeMALength								= 20;
				AtrThreshold								= 2;
				atrThresholdMax								= 20;
				AdxThreshold								= 25;
				EMALength									= 30;
				EMALength2									= 100;
				smaOvbLength								= 14;
				
				MaxBuyTradesT								= 2;
				MaxSellTradesT								= 2;
			}
			else if (State == State.Configure)
			{
				AddDataSeries(Data.BarsPeriodType.Tick, 1);
				deltaCloseSeries = new Series<double>(this);
				
				OFCD1				= OrderFlowCumulativeDelta(CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0); // Initialisation de OrderFlowCumulativeDelta
				Swing1				= Swing(Close, 5);
				Swing2				= Swing(OFCD1.DeltaClose, 5);
				Swing3				= Swing(OBV1, 5);
				OBV1				= OBV(Close);
				SMA1				= SMA(OBV1, 14);
				if (UseTralingStop)
				{
					// SetTrailStop(@"Long", CalculationMode.Ticks, Sl, true);
					// SetTrailStop(@"Short", CalculationMode.Ticks, Sl, true);
					SetStopLoss(@"Long", CalculationMode.Ticks, Sl, false);
					SetStopLoss(@"Short", CalculationMode.Ticks, Sl, false);
					SetProfitTarget(@"Long", CalculationMode.Ticks, Pt);
					SetProfitTarget(@"Short", CalculationMode.Ticks, Pt);
				}
				else
				{
					SetProfitTarget(@"Long", CalculationMode.Ticks, Pt);
					SetProfitTarget(@"Short", CalculationMode.Ticks, Pt);
					SetParabolicStop("Long", CalculationMode.Ticks, Sl, true, 0.09, 0.9, 0.09);
					SetParabolicStop("Short", CalculationMode.Ticks, Sl, true, 0.09, 0.9, 0.09);
				}
			}
		}

		protected override void OnBarUpdate()
		{
			if (BarsInProgress != 0) 
				return;
			
			if (CurrentBars[0] < 20) return;
			
			if ((Times[0][0].TimeOfDay < StartTime.TimeOfDay) || (Times[0][0].TimeOfDay > EndTime.TimeOfDay))
			{
				// Clôturer toutes les positions ouvertes
				if (Position.MarketPosition != MarketPosition.Flat)
				{
					if (Position.MarketPosition == MarketPosition.Long)
						ExitLong();
					else if (Position.MarketPosition == MarketPosition.Short)
						ExitShort();
				}
				return;
			}
			
			if (BarsInProgress == 1)
            {
                // Update the secondary series of the cached indicator to stay in sync with BarsInProgress == 0
                OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).Update(OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).BarsArray[1].Count - 1, 1);
				OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0).Update(OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0).BarsArray[1].Count - 1, 1);
				OFCD1.Update(OFCD1.BarsArray[1].Count - 1, 1);
				// OrderFlowVWAP(BarsArray[0], VWAPResolution.Tick, BarsArray[0].TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).Update(OrderFlowVWAP(BarsArray[0], VWAPResolution.Tick, BarsArray[0].TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).BarsArray[1].Count - 1, 1);
                return;
            }
			
			// double currentVWAP = OrderFlowVWAP(VWAPResolution.Tick, TradingHours.String2TradingHours("CME US Index Futures RTH"), VWAPStandardDeviations.Three, 1, 2, 3).VWAP[0];
			double currentDayLow = CurrentDayOHL().CurrentLow[0];
            double currentDayHigh = CurrentDayOHL().CurrentHigh[0];
			
			bool isLowWithinRange0 = Low[0] <= currentDayLow;
			bool isLowWithinRange1 = Low[0] <= currentDayLow || Low[1] <= currentDayLow;
			bool isLowWithinRange2 = Low[0] <= currentDayLow || Low[1] <= currentDayLow || Low[2] <= currentDayLow;
			bool isLowWithinRange6 = Low[0] <= currentDayLow || Low[1] <= currentDayLow || Low[2] <= currentDayLow || Low[3] <= currentDayLow || Low[4] <= currentDayLow || Low[5] <= currentDayLow || Low[6] <= currentDayLow;
			
			bool isHighWithinRange0 = High[0] >= currentDayHigh;
			bool isHighWithinRange1 = High[0] >= currentDayHigh || High[1] >= currentDayHigh;
			bool isHighWithinRange2 = High[0] >= currentDayHigh || High[1] >= currentDayHigh || High[2] >= currentDayHigh;
			bool isHighWithinRange6 = High[0] >= currentDayHigh || High[1] >= currentDayHigh || High[2] >= currentDayHigh || High[3] >= currentDayHigh || High[4] >= currentDayHigh || High[5] >= currentDayHigh || High[6] >= currentDayHigh;
			
			bool isBreakEvenActive = false; // Contrôle si le break-even est actif
            
			double vol0 = Volume[0];
			double vol1 = Volume[1];
			double vol2 = Volume[2];
			double delta0 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).DeltaClose[0];
            double delta1 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).DeltaClose[1];
            double delta2 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).DeltaClose[2];
			double deltaLow0 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).DeltaLow[0];
			double deltaLow1 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).DeltaLow[1];
			double deltaLow2 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).DeltaLow[2];
			double deltaHigh0 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).DeltaHigh[0];
			double deltaHigh1 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).DeltaHigh[1];
			double deltaHigh2 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, 0).DeltaHigh[2];
			double deltaSessionClose0 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0).DeltaClose[0];
			double deltaSessionClose1 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0).DeltaClose[1];
			double deltaSessionClose2 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0).DeltaClose[2];
			double deltaSessionOpen0 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0).DeltaOpen[0];
			double deltaSessionOpen1 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0).DeltaOpen[1];
			double deltaSessionOpen2 = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0).DeltaOpen[2];
			double delta0_size = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Bar, Dsize).DeltaClose[0];
			double deltaSC0SMA = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, Dsize).DeltaClose[0];
			double pdelta0 = (delta0 / vol0) * 100;
			double pdelta1 = (delta1 / vol1) * 100;
			double pdelta2 = (delta2 / vol2) * 100;
			double volumeTotal = Volume[0] + Volume[1] + Volume[2];
			
			// VolumeAndATRStrategy
			double v = Volume[0];
			double va = SMA(Volume, VolumeMALength)[0];
			double atr = ATR(14)[0];
			double adx = ADX(14)[0];
			double ema = EMA(Close, EMALength)[0];
			double ema2 = EMA(Close, EMALength2)[0];
			double trendLineValue = TrendLines(Close, 5, 4, 25, true)[0];
			// OBV1 = OBV(Close);
			// SMA1 = SMA(OBV1, smaOvbLength);
			deltaCloseSeries[0] = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, Dsize).DeltaClose[0];
			double smaDeltaSC0 = SMA(deltaCloseSeries, smaOvbLength)[0];
			double upperValue = Bollinger(2, 14).Upper[0];
			double lowerValue = Bollinger(2, 14).Lower[0];
			
			// ####################
			double SwingHigh1 = High[Math.Max(0, Swing(5).SwingHighBar(0, 1, 100))];
			double SwingHigh2 = High[Math.Max(0, Swing(5).SwingHighBar(0, 2, 100))];
			double SwingHigh3 = High[Math.Max(0, Swing(5).SwingHighBar(0, 3, 100))];
			
			double SwingLow1 = Low[Math.Max(0, Swing(5).SwingLowBar(0, 1, 100))];
			double SwingLow2 = Low[Math.Max(0, Swing(5).SwingLowBar(0, 2, 100))];
			double SwingLow3 = Low[Math.Max(0, Swing(5).SwingLowBar(0, 3, 100))];
			
			double SelAbsortion = deltaLow0 - delta0;
			int SelAbsortionInt = Convert.ToInt32(Math.Abs(SelAbsortion));
			int deltaLow0Int = Convert.ToInt32(Math.Abs(deltaLow0));
			
			double BuyAbsortion = deltaHigh0 - delta0;
			int BuyAbsortionInt = Convert.ToInt32(Math.Abs(BuyAbsortion));
			int deltaHigh0Int = Convert.ToInt32(Math.Abs(deltaHigh0));
			
			
			// Convertir les valeurs delta en nombres positifs (en supposant qu'elles sont dans la plage d'entiers)
			int positiveDelta0 = Convert.ToInt32(Math.Abs(delta0));
			int positiveDeltaLow0 = Convert.ToInt32(Math.Abs(deltaLow0));
			int positiveDeltaHigh0 = Convert.ToInt32(Math.Abs(deltaHigh0));
			
			// Multiplier delta0 par 3 (la raison de cette multiplication devrait être documentée)
			int multipleDelta0 = positiveDelta0 * FmultipleDelta;
			
			// Vérifier si les autres deltas sont supérieurs ou égaux à trois fois delta0
			bool isDeltaLow0GreaterDelta0 = positiveDeltaLow0 >= multipleDelta0;
			bool isDeltaHigh0GreaterDelta0 = positiveDeltaHigh0 >= multipleDelta0;
			
			bool isHigh01 = High[0] > High[1];
			bool isLow01 = Low[0] < Low[1];
			bool orderEnteredBuy = false;
			bool orderEnteredSell = false;
			
			// ####################
			// Buy Condition
				if ((!isVolOK || vol0 > Vol0T && vol1 > Vol1T && vol2 > Vol2T)
					&& (!isvolumeTotalOK || volumeTotal > seuilVolume && volumeTotal < seuilVolumeMax)
					&& (!isVsmaOK || v > va)
					&& (!isAtrOK || atr > AtrThreshold && atr < atrThresholdMax)
					&& (!isAdxOK || adx > AdxThreshold)
					&& (!isDiOK || delta0 > delta1 && delta1 > delta2)
					&& (!isPDiOK || pdelta0 > pdelta1 && pdelta1 > pdelta2)
					&& (!isDeltaBarre0ForBuyOK || delta0 > Delta0T)
					&& (!isDeltaBarre1ForBuyOK || delta1 > Delta1T)
					&& (!isDeltaBarre2ForBuyOK || delta2 < -Delta2T)
					&& (!isPrecentDeltaBarre0ForBuyOK || pdelta0 > PDelta0T)
					&& (!isPrecentDeltaBarre1ForBuyOK || pdelta1 > PDelta1T)
					&& (!isPrecentDeltaBarre2ForBuyOK || pdelta2 < -PDelta2T)
					&& (!isDSession1ForBuy || deltaSessionClose0 > deltaSessionOpen1)
					&& (!isDSession2ForBuy || deltaSessionClose0 > deltaSessionOpen2)
					&& (!isEmaForBuy || Close[0] > ema)
					&& (!isdelta0_sizeForBuy || delta0_size > DsizeT)
					&& (!isDCforBuyok || delta0 > Delta0T && delta1 < -Delta1T)
					&& (!isPrecentDCforBuyok || pdelta0 > PDelta0T && pdelta1 < -PDelta1T)
					&& (!isTLForBuy || Close[0] > trendLineValue)
					&& (!IsLowCurrentDayLow0 || isLowWithinRange0)
					&& (!IsLowCurrentDayLow1 || isLowWithinRange1)
					&& (!IsLowCurrentDayLow2 || isLowWithinRange2)
					&& (!IsLowCurrentDayLow6 || isLowWithinRange6)
					&& (!IsOVBforBuy || OBV1[0] > SMA1[0])
					&& (!IsDeltaSC0SMAforBuy || deltaSC0SMA > smaDeltaSC0)
					&& (!IsBollingerforBuy || Low[0] <= lowerValue)
					&& (!isEmaForBuy2 || Close[0] > ema2 && ema > ema2)
					&& (!isSwing1Buy || High[0] > Swing1.SwingHigh[0])
					&& (!isSwing2Buy || OFCD1.DeltaClose[0] > Swing2.SwingHigh[0])
					&& (!isSwing3Buy || OBV1[0] > Swing3.SwingHigh[0])
					&& (!isSwingLowUp12 || SwingLow1 > SwingLow2)
					&& (!isSwingLowUp23 || SwingLow2 > SwingLow3)
					&& (Position.MarketPosition == MarketPosition.Flat)
					&& (!isCloseUP || Close[0] > Open[0])
					&& (!IsSelAsobtion || SelAbsortionInt >= FSelAbsortion)
					&& (!isFdeltaLow0OK || deltaLow0Int >= FdeltaLow0)
					&& (!isOkDeltaLow0GreaterDelta0 || isDeltaLow0GreaterDelta0)
					&& (!isOkHigh01 || isHigh01)
					)
				{
					// EnterLong(Convert.ToInt32(Qty), @"Long");
					if (!SelOnly)
					{
						if (useMIT)
						{
							EnterLongMIT(0, false, Qty, GetCurrentBid(), @"Long");
							orderEnteredBuy = true;
							// touches[0]++;
						}
						else if (useLimit)
						{
							EnterLongLimit(0, false, Qty, GetCurrentBid(), @"Long");
							orderEnteredBuy = true;
							// touches[0]++;
						}
						else if (useMarket)
						{
							EnterLong(Convert.ToInt32(Qty), @"Long");
							// EnterLong(1, Qty, @"Long");
							orderEnteredBuy = true;
							// touches[0]++;
						}
						else if (useStopMarket)
						{
							EnterLongStopMarket(0, false, Qty, GetCurrentBid(), @"Long");
							orderEnteredBuy = true;
							// touches[0]++;
						}
						// touches[0]++;	
						// orderEnteredBuy = true;
						return;
					}
				}

				// ######################
				// Sell Condition
				if ((!isVolOK || vol0 > Vol0T && vol1 > Vol1T && vol2 > Vol2T)
					&& (!isvolumeTotalOK || volumeTotal > seuilVolume && volumeTotal < seuilVolumeMax)
					&& (!isVsmaOK || v > va)
					&& (!isAtrOK || atr > AtrThreshold && atr < atrThresholdMax)
					&& (!isAdxOK || adx > AdxThreshold)
					&& (!isDdOK || delta0 < delta1 && delta1 < delta2)
					&& (!isPDdOK || pdelta0 < pdelta1 && pdelta1 < pdelta2)
					&& (!isDeltaBarre0ForSelOK || delta0 < -Delta0T)
					&& (!isDeltaBarre1ForSelOK || delta1 < -Delta1T)
					&& (!isDeltaBarre2ForSelOK || delta2 > Delta2T)
					&& (!isPrecentDeltaBarre0ForSelOK || pdelta0 < -PDelta0T)
					&& (!isPrecentDeltaBarre1ForSelOK || pdelta1 < -PDelta1T)
					&& (!isPrecentDeltaBarre2ForSelOK || pdelta2 > PDelta2T)
					&& (!isDSession1ForDel || deltaSessionClose0 < deltaSessionOpen1)
					&& (!isDSession2ForDel || deltaSessionClose0 < deltaSessionOpen2)
					&& (!isEmaForSel || Close[0] < ema)
					&& (!isdelta0_sizeForSel || delta0_size < -DsizeT)
					&& (!isDCforSelok || delta0 < -Delta0T && delta1 > Delta1T)
					&& (!isPrecentDCforSelok || pdelta0 < -PDelta0T && pdelta1 > PDelta1T)
					&& (!isTLForSel || Close[0] < trendLineValue)
					&& (!IsHighCurrentDayHigh0 || isHighWithinRange0)
					&& (!IsHighCurrentDayHigh1 || isHighWithinRange1)
					&& (!IsHighCurrentDayHigh2 || isHighWithinRange2)
					&& (!IsHighCurrentDayHigh6 || isHighWithinRange6)
					&& (!IsOVBforSel || OBV1[0] < SMA1[0])
					&& (!IsDeltaSC0SMAforSel || deltaSC0SMA < smaDeltaSC0)
					&& (!IsBollingerforSel || High[0] >= upperValue)
					&& (!isEmaForSel2 || Close[0] < ema2 && ema < ema2)
					&& (!isSwing1Sel || Low[0] < Swing1.SwingLow[0])
					&& (!isSwing2Sel || OFCD1.DeltaClose[0] < Swing2.SwingLow[0])
					&& (!isSwing3Sel || OBV1[0] < Swing3.SwingLow[0])
					&& (!isSwingHighDown12 || SwingHigh1 < SwingHigh2)
					&& (!isSwingHighDown23 || SwingHigh2 < SwingHigh3)
					&& (Position.MarketPosition == MarketPosition.Flat)
					&& (!isCloseDown || Close[0] < Open[0])
					&& (!IsBuyAsobtion || BuyAbsortionInt >= FBuyAbsortion)
					&& (!isFdeltaHigh0OK || deltaHigh0Int >= FdeltaHigh0)
					&& (!isOkDeltaHigh0GreaterDelta0 || isDeltaHigh0GreaterDelta0)
					&& (!isOKLow01 || isLow01)
					)
				{
					// EnterShort(Convert.ToInt32(Qty), @"Short");
					if (!BuyOnly)
					{
						if (useMIT)
						{
							EnterShortMIT(0, false, Qty, GetCurrentAsk(), @"Short");
							orderEnteredSell = true;
//							touches[i]++;
						}
						else if (useLimit)
						{
							EnterShortLimit(0, false, Qty, GetCurrentAsk(), @"Short");
							orderEnteredSell = true;
//							touches[i]++;
						}
						else if (useMarket)
						{
							EnterShort(Convert.ToInt32(Qty), @"Short");
							// EnterShort(1, Qty, @"Short");
							orderEnteredSell = true;
//							touches[i]++;
						}
						else if (useStopMarket)
						{
							EnterShortStopMarket(0, false, Qty, GetCurrentAsk(), @"Short");
							orderEnteredSell = true;
//							touches[i]++;
						}
						// touches[i]++;	
						// orderEnteredSell = true;
						return;
					}	
				}
		}
		#region Properties
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="StartTime", Order=1, GroupName="0.Time_Parameters")]
		public DateTime StartTime
		{ get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="EndTime", Order=2, GroupName="0.Time_Parameters")]
		public DateTime EndTime
		{ get; set; }
		
		// ############# 1.Etry_Parameters ####################
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Qty", Order=1, GroupName="1.Etry_Parameters")]
		public int Qty
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Sl", Order=2, GroupName="1.Etry_Parameters")]
		public int Sl
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Pt", Order=3, GroupName="1.Etry_Parameters")]
		public int Pt
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="MaxBuyTradesT", Order=4, GroupName="1.Etry_Parameters")]
		public int MaxBuyTradesT
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="MaxSellTradesT", Order=5, GroupName="1.Etry_Parameters")]
		public int MaxSellTradesT
		{ get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="UseBE", Description="Use Break Even", Order=6, GroupName="1.Etry_Parameters")]
		public bool UseBE { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="BeTarget", Order=7, GroupName="1.Etry_Parameters")]
		public int BeTarget
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="BeOfSet", Order=8, GroupName="1.Etry_Parameters")]
		public int BeOfSet
		{ get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="UseTralingStop", Description="UseTralingStop", Order=9, GroupName="1.Etry_Parameters")]
		public bool UseTralingStop { get; set; }
		
		// Paramètres de la stratégie
		[Display(Name = "Buy Only", GroupName = "1.Etry_Parameters", Order = 10)]
		public bool BuyOnly { get; set; }
	
		[Display(Name = "Sell Only", GroupName = "1.Etry_Parameters", Order = 11)]
		public bool SelOnly { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="useMIT", Description="useMIT", Order=12, GroupName="1.Etry_Parameters")]
		public bool useMIT { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="useLimit", Description="useLimit", Order=13, GroupName="1.Etry_Parameters")]
		public bool useLimit { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="useMarket", Description="useMarket", Order=14, GroupName="1.Etry_Parameters")]
		public bool useMarket { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="useStopMarket", Description="useStopMarket", Order=15, GroupName="1.Etry_Parameters")]
		public bool useStopMarket { get; set; }
		
		// [Range(0, 1), NinjaScriptProperty]
		// [Display(Name="UseStopNormal", Description="UseStopNormal", Order=10, GroupName="Etry_Parameters")]
		// public bool UseStopNormal { get; set; }
		
		// ######### 2.Cummun_Setup #########################
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isVolOK", Description="isVolOK", Order=1, GroupName="2.Cummun_Setup")]
		public bool isVolOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isvolumeTotalOK", Description="isvolumeTotalOK", Order=2, GroupName="2.Cummun_Setup")]
		public bool isvolumeTotalOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isVsmaOK", Description="isVsmaOK", Order=3, GroupName="2.Cummun_Setup")]
		public bool isVsmaOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isAtrOK", Description="isAtrOK", Order=4, GroupName="2.Cummun_Setup")]
		public bool isAtrOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isAdxOK", Description="isAdxOK", Order=5, GroupName="2.Cummun_Setup")]
		public bool isAdxOK { get; set; }
	
		/// ########## 3.Buy_Setup ########################
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDiOK", Description="isDiOK", Order=1, GroupName="3.Buy_Setup")]
		public bool isDiOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isPDiOK", Description="isPDiOK", Order=2, GroupName="3.Buy_Setup")]
		public bool isPDiOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDeltaBarre0ForBuyOK", Description="isDeltaBarre0ForBuyOK", Order=3, GroupName="3.Buy_Setup")]
		public bool isDeltaBarre0ForBuyOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDeltaBarre1ForBuyOK", Description="isDeltaBarre1ForBuyOK", Order=4, GroupName="3.Buy_Setup")]
		public bool isDeltaBarre1ForBuyOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDeltaBarre2ForBuyOK", Description="isDeltaBarre2ForBuyOK", Order=5, GroupName="3.Buy_Setup")]
		public bool isDeltaBarre2ForBuyOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isPrecentDeltaBarre0ForBuyOK", Description="isPrecentDeltaBarre0ForBuyOK", Order=6, GroupName="3.Buy_Setup")]
		public bool isPrecentDeltaBarre0ForBuyOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isPrecentDeltaBarre1ForBuyOK", Description="isPrecentDeltaBarre1ForBuyOK", Order=7, GroupName="3.Buy_Setup")]
		public bool isPrecentDeltaBarre1ForBuyOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isPrecentDeltaBarre2ForBuyOK", Description="isPrecentDeltaBarre2ForBuyOK", Order=8, GroupName="3.Buy_Setup")]
		public bool isPrecentDeltaBarre2ForBuyOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDSession1ForBuy", Description="isDSession1ForBuy", Order=9, GroupName="3.Buy_Setup")]
		public bool isDSession1ForBuy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDSession2ForBuy", Description="isDSession2ForBuy", Order=10, GroupName="3.Buy_Setup")]
		public bool isDSession2ForBuy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isEmaForBuy", Description="isEmaForBuy", Order=11, GroupName="3.Buy_Setup")]
		public bool isEmaForBuy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isdelta0_sizeForBuy", Description="isdelta0_sizeForBuy", Order=12, GroupName="3.Buy_Setup")]
		public bool isdelta0_sizeForBuy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDCforBuyok", Description="isDCforBuyok", Order=13, GroupName="3.Buy_Setup")]
		public bool isDCforBuyok { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isPrecentDCforBuyok", Description="isPrecentDCforBuyok", Order=14, GroupName="3.Buy_Setup")]
		public bool isPrecentDCforBuyok { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isTLForBuy", Description="isTLForBuy", Order=15, GroupName="3.Buy_Setup")]
		public bool isTLForBuy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsLowCurrentDayLow0", Description="IsLowCurrentDayLow0", Order=16, GroupName="3.Buy_Setup")]
		public bool IsLowCurrentDayLow0 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsLowCurrentDayLow1", Description="IsLowCurrentDayLow1", Order=17, GroupName="3.Buy_Setup")]
		public bool IsLowCurrentDayLow1 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsLowCurrentDayLow2", Description="IsLowCurrentDayLow2", Order=18, GroupName="3.Buy_Setup")]
		public bool IsLowCurrentDayLow2 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsLowCurrentDayLow6", Description="IsLowCurrentDayLow6", Order=19, GroupName="3.Buy_Setup")]
		public bool IsLowCurrentDayLow6 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsOVBforBuy", Description="IsOVBforBuy", Order=20, GroupName="3.Buy_Setup")]
		public bool IsOVBforBuy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsDeltaSC0SMAforBuy", Description="IsDeltaSC0SMAforBuy", Order=21, GroupName="3.Buy_Setup")]
		public bool IsDeltaSC0SMAforBuy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsBollingerforBuy", Description="IsBollingerforBuy", Order=22, GroupName="3.Buy_Setup")]
		public bool IsBollingerforBuy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isEmaForBuy2", Description="isEmaForBuy2", Order=23, GroupName="3.Buy_Setup")]
		public bool isEmaForBuy2 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isSwing1Buy", Description="isSwing1Buy", Order=24, GroupName="3.Buy_Setup")]
		public bool isSwing1Buy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isSwing2Buy", Description="isSwing2Buy", Order=25, GroupName="3.Buy_Setup")]
		public bool isSwing2Buy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isSwing3Buy", Description="isSwing3Buy", Order=26, GroupName="3.Buy_Setup")]
		public bool isSwing3Buy { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isSwingLowUp12", Description="isSwingLowUp12", Order=27, GroupName="3.Buy_Setup")]
		public bool isSwingLowUp12 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isSwingLowUp23", Description="isSwingLowUp23", Order=28, GroupName="3.Buy_Setup")]
		public bool isSwingLowUp23 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isCloseUP", Description="isCloseUP", Order=29, GroupName="3.Buy_Setup")]
		public bool isCloseUP { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsSelAsobtion", Description="IsSelAsobtion", Order=30, GroupName="3.Buy_Setup")]
		public bool IsSelAsobtion { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isFdeltaLow0OK", Description="isFdeltaLow0OK", Order=31, GroupName="3.Buy_Setup")]
		public bool isFdeltaLow0OK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isOkDeltaLow0GreaterDelta0", Description="isOkDeltaLow0GreaterDelta0", Order=32, GroupName="3.Buy_Setup")]
		public bool isOkDeltaLow0GreaterDelta0 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isOkHigh01", Description="isOkHigh01", Order=33, GroupName="3.Buy_Setup")]
		public bool isOkHigh01 { get; set; }
		
		// ##########  4.Sel_Setup  ########################
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDdOK", Description="isDdOK", Order=1, GroupName="4.Sel_Setup")]
		public bool isDdOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isPDdOK", Description="isPDdOK", Order=2, GroupName="4.Sel_Setup")]
		public bool isPDdOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDeltaBarre0ForSelOK", Description="isDeltaBarre0ForSelOK", Order=3, GroupName="4.Sel_Setup")]
		public bool isDeltaBarre0ForSelOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDeltaBarre1ForSelOK", Description="isDeltaBarre1ForSelOK", Order=4, GroupName="4.Sel_Setup")]
		public bool isDeltaBarre1ForSelOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDeltaBarre2ForSelOK", Description="isDeltaBarre2ForSelOK", Order=5, GroupName="4.Sel_Setup")]
		public bool isDeltaBarre2ForSelOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isPrecentDeltaBarre0ForSelOK", Description="isPrecentDeltaBarre0ForSelOK", Order=6, GroupName="4.Sel_Setup")]
		public bool isPrecentDeltaBarre0ForSelOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isPrecentDeltaBarre1ForSelOK", Description="isPrecentDeltaBarre1ForSelOK", Order=7, GroupName="4.Sel_Setup")]
		public bool isPrecentDeltaBarre1ForSelOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isPrecentDeltaBarre2ForSelOK", Description="isPrecentDeltaBarre2ForSelOK", Order=8, GroupName="4.Sel_Setup")]
		public bool isPrecentDeltaBarre2ForSelOK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDSession1ForDel", Description="isDSession1ForDel", Order=9, GroupName="4.Sel_Setup")]
		public bool isDSession1ForDel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDSession2ForDel", Description="isDSession2ForDel", Order=10, GroupName="4.Sel_Setup")]
		public bool isDSession2ForDel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isEmaForSel", Description="isEmaForSel", Order=11, GroupName="4.Sel_Setup")]
		public bool isEmaForSel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isdelta0_sizeForSel", Description="isdelta0_sizeForSel", Order=12, GroupName="4.Sel_Setup")]
		public bool isdelta0_sizeForSel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isDCforSelok", Description="isDCforSelok", Order=13, GroupName="4.Sel_Setup")]
		public bool isDCforSelok { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isPrecentDCforSelok", Description="isPrecentDCforSelok", Order=14, GroupName="4.Sel_Setup")]
		public bool isPrecentDCforSelok { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isTLForSel", Description="isTLForSel", Order=15, GroupName="4.Sel_Setup")]
		public bool isTLForSel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsHighCurrentDayHigh0", Description="IsHighCurrentDayHigh0", Order=16, GroupName="4.Sel_Setup")]
		public bool IsHighCurrentDayHigh0 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsHighCurrentDayHigh1", Description="IsHighCurrentDayHigh1", Order=17, GroupName="4.Sel_Setup")]
		public bool IsHighCurrentDayHigh1 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsHighCurrentDayHigh2", Description="IsHighCurrentDayHigh2", Order=18, GroupName="4.Sel_Setup")]
		public bool IsHighCurrentDayHigh2 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsHighCurrentDayHigh6", Description="IsHighCurrentDayHigh6", Order=19, GroupName="4.Sel_Setup")]
		public bool IsHighCurrentDayHigh6 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsOVBforSel", Description="IsOVBforSel", Order=20, GroupName="4.Sel_Setup")]
		public bool IsOVBforSel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsDeltaSC0SMAforSel", Description="IsDeltaSC0SMAforSel", Order=21, GroupName="4.Sel_Setup")]
		public bool IsDeltaSC0SMAforSel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsBollingerforSel", Description="IsBollingerforSel", Order=22, GroupName="4.Sel_Setup")]
		public bool IsBollingerforSel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isEmaForSel2", Description="isEmaForSel2", Order=23, GroupName="4.Sel_Setup")]
		public bool isEmaForSel2 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isSwing1Sel", Description="isSwing1Sel", Order=24, GroupName="4.Sel_Setup")]
		public bool isSwing1Sel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isSwing2Sel", Description="isSwing2Sel", Order=25, GroupName="4.Sel_Setup")]
		public bool isSwing2Sel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isSwing3Sel", Description="isSwing3Sel", Order=26, GroupName="4.Sel_Setup")]
		public bool isSwing3Sel { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isSwingHighDown12", Description="isSwingHighDown12", Order=27, GroupName="4.Sel_Setup")]
		public bool isSwingHighDown12 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isSwingHighDown23", Description="isSwingHighDown23", Order=28, GroupName="4.Sel_Setup")]
		public bool isSwingHighDown23 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isCloseDown", Description="isCloseDown", Order=29, GroupName="4.Sel_Setup")]
		public bool isCloseDown { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="IsBuyAsobtion", Description="IsBuyAsobtion", Order=30, GroupName="4.Sel_Setup")]
		public bool IsBuyAsobtion { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isFdeltaHigh0OK", Description="isFdeltaHigh0OK", Order=31, GroupName="4.Sel_Setup")]
		public bool isFdeltaHigh0OK { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isOkDeltaHigh0GreaterDelta0", Description="isOkDeltaHigh0GreaterDelta0", Order=32, GroupName="4.Sel_Setup")]
		public bool isOkDeltaHigh0GreaterDelta0 { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="isOKLow01", Description="isOKLow01", Order=33, GroupName="4.Sel_Setup")]
		public bool isOKLow01 { get; set; }
		
		// ###########  5.VolumeAndATR  #######################
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="VolumeMALength", Order=1, GroupName="5.VolumeAndATR")]
		public int VolumeMALength
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name="AtrThreshold", Order=2, GroupName="5.VolumeAndATR")]
		public double AtrThreshold
		{ get; set; }
		
		[Range(0, double.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "atrThresholdMax", GroupName = "5.VolumeAndATR", Order = 3)]
		public double atrThresholdMax
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="AdxThreshold", Order=4, GroupName="5.VolumeAndATR")]
		public int AdxThreshold
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="EMALength", Order=5, GroupName="5.VolumeAndATR")]
		public int EMALength
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="EMALength2", Order=6, GroupName="5.VolumeAndATR")]
		public int EMALength2
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="smaOvbLength", Order=7, GroupName="5.VolumeAndATR")]
		public int smaOvbLength
		{ get; set; }
		
		// ############  6.DC ######################
		[NinjaScriptProperty]
		[Display(Name="Vol0T", Order=1, GroupName="6.DC")]
		public int Vol0T
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Vol1T", Order=2, GroupName="6.DC")]
		public int Vol1T
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Vol2T", Order=3, GroupName="6.DC")]
		public int Vol2T
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Delta0T", Order=4, GroupName="6.DC")]
		public int Delta0T
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Delta1T", Order=5, GroupName="6.DC")]
		public int Delta1T
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Delta2T", Order=6, GroupName="6.DC")]
		public int Delta2T
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="PDelta0T", Order=7, GroupName="6.DC")]
		public int PDelta0T
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="PDelta1T", Order=8, GroupName="6.DC")]
		public int PDelta1T
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="PDelta2T", Order=9, GroupName="6.DC")]
		public int PDelta2T
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="seuilVolume", Order=10, GroupName="6.DC")]
		public double seuilVolume
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="seuilVolumeMax", Order=11, GroupName="6.DC")]
		public double seuilVolumeMax
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Dsize", Order=12, GroupName="6.DC")]
		public int Dsize
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="DsizeT", Order=13, GroupName="6.DC")]
		public int DsizeT
		{ get; set; }
		
		
		[NinjaScriptProperty]
		[Display(Name="FSelAbsortion", Order=14, GroupName="6.DC")]
		public int FSelAbsortion
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="FdeltaLow0", Order=15, GroupName="6.DC")]
		public int FdeltaLow0
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="FBuyAbsortion", Order=16, GroupName="6.DC")]
		public int FBuyAbsortion
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="FdeltaHigh0", Order=17, GroupName="6.DC")]
		public int FdeltaHigh0
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="FmultipleDelta", Order=18, GroupName="6.DC")]
		public int FmultipleDelta
		{ get; set; }
		
		// ############# 7.TradCount #####################
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="UsetradCount", Description="Use trad Count", Order=1, GroupName="7.TradCount")]
		public bool UsetradCount { get; set; }
		
		[Range(0, 1), NinjaScriptProperty]
		[Display(Name="UseVwapReset", Description="Use Vwap Reset", Order=2, GroupName="7.TradCount")]
		public bool UseVwapReset { get; set; }
		
		#endregion
	}
}
