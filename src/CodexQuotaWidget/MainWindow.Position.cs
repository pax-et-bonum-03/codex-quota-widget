using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CodexQuotaWidget;

public partial class MainWindow
{
    private Point anchorPixels, dragStartPointer, dragStartAnchor;
    private bool placementReady, positioning, dragPending, dragging, opensUpwards, defaultDock = true;
    private readonly bool previewMode;
    private readonly DispatcherTimer dragTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private static string PlacementPath => Path.Combine(WidgetPaths.DataDirectory, "position.json");

    private void InitializePlacement()
    {
        var primary = Forms.Screen.PrimaryScreen!.Bounds;
        anchorPixels = new Point(primary.Left + primary.Width / 2d, primary.Top);
        if (!previewMode && WindowPlacement.Load(PlacementPath) is Point saved) { anchorPixels = saved; defaultDock = false; }
        placementReady = true;
        NormalizeAnchor();
        ApplyWindowPosition();
    }

    private Rect PlacementBounds()
    {
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)Math.Round(anchorPixels.X), (int)Math.Round(anchorPixels.Y)));
        var area = defaultDock ? screen.Bounds : screen.WorkingArea;
        return new Rect(area.Left, area.Top, area.Width, area.Height);
    }

    private void NormalizeAnchor()
    {
        var bounds = PlacementBounds();
        var dpi = VisualTreeHelper.GetDpi(this);
        double halfWidth = FoldedWidth * dpi.DpiScaleX / 2, height = FoldedHeight * dpi.DpiScaleY;
        anchorPixels = new Point(Math.Clamp(anchorPixels.X, bounds.Left + halfWidth, Math.Max(bounds.Left + halfWidth, bounds.Right - halfWidth)),
            Math.Clamp(anchorPixels.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - height)));
    }

    private void ChooseExpansionDirection()
    {
        if (!placementReady) return;
        opensUpwards = anchorPixels.Y + OpenHeight * VisualTreeHelper.GetDpi(this).DpiScaleY > PlacementBounds().Bottom;
    }

    private void ApplyWindowPosition()
    {
        if (!placementReady || positioning) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var location = WindowPlacement.Calculate(anchorPixels, Width * dpi.DpiScaleX, Height * dpi.DpiScaleY,
            FoldedHeight * dpi.DpiScaleY, PlacementBounds(), opensUpwards);
        positioning = true;
        try { SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(location.X), (int)Math.Round(location.Y), 0, 0, 0x15); } // keep size, z-order and active app
        finally { positioning = false; }
    }

    private void SetWindowBounds(double width, double height)
    {
        positioning = true;
        try
        {
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            Width = width;
            Height = height;
            var handle = new WindowInteropHelper(this).Handle;
            if (!placementReady || handle == IntPtr.Zero) return;
            var dpi = VisualTreeHelper.GetDpi(this);
            double physicalWidth = Math.Round(width * dpi.DpiScaleX);
            double physicalHeight = Math.Round(height * dpi.DpiScaleY);
            var location = WindowPlacement.Calculate(anchorPixels, physicalWidth, physicalHeight,
                Math.Round(FoldedHeight * dpi.DpiScaleY), PlacementBounds(), opensUpwards);
            // Move and size in one native transaction without changing z-order or focus.
            SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(location.X), (int)Math.Round(location.Y),
                (int)physicalWidth, (int)physicalHeight, 0x14);
        }
        finally { positioning = false; }
    }

    private static bool IsInteractiveSource(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ButtonBase or MenuItem or TextBoxBase or Selector) return true;
            source = source is Visual or Visual3D ? VisualTreeHelper.GetParent(source)
                : source is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    private static Point PointerPixels()
    {
        GetCursorPos(out var pointer);
        return new Point(pointer.X, pointer.Y);
    }

    private void DragPress(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveSource(e.OriginalSource as DependencyObject) || menu?.IsOpen == true || choosingLanguagePack) return;
        if (e.ClickCount == 2) { RestorePosition(); e.Handled = true; return; }
        BeginPointerDrag(PointerPixels());
        e.Handled = true;
    }

    private void BeginPointerDrag(Point pointer)
    {
        SetExpanded(expanded, false); // Finish resizing before capturing the drag origin.
        foldTimer.Stop();
        dragStartPointer = pointer; dragStartAnchor = anchorPixels;
        dragPending = true;
        _ = Mouse.Capture(this, CaptureMode.Element);
        // A non-activating window can lose capture outside its bounds. Poll only during the user's drag.
        dragTimer.Start();
    }

    private static bool LeftButtonPressed => (GetAsyncKeyState(1) & 0x8000) != 0;
    private void DragTimerTick(object? sender, EventArgs e)
    {
        if (!dragPending || !LeftButtonPressed) { EndPointerDrag(); return; }
        UpdatePointerDrag(PointerPixels());
    }

    private void UpdatePointerDrag(Point pointer)
    {
        if (!dragPending) return;
        var delta = pointer - dragStartPointer;
        var dpi = VisualTreeHelper.GetDpi(this);
        if (!dragging && Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance * dpi.DpiScaleX && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance * dpi.DpiScaleY) return;
        dragging = true; defaultDock = false;
        anchorPixels = dragStartAnchor + delta;
        ApplyWindowPosition();
    }

    private void EndPointerDrag()
    {
        bool moved = dragging;
        dragTimer.Stop();
        dragPending = false; dragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (moved)
        {
            NormalizeAnchor(); ChooseExpansionDirection(); ApplyWindowPosition();
            if (!previewMode) WindowPlacement.Save(PlacementPath, anchorPixels);
        }
        if (!IsMouseOver) { foldTimer.Stop(); foldTimer.Start(); }
    }

    public void RestorePosition()
    {
        EndPointerDrag();
        menu?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
        foldTimer.Stop();
        var primary = Forms.Screen.PrimaryScreen!.Bounds;
        anchorPixels = new Point(primary.Left + primary.Width / 2d, primary.Top);
        defaultDock = true; opensUpwards = false;
        SetExpanded(false, false); ApplyWindowPosition();
        if (!previewMode) WindowPlacement.Save(PlacementPath, null);
    }

    private void RestoreClick(object sender, RoutedEventArgs e) => RestorePosition();
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
