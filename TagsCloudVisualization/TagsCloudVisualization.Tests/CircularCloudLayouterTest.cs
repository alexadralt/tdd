using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using TagsCloudVisualization;

namespace TagsCloudVisualization.Tests;

[TestFixture]
[TestOf(typeof(CircularCloudLayouter))]
public class CircularCloudLayouterTest
{

    [Test]
    [Description("Проверяем, что прямоугольники не пересекаются друг с другом")]
    [TestCaseSource(nameof(IntersectionTestSource))]
    public void RectanglesShouldNotIntersectEachOther(
        int centerX,
        int centerY,
        params (int, int)[] sizes)
    {
        var circularCloudLayouter = Arrange(centerX, centerY);

        var rectangles = GenerateLayout(sizes, circularCloudLayouter);

        Assert_RectanglesDoNotIntersect(rectangles);
    }

    [Test]
    [Description("Проверяем, что центр прямоугольника, размер которого был передан первым " +
                 "совпадает с центром, переданным в аргумент конструктора CircularCloudLayouter")]
    [TestCaseSource(nameof(FirstRectanglePositionTestSource))]
    public void FirstRectangleShouldBePositionedAtProvidedCenter(
        int centerX,
        int centerY,
        params (int, int)[] sizes)
    {
        var circularCloudLayouter = Arrange(centerX, centerY);

        var rectangles = GenerateLayout(sizes, circularCloudLayouter);
        
        Assert_FirstRectangleIsPositionedAtProvidedCenter(rectangles, new Point(centerX, centerY));
    }

    [Test]
    [Description("Проверяем, что прямоугольники расположены наиболее плотно, " +
                 "то есть максимум из попарных расстояний между центрами " +
                 "прямоугольников не превышает maxDistance")]
    [TestCaseSource(nameof(DensityTestSource))]
    public void RectanglesShouldBeCloseToEachOther(
        int centerX,
        int centerY,
        int maxDistance,
        params (int, int)[] sizes)
    {
        var circularCloudLayouter = Arrange(centerX, centerY);

        var rectangles = GenerateLayout(sizes, circularCloudLayouter);
        
        Assert_RectanglesArePositionedCloseToEachOther(rectangles, maxDistance);
    }

    [Test]
    [Description("Проверяем, что общий центр масс всех прямоугольников находится " +
                 "рядом с центром, переданным в конструктор CircularCloudLayouter")]
    [TestCaseSource(nameof(CenterTestSource))]
    public void RectanglesCommonBarycenterIsCloseToTheProvidedCenter(
        int centerX,
        int centerY,
        params (int, int)[] sizes)
    {
        var circularCloudLayouter = Arrange(centerX, centerY);

        var rectangles = GenerateLayout(sizes, circularCloudLayouter);
        
        Assert_RectanglesBarycenterIsCloseToCenter(rectangles, new Point(centerX, centerY));
    }
    
    private static CircularCloudLayouter Arrange(int centerX, int centerY)
    {
        return new CircularCloudLayouter(new Point(centerX, centerY));
    }

    private readonly struct RectanglePair(Rectangle rectangle1, Rectangle rectangle2)
    {
        public Rectangle Rectangle1 { get; } = rectangle1;
        public Rectangle Rectangle2 { get; } = rectangle2;
        public bool HasNoIntersections() => !Rectangle1.IntersectsWith(Rectangle2);

        public bool DistanceIsNotGreaterThan(int expectedDistance)
        {
            var center1 = RectangleCenter(Rectangle1);
            var center2 = RectangleCenter(Rectangle2);
            var actualDistance = SquaredDistance(center1, center2);
            return actualDistance <= expectedDistance;
        }
    }

    private static Point RectangleCenter(Rectangle rectangle)
    {
        return new Point((rectangle.X + rectangle.Right) / 2, (rectangle.Y + rectangle.Bottom) / 2);
    }

