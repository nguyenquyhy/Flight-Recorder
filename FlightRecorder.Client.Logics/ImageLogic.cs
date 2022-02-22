using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.Linq;

namespace FlightRecorder.Client.Logics
{
    public class ImageLogic
    {
        private readonly ILogger<ImageLogic> logger;

        public ImageLogic(ILogger<ImageLogic> logger)
        {
            logger.LogDebug("Creating instance of {class}", nameof(ImageLogic));

            this.logger = logger;
        }

        private (int width, int height, int count)? cacheKey = null;
        private Image<Rgba32>? cachedBackground = null;
        private List<int>? cachedFrames = null;

        public void ClearCache()
        {
            logger.LogDebug("Clear image cache");
            cacheKey = null;
            cachedBackground = null;
            cachedFrames = null;
        }

        public Image<Rgba32>? Draw(int width, int height, List<(long milliseconds, AircraftPositionStruct position)> records, int currentFrame)
        {
            logger.LogTrace("Generate chart {frame}", currentFrame);

            if (cacheKey.HasValue && cacheKey.Value == (width, height, records.Count))
            {
                logger.LogTrace("Use cached background", currentFrame);
            }
            else
            {
                logger.LogTrace("Draw new background", currentFrame);

                var lines = new List<(Color color, List<PointF> points)>();
                var frames = new List<int>();

                if (width == 0) return null;

                Color? lastColor = null;

                if (records != null && records.Count > 0)
                {
                    var data = records.ToArray();
                    var min = data.Min(item => item.position.Altitude);
                    var max = data.Max(item => item.position.Altitude);
                    var startTime = data.Min(item => item.milliseconds);
                    var endTime = data.Max(item => item.milliseconds);

                    if (startTime == endTime) return null;

                    var chartHeight = height - 5;

                    var dataIndex = 0;
                    for (var i = 0; i < width; i++)
                    {
                        var currentTime = startTime + (double)i / width * (endTime - startTime);

                        while (data[dataIndex].milliseconds < currentTime)
                        {
                            dataIndex++;
                        }
                        var (milliseconds, position) = data[dataIndex];

                        var altitude = position.Altitude;
                        var color = position.IsOnGround == 0 ? Color.Blue : Color.Brown;

                        if (color != lastColor)
                        {
                            lines.Add((color, new()));
                            lastColor = color;
                        }
                        lines.Last().points.Add(new PointF(i, max == min ? height / 2 : height - (float)((altitude - min) / (max - min)) * chartHeight - 1));
                        frames.Add(dataIndex);
                    }
                }

                var imgBlank = new Image<Rgba32>(width, height);
                var imgBackground = imgBlank.Clone(ctx =>
                {
                    ctx.Fill(Color.LightGray);

                    foreach ((var color, var points) in lines)
                    {
                        if (points.Count > 1)
                        {
                            ctx.DrawLines(color, 1f, points.ToArray());
                        }
                        else if (points.Count == 1)
                        {
                            ctx.DrawLines(color, 1f, new PointF[] { points[0], points[0] });
                        }
                    }
                });

                cacheKey = (width, height, records?.Count ?? 0);
                cachedFrames = frames;
                cachedBackground = imgBackground;
            }


            if (cachedBackground != null && cachedFrames != null)
            {
                var image = cachedBackground.Clone(ctx =>
                {
                    var currentX = cachedFrames.FindIndex(frame => frame > currentFrame);

                    if (currentX >= 0)
                    {
                        ctx.DrawLines(Color.Red, 1, new PointF(currentX, 0), new PointF(currentX, height));
                    }
                });
                return image;
            }
            else
            {
                return null;
            }
        }
    }
}
