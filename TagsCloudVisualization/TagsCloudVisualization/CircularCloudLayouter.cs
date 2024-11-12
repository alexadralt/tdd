using System.Drawing;

namespace TagsCloudVisualization;

public class CircularCloudLayouter
{
    private List<Rectangle> generatedLayout = new();
    private Point cloudCenter;
    private double nextAngle;

    private static readonly double angleStep = Math.PI / 2;
    private static readonly float tracingStep = 0.01f;
    private static readonly float maxTracingDistance = 500f;
    
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

        var nextRectangle = GetNextRectangle(rectangleSize);
        generatedLayout.Add(nextRectangle);
        return nextRectangle;
    }

    private Rectangle GetNextRectangle(Size rectangleSize)
    {
        Rectangle? result = null;
        while (result == null)
        {
            var direction = GetNextDirection();
            var step = 0.0f;
            while (step < 1f && result == null)
            {
                (step, var availablePos) = FindNextAvailablePosByTracingLine(direction, step);
                result = TryFindGoodRectanglePosition(availablePos, rectangleSize);
            }
        }

        return result.Value;
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

    private (float, Point) FindNextAvailablePosByTracingLine(PointF direction, float startingStep = 0.0f)
    {
        var nextPos = new PointF(
            cloudCenter.X + direction.X * tracingStep,
            cloudCenter.Y + direction.Y * tracingStep);
        var currentStep = startingStep == 0.0f ? tracingStep : startingStep;
        var notInRectangle = false;
        while (!notInRectangle)
        {
            notInRectangle = generatedLayout.All(rect => !rect.Contains(Point.Truncate(nextPos)));
            currentStep += tracingStep;
            nextPos = new PointF(
                cloudCenter.X + direction.X * currentStep,
                cloudCenter.Y + direction.Y * currentStep);
        }

        return (currentStep, Point.Truncate(nextPos));
    }

    private PointF GetNextDirection()
    {
        var x = (float)Math.Cos(nextAngle);
        var y = (float)Math.Sin(nextAngle);
        nextAngle += angleStep;
        return new PointF(x, y);
    }
}