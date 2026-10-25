namespace MiniStock.Controls;

public sealed class LucideIcon : GraphicsView
{
    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(LucideIcon), "search",
            propertyChanged: static (bindable, _, _) => ((LucideIcon)bindable).Invalidate());

    public static readonly BindableProperty IconColorProperty =
        BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(LucideIcon), Colors.Transparent,
            propertyChanged: static (bindable, _, _) => ((LucideIcon)bindable).Invalidate());

    public LucideIcon()
    {
        Drawable = new LucideDrawable(this);
        InputTransparent = true;
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    private sealed class LucideDrawable(LucideIcon owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF bounds)
        {
            canvas.StrokeSize = 2;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.StrokeColor = owner.IconColor;

            var scale = Math.Min(bounds.Width, bounds.Height) / 24f;
            canvas.SaveState();
            canvas.Translate(bounds.Center.X - 12 * scale, bounds.Center.Y - 12 * scale);
            canvas.Scale(scale, scale);

            switch (owner.Icon)
            {
                case "search":
                    canvas.DrawEllipse(3, 3, 16, 16);
                    canvas.DrawLine(16, 16, 21, 21);
                    break;
                case "house":
                    canvas.DrawLine(3, 11, 12, 3);
                    canvas.DrawLine(12, 3, 21, 11);
                    canvas.DrawLine(5, 10, 5, 21);
                    canvas.DrawLine(19, 10, 19, 21);
                    canvas.DrawLine(5, 21, 19, 21);
                    canvas.DrawLine(9, 21, 9, 14);
                    canvas.DrawLine(15, 21, 15, 14);
                    canvas.DrawLine(9, 14, 15, 14);
                    break;
                case "arrow-left-right":
                    canvas.DrawLine(3, 8, 21, 8);
                    canvas.DrawLine(17, 4, 21, 8);
                    canvas.DrawLine(17, 12, 21, 8);
                    canvas.DrawLine(21, 16, 3, 16);
                    canvas.DrawLine(7, 12, 3, 16);
                    canvas.DrawLine(7, 20, 3, 16);
                    break;
                case "arrow-down-left":
                    canvas.DrawLine(17, 7, 7, 17);
                    canvas.DrawLine(7, 7, 7, 17);
                    canvas.DrawLine(17, 17, 7, 17);
                    break;
                case "arrow-up-right":
                    canvas.DrawLine(7, 17, 17, 7);
                    canvas.DrawLine(7, 7, 17, 7);
                    canvas.DrawLine(17, 17, 17, 7);
                    break;
                case "package":
                    canvas.DrawRectangle(3, 5, 18, 14);
                    canvas.DrawLine(3, 6, 12, 11);
                    canvas.DrawLine(21, 6, 12, 11);
                    canvas.DrawLine(12, 11, 12, 21);
                    canvas.DrawLine(8, 3, 12, 5);
                    canvas.DrawLine(16, 3, 12, 5);
                    break;
                case "plus":
                    canvas.DrawLine(12, 5, 12, 19);
                    canvas.DrawLine(5, 12, 19, 12);
                    break;
                case "pencil":
                    canvas.DrawLine(3, 21, 7, 20);
                    canvas.DrawLine(7, 20, 20, 7);
                    canvas.DrawLine(17, 4, 20, 7);
                    canvas.DrawLine(14, 7, 17, 10);
                    break;
                case "arrow-left":
                    canvas.DrawLine(5, 12, 19, 12);
                    canvas.DrawLine(5, 12, 12, 5);
                    canvas.DrawLine(5, 12, 12, 19);
                    break;
                case "chevron-right":
                    canvas.DrawLine(9, 6, 15, 12);
                    canvas.DrawLine(15, 12, 9, 18);
                    break;
                case "mail":
                    canvas.DrawRectangle(2, 4, 20, 16);
                    canvas.DrawLine(3, 6, 12, 13);
                    canvas.DrawLine(21, 6, 12, 13);
                    break;
                case "lock":
                    canvas.DrawRectangle(3, 10, 18, 11);
                    canvas.DrawArc(7, 3, 10, 10, 180, 180, true, false);
                    canvas.DrawEllipse(11, 14, 2, 2);
                    break;
                case "eye":
                    canvas.DrawEllipse(3, 6, 18, 12);
                    canvas.DrawEllipse(9, 9, 6, 6);
                    break;
                case "eye-off":
                    canvas.DrawEllipse(3, 6, 18, 12);
                    canvas.DrawEllipse(9, 9, 6, 6);
                    canvas.DrawLine(2, 2, 22, 22);
                    break;
                case "bell":
                    canvas.DrawArc(5, 3, 14, 14, 180, 180, true, false);
                    canvas.DrawLine(3, 17, 21, 17);
                    canvas.DrawLine(10, 21, 14, 21);
                    break;
                case "triangle-alert":
                    canvas.DrawLine(12, 3, 22, 21);
                    canvas.DrawLine(22, 21, 2, 21);
                    canvas.DrawLine(2, 21, 12, 3);
                    canvas.DrawLine(12, 9, 12, 15);
                    canvas.DrawEllipse(11.5f, 17.5f, 1, 1);
                    break;
                case "settings":
                    canvas.DrawEllipse(8, 8, 8, 8);
                    canvas.DrawEllipse(10.5f, 10.5f, 3, 3);
                    canvas.DrawLine(12, 2, 12, 6);
                    canvas.DrawLine(12, 18, 12, 22);
                    canvas.DrawLine(2, 12, 6, 12);
                    canvas.DrawLine(18, 12, 22, 12);
                    break;
                case "clipboard-check":
                    canvas.DrawRectangle(5, 4, 14, 18);
                    canvas.DrawRectangle(9, 2, 6, 4);
                    canvas.DrawLine(8, 13, 11, 16);
                    canvas.DrawLine(11, 16, 17, 10);
                    break;
                case "calendar":
                    canvas.DrawRectangle(3, 5, 18, 16);
                    canvas.DrawLine(3, 10, 21, 10);
                    canvas.DrawLine(7, 2, 7, 7);
                    canvas.DrawLine(17, 2, 17, 7);
                    break;
                case "history":
                    canvas.DrawEllipse(4, 4, 16, 16);
                    canvas.DrawLine(12, 8, 12, 13);
                    canvas.DrawLine(12, 13, 17, 16);
                    break;
                case "info":
                    canvas.DrawEllipse(2, 2, 20, 20);
                    canvas.DrawLine(12, 11, 12, 17);
                    canvas.DrawEllipse(11.5f, 7, 1, 1);
                    break;
                case "user":
                    canvas.DrawEllipse(8, 3, 8, 8);
                    canvas.DrawArc(4, 12, 16, 10, 180, 180, true, false);
                    break;
                case "store":
                    canvas.DrawRectangle(3, 9, 18, 12);
                    canvas.DrawLine(3, 9, 6, 3);
                    canvas.DrawLine(6, 3, 18, 3);
                    canvas.DrawLine(18, 3, 21, 9);
                    canvas.DrawLine(9, 21, 9, 15);
                    canvas.DrawLine(15, 21, 15, 15);
                    canvas.DrawLine(9, 15, 15, 15);
                    break;
                case "phone":
                    canvas.DrawLine(7, 3, 11, 7);
                    canvas.DrawLine(11, 7, 8, 10);
                    canvas.DrawLine(8, 10, 14, 16);
                    canvas.DrawLine(14, 16, 17, 13);
                    canvas.DrawLine(17, 13, 21, 17);
                    break;
                case "wallet":
                    canvas.DrawRectangle(3, 6, 18, 14);
                    canvas.DrawLine(3, 6, 18, 6);
                    canvas.DrawRectangle(14, 11, 7, 5);
                    break;
            }

            canvas.RestoreState();
        }
    }
}