    private static int SquaredDistance(Point p1, Point p2)
    {
        return (int)(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
    }

    private IEnumerable<Rectangle> GenerateLayout(
        (int, int)[] sizes,
        CircularCloudLayouter circularCloudLayouter)
    {
        return sizes
            .Select(((int w, int h) s) =>
                circularCloudLayouter.PutNextRectangle(new Size(s.w, s.h)));
    }

    private IEnumerable<RectanglePair> GetAllPossibleRectanglePairs(IEnumerable<Rectangle> rectangles)
    {
        var rectangleList = rectangles.ToList();
        return rectangleList
            .SelectMany(rect1 => rectangleList.Select(rect2 => new RectanglePair(rect1, rect2)))
            .Where(pair => pair.Rectangle1 != pair.Rectangle2);
    }

    private void Assert_RectanglesDoNotIntersect(IEnumerable<Rectangle> rectangles)
    {
        GetAllPossibleRectanglePairs(rectangles)
            .Should()
            .OnlyContain(pair => pair.HasNoIntersections());
    }

    private void Assert_FirstRectangleIsPositionedAtProvidedCenter(IEnumerable<Rectangle> rectangles, Point center)
    {
        RectangleCenter(rectangles.First())
            .Should()
            .BeEquivalentTo(center);
    }

    private void Assert_RectanglesArePositionedCloseToEachOther(
        IEnumerable<Rectangle> rectangles,
        int maxDistance)
    {
        GetAllPossibleRectanglePairs(rectangles)
            .Should()
            .OnlyContain(pair => pair.DistanceIsNotGreaterThan(maxDistance));
    }

    private void Assert_RectanglesBarycenterIsCloseToCenter(
        IEnumerable<Rectangle> rectangles,
        Point center)
    {
        var (totalX, totalY, count) = rectangles
            .Aggregate((0, 0, 0), ((int totalX, int totalY, int count) res, Rectangle rect) =>
            {
                var rectCenter = RectangleCenter(rect);
                return (res.totalX + rectCenter.X, res.totalY + rectCenter.Y, ++res.count);
            });
        var barycenter = new Point(totalX / count, totalY / count);
        var deviationFromCenter = SquaredDistance(barycenter, center);
        deviationFromCenter.Should().BeLessOrEqualTo(100);
    }

    private static object[][] IntersectionTestSource()
    {
        return
        [
            [
                0, 0,
                (1, 2),
                (3, 4),
                (5, 6),
                (7, 8)
            ],
            [
                0, 0,
                (1, 1),
                (1, 1),
                (2, 2),
                (2, 2)
            ],
            [
                3, 3,
                (1, 2),
                (2, 1),
                (4, 5),
                (6, 5),
                (3, 2),
                (2, 2)
            ]
        ];
    }

    private static object[][] FirstRectanglePositionTestSource()
    {
        return
        [
            [
                0, 0,
                (1, 2),
                (2, 3),
                (4, 5)
            ],
            [
                1, 5,
                (4, 4),
                (3, 3),
                (2, 2)
            ],
            [
                -2, 3,
                (3, 3)
            ]
        ];
    }

    private static object[][] DensityTestSource()
    {
        return
        [
            [
                0, 0, 25,
                (2, 2),
                (3, 3),
                (4, 4)
            ],
            [
                0, 0, 25,
                (2, 2),
                (4, 4),
                (3, 3)
            ],
            [
                0, 0, 36,
                (4, 4),
                (3, 3),
                (2, 2),
                (1, 1)
            ],
            [
                0, 0, 25,
                (2, 2),
                (3, 3),
                (4, 4),
                (1, 1)
            ]
        ];
    }

    private static object[][] CenterTestSource()
    {
        return
        [
            [
                0, 0,
                (2, 2),
                (4, 3),
                (3, 4),
                (2 ,2)
            ],
            [
                3, 5,
                (4, 1),
                (1, 5),
                (3, 2),
                (4, 3)
            ],
            [
                2, 10,
                (1, 2),
                (2, 3),
                (3, 4),
                (4, 5),
                (5, 4),
                (4, 3),
                (3, 2),
                (2, 1)
            ]
        ];
    }
}