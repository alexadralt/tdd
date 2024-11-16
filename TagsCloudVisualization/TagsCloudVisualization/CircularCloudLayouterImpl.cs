using System.Drawing;

namespace TagsCloudVisualization;

public class CircularCloudLayouter
{
    
    private static readonly float TracingStep = 0.01f;
    private static readonly float MaxTracingDistance = 1000f;
    private static readonly int MaxCycleCount = 6;
    
    private List<Rectangle> _generatedLayout = new();
    private readonly Point _cloudCenter;
    private double _nextAngle;
    private double _angleStep = Math.PI / 2;
    private int _currentCycle;
    private int CurrentCycle
    {
        get => _currentCycle;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (value < MaxCycleCount)
            {
                _currentCycle = value;
                _angleStep = Math.PI / Math.Pow(2, value);
            }
        }
    }
    
    public CircularCloudLayouter(Point center)
    {
        _cloudCenter = center;
    }

    public Rectangle PutNextRectangle(Size rectangleSize)
    {
        if (rectangleSize.Width <= 0 || rectangleSize.Height <= 0)
            throw new ArgumentException("rectangle width and height must be greater than 0");
        
        if (_generatedLayout.Count == 0)
        {
            var rectangle = new Rectangle(
                _cloudCenter.X - rectangleSize.Width / 2,
                _cloudCenter.Y - rectangleSize.Height / 2,
                rectangleSize.Width,
                rectangleSize.Height);
            _generatedLayout.Add(rectangle);
            return rectangle;
        }

        var nextRectangle = GetNextRectangle(rectangleSize);
        _generatedLayout.Add(nextRectangle);
        return nextRectangle;
    }

    private Rectangle GetNextRectangle(Size rectangleSize)
    {
        Rectangle result = Rectangle.Empty;
        var found = false;
        while (!found)
        {
            var direction = GetNextDirection();
            var step = 0.0f;
            while (step < 1f && !found)
            {
                (step, var availablePos) = FindNextAvailablePosByTracingLine(direction, step);
                found = TryFindGoodRectanglePosition(availablePos, rectangleSize, out result);
            }
        }

        return result;
    }

    private bool TryFindGoodRectanglePosition(Point posToPlace, Size rectangleSize, out Rectangle result)
    {
        var possibleOptions = new Rectangle[]
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
        
        foreach (var option in possibleOptions)
        {
            bool intersects = false;
            foreach (var rectangle in _generatedLayout)
            {
                if (rectangle.IntersectsWith(option))
                {
                    intersects = true;
                    break;
                }
            }

            if (!intersects)
            {
                result = option;
                return true;
            }
        }
        
        result = Rectangle.Empty;
        return false;
    }

    private (float, Point) FindNextAvailablePosByTracingLine(PointF direction, float startingStep = 0.0f)
    {
        var nextPos = new PointF(
            _cloudCenter.X + direction.X * MaxTracingDistance * TracingStep,
            _cloudCenter.Y + direction.Y * MaxTracingDistance * TracingStep);
        var currentStep = startingStep == 0.0f ? TracingStep : startingStep;
        var notInRectangle = false;
        while (!notInRectangle)
        {
            notInRectangle = true;
            foreach (var rectangle in _generatedLayout)
            {
                if (rectangle.ContainsFloat(nextPos))
                {
                    notInRectangle = false;
                    break;
                }
            }
            currentStep += TracingStep;
            nextPos = new PointF(
                _cloudCenter.X + direction.X * MaxTracingDistance * currentStep,
                _cloudCenter.Y + direction.Y * MaxTracingDistance * currentStep);
        }

        return (currentStep, Point.Truncate(nextPos));
    }

    private PointF GetNextDirection()
    {
        var x = (float)Math.Cos(_nextAngle);
        var y = (float)Math.Sin(_nextAngle);
        _nextAngle += _angleStep;
        if (Math.Abs(_nextAngle - Math.PI * 2) < 1e-12f)
        {
            _nextAngle = 0;
            CurrentCycle++;
        }
        return new PointF(x, y);
    }
}