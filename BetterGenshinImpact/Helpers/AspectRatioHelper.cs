namespace BetterGenshinImpact.Helpers;

/// <summary>
/// 宽高比判断工具
/// </summary>
public static class AspectRatioHelper
{
    /// <summary>
    /// 判断分辨率是否可视为 16:9。
    /// 1366x768 这类笔记本常见分辨率与 16:9 只差不到 0.1%，按近似 16:9 处理，允许 1% 以内的误差。
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>近似 16:9 时为 true</returns>
    public static bool Is16By9(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var diff = System.Math.Abs((long)width * 9 - (long)height * 16);
        return diff * 100 <= (long)height * 16;
    }
}
