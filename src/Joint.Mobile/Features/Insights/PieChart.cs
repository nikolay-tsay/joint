namespace Joint.Mobile.Features.Insights;

/// <summary>
/// A ring cut into slices, one per series value, starting at the top and going clockwise.
/// The picked slice is drawn a little thicker. The slices are built from short straight
/// segments, which keeps the start angle and direction in this class's own hands.
/// </summary>
public class PieChart(IReadOnlyList<ChartSeries> slices) : Chart
{
    /// <summary>The hole, as a share of the ring's outer radius.</summary>
    private const float HoleShare = 0.58f;

    /// <summary>How far the picked slice reaches beyond the others.</summary>
    private const float PickedExtra = 7;

    /// <summary>The ring is drawn in steps of about this many degrees.</summary>
    private const float StepDegrees = 3;

    private float Total => (float)slices.Sum(s => s.Values[0]);

    protected override int? IndexAt(PointF point)
    {
        var (center, radius) = Layout(Bounds);
        var total = Total;
        if (total <= 0 || radius <= 0)
        {
            return null;
        }

        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance < (radius * HoleShare) - 12 || distance > radius + 12)
        {
            return null;
        }

        // Clockwise from the top, as a share of the full turn.
        var turn = MathF.Atan2(dx, -dy) / (2 * MathF.PI);
        if (turn < 0)
        {
            turn += 1;
        }

        var reached = 0f;
        for (var index = 0; index < slices.Count; index++)
        {
            reached += (float)slices[index].Values[0] / total;
            if (turn <= reached)
            {
                return index;
            }
        }

        return slices.Count - 1;
    }

    protected override void Render(ICanvas canvas, RectF bounds)
    {
        var (center, radius) = Layout(bounds);
        var total = Total;
        if (total <= 0 || radius <= 0)
        {
            return;
        }

        var from = 0f;
        for (var index = 0; index < slices.Count; index++)
        {
            var sweep = (float)slices[index].Values[0] / total * 360f;
            if (sweep <= 0)
            {
                continue;
            }

            var outer = index == SelectedIndex ? radius + PickedExtra : radius;
            canvas.FillColor = slices[index].Color;
            canvas.FillPath(Ring(center, radius * HoleShare, outer, from, from + sweep));
            from += sweep;
        }
    }

    /// <summary>Leaves room around the ring for the picked slice to grow into.</summary>
    private static (PointF Center, float Radius) Layout(RectF bounds) =>
        (new PointF(bounds.Center.X, bounds.Center.Y), (Math.Min(bounds.Width, bounds.Height) / 2) - PickedExtra - 2);

    /// <summary>A piece of the ring between two angles, in degrees clockwise from the top.</summary>
    private static PathF Ring(PointF center, float inner, float outer, float fromDegrees, float toDegrees)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling((toDegrees - fromDegrees) / StepDegrees));
        var path = new PathF();

        for (var step = 0; step <= steps; step++)
        {
            var point = At(center, outer, fromDegrees + ((toDegrees - fromDegrees) * step / steps));
            if (step == 0)
            {
                path.MoveTo(point);
            }
            else
            {
                path.LineTo(point);
            }
        }

        for (var step = steps; step >= 0; step--)
        {
            path.LineTo(At(center, inner, fromDegrees + ((toDegrees - fromDegrees) * step / steps)));
        }

        path.Close();
        return path;
    }

    private static PointF At(PointF center, float radius, float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        return new PointF(center.X + (radius * MathF.Sin(radians)), center.Y - (radius * MathF.Cos(radians)));
    }
}
