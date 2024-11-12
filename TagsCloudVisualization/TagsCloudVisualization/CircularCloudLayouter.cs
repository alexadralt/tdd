using System.Drawing;

namespace TagsCloudVisualization;

public class CircularCloudLayouter
{
    private List<Rectangle> generatedLayout = new();
    private Point cloudCenter;
    private double nextAngle;

    private static readonly double angleStep = Math.PI / 8;
    private static readonly double tracingStep = 1;
    
    public CircularCloudLayouter(Point center)
    {
        cloudCenter = center;
    }

    public Rectangle PutNextRectangle(Size rectangleSize)
    {
        if (rectangleSize.Width <= 0 || rectangleSize.Height <= 0)
            throw new ArgumentException("rectangle width and height must be greater than 0");
        
        if (generatedLayout.Count == 0)
        {
            var rectangle = new Rectangle(
                cloudCenter.X - rectangleSize.Width / 2,
                cloudCenter.Y - rectangleSize.Height / 2,
                rectangleSize.Width,
                rectangleSize.Height);
            generatedLayout.Add(rectangle);
            return rectangle;
        }

        Rectangle? result = null;
        while (!result.HasValue)
        {
            var nextPos = GetNextPosition();
            var (hitRect, hitPoint) = TraceLineToCenter(nextPos);
            result = TryPlaceRectangleNearHitPoint(rectangleSize, hitRect, hitPoint);
        }
        
        generatedLayout.Add(result.Value);
        return result.Value;
    }

    private Rectangle? TryPlaceRectangleNearHitPoint(Size rectangleSize, Rectangle hitRectangle, PointF hitPoint)
    {
        var differences = new PointF[]
        {
            new PointF(hitRectangle.X - hitPoint.X, hitRectangle.Y - hitPoint.Y),
            new PointF(hitRectangle.Right - hitPoint.X, hitRectangle.Y - hitPoint.Y),
            new PointF(hitRectangle.X - hitPoint.X, hitRectangle.Bottom - hitPoint.Y),
            new PointF(hitRectangle.Right - hitPoint.X, hitRectangle.Bottom - hitPoint.Y),
        };
        var min = differences.MinBy(point => Math.Pow(point.X, 2) + Math.Pow(point.Y, 2));
        var posToPlace = Point.Truncate(new PointF(hitPoint.X + min.X, hitPoint.Y + min.Y));

        return TryFindGoodRectanglePosition(posToPlace, rectangleSize);
    }

    private Rectangle? TryFindGoodRectanglePosition(Point posToPlace, Size rectangleSize)
    {
        var possibleOptions = new Rectangle?[]
        {
            new Rectangle(
                posToPlace.X,
                posToPlace.Y,
                rectangleSize.Width,
                rectangleSize.Height),
            new Rectangle(
                posToPlace.X - rectangleSize.Width,
                posToPlace.Y,
                rectangleSize.Width,
                rectangleSize.Height),
            new Rectangle(
                posToPlace.X,
                posToPlace.Y - rectangleSize.Height,
                rectangleSize.Width,
                rectangleSize.Height),
            new Rectangle(
                posToPlace.X - rectangleSize.Width,
                posToPlace.Y - rectangleSize.Height,
                rectangleSize.Width,
                rectangleSize.Height)
        };
        
        Rectangle? best = possibleOptions
            .FirstOrDefault(rect => generatedLayout
                .All(generatedRect => !rect?.IntersectsWith(generatedRect) ?? false),
                null);
        
        return best;
    }

    private (Rectangle, PointF) TraceLineToCenter(PointF from)
    {
        var difference = new PointF(cloudCenter.X - from.X, cloudCenter.Y - from.Y);
        var currentStep = 0.1d;
        while (currentStep < 1.0d)
        {
            var tracingPos = new PointF(
                (float)(from.X + difference.X * currentStep),
                (float)(from.Y + difference.Y * currentStep));
            foreach (var rect in generatedLayout)
            {
                if (rect.Contains(Point.Truncate(tracingPos)))
                    return (rect, tracingPos);
            }
            currentStep += 0.1;
        }
        
        return (generatedLayout[0], cloudCenter);
    }

    private PointF GetNextPosition()
    {
        //var radius = 500d / (2 * Math.PI) * nextAngle;
        var x = (float)(500d * Math.Cos(nextAngle) + cloudCenter.X);
        var y = (float)(500d * Math.Sin(nextAngle) + cloudCenter.Y);

        nextAngle *= -1;
        if (nextAngle < 0)
            nextAngle -= angleStep;
        else
            nextAngle += angleStep;
        return new PointF(x, y);
    }
}