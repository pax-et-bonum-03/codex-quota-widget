using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodexQuotaWidget;

public partial class MainWindow
{
    private bool refreshSweepShown;

    private void SetMiniQuota(TextBlock block,string key,Quota? quota)
    {
        // Preserve the existing {0} language-pack contract, including its word order.
        string number=Percent(quota);
        var fragments=L(key,"\uFFF9").Split('\uFFF9');
        var labelFamily=(FontFamily)FindResource("MiniDateFont");
        var numberFamily=(FontFamily)FindResource("MiniFont");
        var labelInk=MechaChrome.Ink("#ADA49C");
        var numberInk=QuotaColor(quota);
        var labelFace=new Typeface(labelFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
        var numberFace=new Typeface(numberFamily,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);
        double pixelsPerDip=VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double WidthOf(string text,Typeface face,double size) => new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,face,size,labelInk,pixelsPerDip).WidthIncludingTrailingWhitespace;
        double labelSize=11;
        double numbersWidth=WidthOf(number,numberFace,14)*(fragments.Length-1);
        double labelsWidth=fragments.Sum(part=>WidthOf(part,labelFace,labelSize));
        double available=Math.Max(0,103-numbersWidth-1); // reserve one DIP for layout rounding
        if(labelsWidth>available)
        {
            labelSize=9.5;
            labelsWidth=fragments.Sum(part=>WidthOf(part,labelFace,labelSize));
        }
        string FitLabel(string text,double budget)
        {
            if(WidthOf(text,labelFace,labelSize)<=budget) return text;
            if(WidthOf("…",labelFace,labelSize)>budget) return "";
            var boundaries=StringInfo.ParseCombiningCharacters(text);
            int low=0,high=boundaries.Length;
            while(low<high)
            {
                int count=(low+high+1)/2;
                int end=count==boundaries.Length?text.Length:boundaries[count];
                if(WidthOf(text[..end]+"…",labelFace,labelSize)<=budget) low=count;
                else high=count-1;
            }
            int length=low==boundaries.Length?text.Length:boundaries[low];
            return text[..length]+"…";
        }
        block.Inlines.Clear();
        for(int i=0;i<fragments.Length;i++)
        {
            string label=fragments[i];
            if(labelsWidth>available)
                label=FitLabel(label,labelsWidth==0?0:available*WidthOf(label,labelFace,labelSize)/labelsWidth);
            block.Inlines.Add(new Run(label) { FontFamily=labelFamily,FontSize=labelSize,FontWeight=FontWeights.Normal,Foreground=labelInk });
            if(i<fragments.Length-1)
                block.Inlines.Add(new Run(number) { FontFamily=numberFamily,FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=numberInk });
        }
        block.ToolTip=L(key,number)+"\n"+ResetTooltip(quota);
    }

    private void UpdateFeedback()
    {
        ArmorChrome.Signal=refreshing?WidgetSignal.Refreshing:displayError!=null?WidgetSignal.Error:WidgetSignal.Ready;
        ArmorChrome.FiveWarning=five!=null && Metrics.Remaining(five)<=20;
        ArmorChrome.WeekWarning=week!=null && Metrics.Remaining(week)<=20;
        if(!expanded || displayError!=null || !SystemParameters.ClientAreaAnimation)
        {
            HudSweep.BeginAnimation(TelemetrySweep.PhaseProperty,null);
            HudSweep.Visibility=Visibility.Collapsed;
            return;
        }
        if(!refreshing || refreshSweepShown) return;
        refreshSweepShown=true;
        HudSweep.Visibility=Visibility.Visible;
        var sweep=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(900)) { FillBehavior=FillBehavior.Stop };
        sweep.Completed+=(_,_)=> { HudSweep.BeginAnimation(TelemetrySweep.PhaseProperty,null); HudSweep.Visibility=Visibility.Collapsed; };
        HudSweep.BeginAnimation(TelemetrySweep.PhaseProperty,sweep);
    }
}
