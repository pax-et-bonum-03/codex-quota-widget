using System;
using System.Windows;
using System.Windows.Media;

namespace CodexQuotaWidget;

public enum WidgetSignal { Ready, Refreshing, Error }

public sealed class MechaChrome : FrameworkElement
{
    public static readonly DependencyProperty IsExpandedProperty=DependencyProperty.Register(nameof(IsExpanded),typeof(bool),typeof(MechaChrome),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    public bool IsExpanded { get=>(bool)GetValue(IsExpandedProperty); set=>SetValue(IsExpandedProperty,value); }
    public static readonly DependencyProperty SignalProperty=DependencyProperty.Register(nameof(Signal),typeof(WidgetSignal),typeof(MechaChrome),new FrameworkPropertyMetadata(WidgetSignal.Ready,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FiveWarningProperty=DependencyProperty.Register(nameof(FiveWarning),typeof(bool),typeof(MechaChrome),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty WeekWarningProperty=DependencyProperty.Register(nameof(WeekWarning),typeof(bool),typeof(MechaChrome),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    public WidgetSignal Signal { get=>(WidgetSignal)GetValue(SignalProperty); set=>SetValue(SignalProperty,value); }
    public bool FiveWarning { get=>(bool)GetValue(FiveWarningProperty); set=>SetValue(FiveWarningProperty,value); }
    public bool WeekWarning { get=>(bool)GetValue(WeekWarningProperty); set=>SetValue(WeekWarningProperty,value); }
    private Brush Edge(bool warning=false) => Ink(Signal==WidgetSignal.Error?"#C7FF5B58":Signal==WidgetSignal.Refreshing?"#B9FF9A68":warning?"#B8FF604E":"#98765040");
    internal static SolidColorBrush Ink(string hex)
    {
        var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze(); return brush;
    }
    internal static StreamGeometry FloatingOutline(double w,double h)
    {
        var g=new StreamGeometry();
        using(var c=g.Open())
        {
            c.BeginFigure(new Point(160,0),true,true);
            foreach(var p in new[] { new Point(w-160,0),new Point(w-150,12),new Point(w-32,12),new Point(w-12,32),new Point(w-12,h-56),new Point(w-32,h-36),new Point(w*.55,h-36),new Point(w*.55-22,h-16),new Point(38,h-16),new Point(12,h-42),new Point(12,42),new Point(38,16),new Point(150,16) }) c.LineTo(p,true,false);
        }
        g.Freeze(); return g;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w=ActualWidth,h=ActualHeight;
        if(w<10 || h<10) return;
        if(!IsExpanded)
        {
            // Two open-topped armour plates: no bounding rectangle or decorative widgets.
            double middle = w / 2;
            var left = new StreamGeometry();
            using (var c = left.Open())
            {
                c.BeginFigure(new Point(.5,-1),true,true);
                foreach (var p in new[] { new Point(middle-7,-1),new Point(middle-7,h-10),new Point(middle-18,h-1),new Point(21,h-1),new Point(.5,h-21) }) c.LineTo(p,true,false);
            }
            var right = new StreamGeometry();
            using (var c = right.Open())
            {
                c.BeginFigure(new Point(middle+7,-1),true,true);
                foreach (var p in new[] { new Point(w-.5,-1),new Point(w-.5,h-21),new Point(w-21,h-1),new Point(middle+18,h-1),new Point(middle+7,h-10) }) c.LineTo(p,true,false);
            }
            left.Freeze(); right.Freeze();
            var graphite = new LinearGradientBrush(Ink("#FA24242A").Color,Ink("#FC13161C").Color,90);
            dc.DrawGeometry(graphite,new Pen(Edge(FiveWarning),.8),left);
            dc.DrawGeometry(graphite,new Pen(Edge(WeekWarning),.8),right);
            return;
        }
        var steel=new LinearGradientBrush(Ink("#FA1C2029").Color,Ink("#FA10131A").Color,80);
        dc.DrawRoundedRectangle(steel,new Pen(Signal==WidgetSignal.Ready?Ink("#5C646265"):Edge(),.6),new Rect(18,18,w-36,61),14,14);
        var dock=new StreamGeometry();
        using(var c=dock.Open()) { c.BeginFigure(new Point(w/2-82,0),true,true); c.LineTo(new Point(w/2+82,0),true,false); c.LineTo(new Point(w/2+94,19),true,false); c.LineTo(new Point(w/2-94,19),true,false); }
        dock.Freeze(); dc.DrawGeometry(Ink("#FA1C2029"),null,dock);
        dc.DrawEllipse(new RadialGradientBrush(Ink("#26FF5426").Color,Color.FromArgb(0,255,84,38)),null,new Point(140,208),132,133);
        dc.DrawEllipse(steel,new Pen(Ink("#594B4444"),.6),new Point(140,208),121,121);
        dc.DrawEllipse(new RadialGradientBrush(Ink("#22FF5426").Color,Color.FromArgb(0,255,84,38)),null,new Point(136,214),118,116);
        dc.DrawRoundedRectangle(Ink("#F012151B"),new Pen(Ink("#2E60564B"),.6),new Rect(34,h-55,w-84,34),10,10);
        var trace=new Pen(Ink("#283C484E"),.6);
        dc.DrawLine(trace,new Point(194,77),new Point(w-72,77));
        dc.DrawLine(trace,new Point(226,308),new Point(267,267));
        dc.DrawLine(trace,new Point(267,267),new Point(267,132));
        dc.DrawEllipse(Ink("#684E3E"),null,new Point(267,132),2,2);
        dc.DrawLine(new Pen(Ink("#46FF7544"),1),new Point(16,169),new Point(16,232));
        dc.DrawLine(new Pen(Ink("#AAFF7843"),1.3),new Point(16,187),new Point(16,212));
        dc.DrawLine(new Pen(Ink("#9CFF6634"),1.1),new Point(44,h-21),new Point(101,h-21));
        dc.DrawRectangle(new LinearGradientBrush(Ink("#FFBF7A").Color,Ink("#FF4924").Color,0),null,new Rect(w/2-82,0,164,2));
        for(int i=0;i<3;i++) dc.DrawLine(new Pen(Ink(i==0?"#CCFF7C41":"#68432E"),1.6),new Point(41+i*5,80),new Point(43+i*5,75));
    }
}

public sealed class QuotaDial : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty=DependencyProperty.Register(nameof(Value),typeof(double),typeof(QuotaDial),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HasDataProperty=DependencyProperty.Register(nameof(HasData),typeof(bool),typeof(QuotaDial),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty AccentProperty=DependencyProperty.Register(nameof(Accent),typeof(Brush),typeof(QuotaDial),new FrameworkPropertyMetadata(MechaChrome.Ink("#FF6B32"),FrameworkPropertyMetadataOptions.AffectsRender));
    public double Value { get=>(double)GetValue(ValueProperty); set=>SetValue(ValueProperty,value); }
    public bool HasData { get=>(bool)GetValue(HasDataProperty); set=>SetValue(HasDataProperty,value); }
    public Brush Accent { get=>(Brush)GetValue(AccentProperty); set=>SetValue(AccentProperty,value); }
    private static Point Polar(Point center,double radius,double degrees)
    {
        double a=degrees*Math.PI/180;
        return new Point(center.X+Math.Cos(a)*radius,center.Y+Math.Sin(a)*radius);
    }
    private static StreamGeometry Arc(Point center,double r,double start,double end)
    {
        var g=new StreamGeometry();
        using(var c=g.Open()) { c.BeginFigure(Polar(center,r,start),false,false); c.ArcTo(Polar(center,r,end),new Size(r,r),0,end-start>180,SweepDirection.Clockwise,true,false); }
        g.Freeze(); return g;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var center=new Point(ActualWidth/2,ActualHeight/2);
        double radius=Math.Min(ActualWidth,ActualHeight)/2-15;
        if(radius<15) return;
        var accent=(Accent as SolidColorBrush)?.Color ?? Colors.OrangeRed;
        var glow=new SolidColorBrush(Color.FromArgb(32,accent.R,accent.G,accent.B)); glow.Freeze();
        double thickness=radius>60?4.5:2.7;
        var flow=new LinearGradientBrush(accent,Color.FromRgb(255,183,119),new Point(0,1),new Point(1,0));
        var bright=new Pen(flow,thickness) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round };
        var bloom=new Pen(glow,thickness*3.6) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round };
        dc.DrawGeometry(null,new Pen(MechaChrome.Ink("#4D382F"),thickness),Arc(center,radius,120,420));
        dc.DrawGeometry(null,new Pen(MechaChrome.Ink("#554C4441"),.6),Arc(center,radius-9,123,417));
        for(int i=0;i<66;i++)
        {
            double a=120+i*300/65;
            dc.DrawLine(new Pen(MechaChrome.Ink(i%5==0?"#967356":"#42444043"),i%5==0?.8:.5),Polar(center,radius+7,a),Polar(center,radius+(i%5==0?11:9),a));
        }
        if(HasData && Value>.001)
        {
            var lit=Arc(center,radius,120,120+Math.Clamp(Value,0,100)*3);
            dc.DrawGeometry(null,bloom,lit); dc.DrawGeometry(null,bright,lit);
        }
        if(HasData)
        {
            var marker=Polar(center,radius,120+Math.Clamp(Value,0,100)*3);
            dc.DrawEllipse(glow,null,marker,7,7);
            dc.DrawEllipse(MechaChrome.Ink("#FFE8C7"),null,marker,radius>60?2.8:1.6,radius>60?2.8:1.6);
        }
    }
}

public sealed class TelemetrySweep : FrameworkElement
{
    public static readonly DependencyProperty PhaseProperty=DependencyProperty.Register(nameof(Phase),typeof(double),typeof(TelemetrySweep),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    public double Phase { get=>(double)GetValue(PhaseProperty); set=>SetValue(PhaseProperty,value); }
    protected override void OnRender(DrawingContext dc)
    {
        if(ActualWidth<320 || ActualHeight<100) return;
        dc.PushClip(MechaChrome.FloatingOutline(ActualWidth,ActualHeight));
        double x=194+Phase*(ActualWidth-266);
        var trail=new LinearGradientBrush(Colors.Transparent,MechaChrome.Ink("#B5FF8C49").Color,0);
        dc.DrawRectangle(trail,null,new Rect(x-36,76.6,36,.8));
        dc.DrawEllipse(MechaChrome.Ink("#E4FFC997"),null,new Point(x,77),1.2,1.2);
        dc.Pop();
    }
}

public sealed class MechaMenuColors : System.Windows.Forms.ProfessionalColorTable
{
    private static System.Drawing.Color Dark=>System.Drawing.Color.FromArgb(29,22,24);
    private static System.Drawing.Color Hover=>System.Drawing.Color.FromArgb(87,39,29);
    private static System.Drawing.Color Edge=>System.Drawing.Color.FromArgb(185,80,44);
    public override System.Drawing.Color ToolStripDropDownBackground=>Dark;
    public override System.Drawing.Color MenuBorder=>Edge;
    public override System.Drawing.Color MenuItemBorder=>Edge;
    public override System.Drawing.Color MenuItemSelected=>Hover;
    public override System.Drawing.Color MenuItemSelectedGradientBegin=>Hover;
    public override System.Drawing.Color MenuItemSelectedGradientEnd=>Hover;
    public override System.Drawing.Color ImageMarginGradientBegin=>Dark;
    public override System.Drawing.Color ImageMarginGradientMiddle=>Dark;
    public override System.Drawing.Color ImageMarginGradientEnd=>Dark;
    public override System.Drawing.Color CheckBackground=>Hover;
    public override System.Drawing.Color CheckSelectedBackground=>Hover;
    public override System.Drawing.Color CheckPressedBackground=>Hover;
    public override System.Drawing.Color SeparatorDark=>System.Drawing.Color.FromArgb(80,43,34);
    public override System.Drawing.Color SeparatorLight=>Dark;
}
