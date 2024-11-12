using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace TagsCloudVisualization.Tests;

[TestFixture]
[TestOf(typeof(CircularCloudLayouter))]
public class CircularCloudLayouterTest
{

    private static readonly string failReportFolderPath = "./failed";
    private static readonly int maxDistanceFromBarycenter = 20;

    [OneTimeSetUp]
    public void EmptyFailReportFolder()
    {
        if (Directory.Exists(failReportFolderPath))
            Directory.Delete(failReportFolderPath, true);
    }

    [TearDown]
    public void ReportFailures()
    {
        var context = TestContext.CurrentContext;
        if (context.Result.Outcome.Status == TestStatus.Failed)
        {
            var args = context.Test.Arguments;
            var center = new Point((int) args[0]!, (int) args[1]!);
            var circularCloudLayouter = Arrange(center.X, center.Y);
            if (context.Test.MethodName == null)
            {
                Console.WriteLine("Teardown error: test method name is null");
                return;
            }

            if (context.Test.MethodName.Contains(nameof(PutNextRectangle_ThrowsOnHeightOrWidth_BeingLessOrEqualToZero)))
                return;
            
            var isClosenessTest = context.Test.MethodName.Contains(nameof(RectanglesShouldBeCloseToEachOther));

            var sizesArr = ((int, int)[])(isClosenessTest ? args[3] : args[2]);
            
            var rectangles = GenerateLayout(sizesArr, circularCloudLayouter).ToArray();
            var savingPath = $"{failReportFolderPath}/{context.Test.Name}.png";
            
            Directory.CreateDirectory(failReportFolderPath);
            
#pragma warning disable CA1416
            var bitmap = new Bitmap(1000, 1000);
            var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.White);
            graphics.DrawRectangles(new Pen(Color.Blue), rectangles);
            
            var isCenterTest = context.Test.MethodName.Contains(nameof(RectanglesCommonBarycenterIsCloseToTheProvidedCenter));
            if (isCenterTest)
            {
                graphics.DrawEllipse(
                    new Pen(Color.Lime), center.X, center.Y,
                    maxDistanceFromBarycenter, maxDistanceFromBarycenter);
                var barycenter = ComputeBaryCenter(rectangles);
                graphics.DrawEllipse(new Pen(Color.Red), barycenter.X, barycenter.Y, 1, 1);
            }
            
            bitmap.Save(savingPath, ImageFormat.Png);
#pragma warning restore CA1416
            Console.WriteLine($"Failure was reported to {Path.GetFullPath(savingPath)}");
        }
    }

    [Test]
    [Description("Проверяем, что метод PutNextRectangle бросает ArgumentException, " +
                 "если ему передан размер прямоугольника с высотой или шириной " +
                 "меньше либо равной нулю")]
    [TestCase(0, -4, 0, 4)]
    [TestCase(0, 1, -2, 4)]
    [TestCase(0, 0, 4, -2)]
    [TestCase(2, 3, 2, 0)]
    [TestCase(1, 2, -2, -1)]
    [TestCase(-1, 0, -1, 0)]
    public void PutNextRectangle_ThrowsOnHeightOrWidth_BeingLessOrEqualToZero(
        int centerX,
        int centerY,
        int width,
        int height)
    {
        var circularCloudLayouter = Arrange(centerX, centerY);

        Action act = () => circularCloudLayouter.PutNextRectangle(new Size(width, height));

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("rectangle width and height must be greater than 0");
    }

    [Test]
    [Description("Проверяем, что прямоугольники не пересекаются друг с другом")]
    [TestCaseSource(nameof(IntersectionTestSource))]
    public void RectanglesShouldNotIntersectEachOther(
        int centerX,
        int centerY,
        (int, int)[] sizes)
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
        (int, int)[] sizes)
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
        (int, int)[] sizes)
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
        (int, int)[] sizes)
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
            .SelectMany((rect1, index) => rectangleList
                .GetRange(index, rectangleList.Count - index)
                .Select(rect2 => new RectanglePair(rect1, rect2)))
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

    private Point ComputeBaryCenter(IEnumerable<Rectangle> rectangles)
    {
        var (totalX, totalY, count) = rectangles
            .Aggregate((0, 0, 0), ((int totalX, int totalY, int count) res, Rectangle rect) =>
            {
                var rectCenter = RectangleCenter(rect);
                return (res.totalX + rectCenter.X, res.totalY + rectCenter.Y, ++res.count);
            });
        return new Point(totalX / count, totalY / count);
    }

    private void Assert_RectanglesBarycenterIsCloseToCenter(
        IEnumerable<Rectangle> rectangles,
        Point center)
    {
        var barycenter = ComputeBaryCenter(rectangles);
        var deviationFromCenter = SquaredDistance(barycenter, center);
        deviationFromCenter.Should().BeLessOrEqualTo(maxDistanceFromBarycenter * maxDistanceFromBarycenter);
    }

    private static object[][] IntersectionTestSource()
    {
        return
        [
            [
                500, 500,
                new[]
                {
                    (20, 10),
                    (40, 20),
                    (60, 30),
                    (80, 40)
                }
            ],
            [
                500, 500,
                new[]
                {
                    (10, 10),
                    (10, 10),
                    (20, 10),
                    (20, 10)
                }
            ],
            [
                600, 600,
                new[]
                {
                    (20, 10),
                    (30, 15),
                    (50, 10),
                    (60, 30),
                    (30, 10),
                    (40, 20)
                }
            ]
        ];
    }

    private static object[][] FirstRectanglePositionTestSource()
    {
        return
        [
            [
                500, 500,
                new[]
                {
                    (20, 30),
                    (30, 45),
                    (40, 60)
                }
            ],
            [
                510, 550,
                new[]
                {
                    (10, 40),
                    (10, 30),
                    (10, 20)
                }
            ],
            [
                300, 800,
                new[]
                {
                    (10, 30)
                }
            ]
        ];
    }

    private static object[][] DensityTestSource()
    {
        return
        [
            [
                500, 500, 3025,
                new[]
                {
                    (20, 20),
                    (30, 30),
                    (40, 40)
                }
            ],
            [
                500, 500, 3025,
                new[]
                {
                    (20, 20),
                    (40, 40),
                    (30, 30)
                }
            ],
            [
                600, 400, 4225,
                new[]
                {
                    (40, 40),
                    (30, 30),
                    (20, 20),
                    (10, 10)
                }
            ],
            [
                400, 550, 3025,
                new[]
                {
                    (20, 20),
                    (30, 30),
                    (40, 40),
                    (10, 10)
                }
            ]
        ];
    }

    private static object[][] CenterTestSource()
    {
        return
        [
            [
                500, 500,
                new[]
                {
                    (10, 20),
                    (10, 60),
                    (10, 60),
                    (10, 20)
                }
            ],
            [
                300, 500,
                new[]
                {
                    (10, 40),
                    (10, 50),
                    (20, 30),
                    (40, 30)
                }
            ],
            [
                520, 410,
                new[]
                {
                    (10, 20),
                    (20, 30),
                    (30, 40),
                    (40, 50),
                    (50, 40),
                    (40, 30),
                    (30, 20),
                    (20, 10)
                }
            ]
        ];
    }
}