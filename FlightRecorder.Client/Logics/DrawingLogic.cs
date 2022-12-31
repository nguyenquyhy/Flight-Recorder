using FlightRecorder.Client.Logics;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlightRecorder.Client;

public class DrawingLogic
{
    private readonly ILogger<DrawingLogic> logger;
    private readonly ImageLogic imageLogic;
    private readonly ThrottleLogic drawingThrottleLogic;

    public DrawingLogic(ILogger<DrawingLogic> logger, ImageLogic imageLogic, ThrottleLogic drawingThrottleLogic)
    {
        logger.LogDebug("Creating instance of {class}", nameof(DrawingLogic));

        this.logger = logger;
        this.imageLogic = imageLogic;
        this.drawingThrottleLogic = drawingThrottleLogic;
    }

    public void ClearCache()
    {
        imageLogic.ClearCache();
    }

    public void Draw(
        List<(long milliseconds, AircraftPositionStruct position)> records,
        Func<int> getCurrentFrame,
        StateMachine.State currentState,
        int width, int height,
        Image imageControl
        )
    {
        Task.Run(() =>
        {
            if (currentState == StateMachine.State.Recording || currentState == StateMachine.State.ReplayingSaved || currentState == StateMachine.State.ReplayingUnsaved)
            {
                drawingThrottleLogic.RunAsync(async () => DrawInternal(records, getCurrentFrame(), currentState, width, height, imageControl), 500);
            }
            else
            {
                DrawInternal(records, getCurrentFrame(), currentState, width, height, imageControl);
            }
        });
    }

    private void DrawInternal(
        List<(long milliseconds, AircraftPositionStruct position)> records,
        int currentFrame, StateMachine.State currentState,
        int width, int height,
        Image imageControl
        )
    {
        try
        {
            var image = imageLogic.Draw(width, height, records, currentFrame);

            if (image != null)
            {
                imageControl.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var bmp = new WriteableBitmap(image.Width, image.Height, image.Metadata.HorizontalResolution, image.Metadata.VerticalResolution, PixelFormats.Bgra32, null);

                        bmp.Lock();
                        try
                        {
                            var backBuffer = bmp.BackBuffer;

                            for (var y = 0; y < image.Height; y++)
                            {
                                var buffer = image.GetPixelRowSpan(y);
                                for (var x = 0; x < image.Width; x++)
                                {
                                    var backBufferPos = backBuffer + (y * image.Width + x) * 4;
                                    var rgba = buffer[x];
                                    var color = rgba.A << 24 | rgba.R << 16 | rgba.G << 8 | rgba.B;

                                    Marshal.WriteInt32(backBufferPos, color);
                                }
                            }

                            bmp.AddDirtyRect(new Int32Rect(0, 0, image.Width, image.Height));
                        }
                        finally
                        {
                            bmp.Unlock();
                            imageControl.Source = bmp;
                        }
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        logger.LogError(ex, "Cannot convert to WriteableBitmap");
#endif
                    }
                    finally
                    {
                        image.Dispose();
                    }
                });
            }
            else
            {
                imageControl.Dispatcher.Invoke(() =>
                {
                    imageControl.Source = null;
                });
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            logger.LogError(ex, "Cannot draw");
#endif
        }
    }
}
