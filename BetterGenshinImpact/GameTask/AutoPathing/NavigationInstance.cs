using System;
using System.IO;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Common.Map.Maps;
using BetterGenshinImpact.GameTask.Common.Map.Maps.Base;
using BetterGenshinImpact.GameTask.Model.Area;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using OpenCvSharp;

namespace BetterGenshinImpact.GameTask.AutoPathing;

public class NavigationInstance
{
    private float _prevX = -1;
    private float _prevY = -1;
    private DateTime _captureTime = DateTime.MinValue;

    /// <summary>
    /// 诊断用：已保存的小地图截图数量，最多保存 20 张，避免占满磁盘
    /// </summary>
    private int _debugDumpCount;

    /// <summary>
    /// 诊断用：上一次保存小地图截图的时间，约每 2 秒保存一张
    /// </summary>
    private DateTime _debugDumpTime = DateTime.MinValue;

    /// <summary>
    /// 诊断用：把实际裁剪到的小地图保存到 log\minimap_debug，用于排查低分辨率下识别坐标错误的问题
    /// </summary>
    private void DumpMiniMapForDebug(Mat colorMat, Point2f position)
    {
        try
        {
            var now = DateTime.UtcNow;
            if (_debugDumpCount >= 20 || (now - _debugDumpTime).TotalSeconds < 2)
            {
                return;
            }

            _debugDumpTime = now;
            _debugDumpCount++;
            var dir = Path.Combine(AppContext.BaseDirectory, "log", "minimap_debug");
            Directory.CreateDirectory(dir);
            // 文件名带上裁剪尺寸与识别到的坐标，便于对照日志
            var name = $"{DateTime.Now:HHmmss}_{colorMat.Width}x{colorMat.Height}_pos{position.X:F0}_{position.Y:F0}.png";
            Cv2.ImWrite(Path.Combine(dir, name), colorMat);
        }
        catch
        {
            // 诊断功能失败不影响正常识别
        }
    }

    public void Reset()
    {
        (_prevX, _prevY) = (-1, -1);
    }
    
    public void SetPrevPosition(float x, float y)
    {
        (_prevX, _prevY) = (x, y);
    }

    public Point2f GetPosition(ImageRegion imageRegion, string mapName, string mapMatchMethod)
    {
        using var colorMat = new Mat(imageRegion.SrcMat, MapAssets.Get(imageRegion).MimiMapRect);
        var captureTime = DateTime.UtcNow;
        var p = MapManager.GetMap(mapName, mapMatchMethod).GetMiniMapPosition(colorMat, _prevX, _prevY);
        DumpMiniMapForDebug(colorMat, p);
        if (p != default && captureTime > _captureTime)
        {
            (_prevX, _prevY) = (p.X, p.Y);
            _captureTime = captureTime;
        }
        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<object>(typeof(Navigation),
            "SendCurrentPosition", new object(), p));
        return p;
    }

    /// <summary>
    /// 稳定获取当前位置坐标，优先使用全地图匹配，适用于不需要高效率但需要高稳定性的场景
    /// </summary>
    /// <param name="imageRegion">图像区域</param>
    /// <param name="mapName">地图名字</param>
    /// <param name="mapMatchMethod">地图匹配方式</param>
    /// <returns>当前位置坐标</returns>
    public Point2f GetPositionStable(ImageRegion imageRegion, string mapName, string mapMatchMethod)
    {
        using var colorMat = new Mat(imageRegion.SrcMat, MapAssets.Get(imageRegion).MimiMapRect);
        var captureTime = DateTime.UtcNow;

        // 先尝试使用局部匹配
        var sceneMap = MapManager.GetMap(mapName, mapMatchMethod);
        //提高局部匹配的阈值，以解决在沙漠录制点位时，移动过远不会触发全局匹配的情况
        var p = (sceneMap as SceneBaseMapByTemplateMatch)?.GetMiniMapPosition(colorMat, _prevX, _prevY, 0)
                ?? sceneMap.GetMiniMapPosition(colorMat, _prevX, _prevY);

        // 如果局部匹配失败或者点位跳跃过大，再尝试全地图匹配
        if (p == default || (_prevX > 0 && _prevY >0 && p.DistanceTo(new Point2f(_prevX,_prevY)) > 150))
        {
            Reset();
            p = MapManager.GetMap(mapName, mapMatchMethod).GetMiniMapPosition(colorMat, _prevX, _prevY);
        }
        if (p != default && captureTime > _captureTime)
        {
            (_prevX, _prevY) = (p.X, p.Y);
            _captureTime = captureTime;
        }

        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<object>(typeof(Navigation),
            "SendCurrentPosition", new object(), p));
        return p;
    }

    public Point2f GetPositionStableByCache(ImageRegion imageRegion, string mapName, string mapMatchingMethod, int cacheTimeMs = 900)
    {
        var captureTime = DateTime.UtcNow;
        if (captureTime - _captureTime < TimeSpan.FromMilliseconds(cacheTimeMs) && _prevX > 0 && _prevY > 0)
        {
            return new Point2f(_prevX, _prevY);
        }

        return GetPositionStable(imageRegion, mapName, mapMatchingMethod);
    }
}
