// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Appearance;
using System.Globalization;

namespace Richasy.BiliKernel;

/// <summary>
/// 基础扩展.
/// </summary>
public static class BasicExtensions
{
    /// <summary>
    /// 将数字简写文本中转换为大略的次数.
    /// </summary>
    /// <param name="text">数字简写文本.</param>
    /// <param name="removeText">需要先在简写文本中移除的内容.</param>
    /// <returns>一个大概的次数，比如 <c>3.2万播放</c>，最终会返回 <c>32000</c>.</returns>
    public static double ToCountNumber(this string text, string removeText = "")
    {
        if (!string.IsNullOrEmpty(removeText))
        {
            text = text.Replace(removeText, string.Empty).Trim();
        }

        // 对于目前的B站来说，汉字单位只有 `万` 和 `亿` 两种.
        if (text.EndsWith("万"))
        {
            var num = Convert.ToDouble(text.Replace("万", string.Empty));
            return num * 10000;
        }
        else if (text.EndsWith("亿"))
        {
            var num = Convert.ToDouble(text.Replace("亿", string.Empty));
            return num * 100000000;
        }

        return double.TryParse(text, out var number) ? number : -1;
    }

    /// <summary>
    /// 将时间文本转换为秒数.
    /// </summary>
    public static int ToDurationSeconds(this string durationText)
    {
        if (string.IsNullOrWhiteSpace(durationText))
        {
            return 0;
        }

        if (durationText.Contains(' '))
        {
            var sp = durationText.Split(' ');
            durationText = sp.FirstOrDefault(p => p.Contains(':'))?.Trim() ?? "00:00";
        }

        // 逐段累加而不是交给 TimeSpan.Parse：站内存在 "00:349:48" 这类分钟数溢出的时长文本，
        // TimeSpan.Parse 会直接抛 OverflowException，连带整页投稿都加载失败.
        long totalSeconds = 0;
        foreach (var part in durationText.Split(':'))
        {
            if (!long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return 0;
            }

            totalSeconds = (totalSeconds * 60) + value;
            if (totalSeconds > int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return (int)totalSeconds;
    }

    /// <summary>
    /// 获取本地时区标识.
    /// </summary>
    /// <returns>标识.</returns>
    public static int GetLocalTimeZoneNumber()
    {
        // 获取当前时间
        var now = DateTime.Now;

        // 获取当前时间的偏移（以 TimeSpan 表示）
        var offset = now - now.ToUniversalTime();

        // 获取偏移的整数小时部分
        return (int)offset.TotalHours;
    }

    /// <summary>
    /// 计算视频的宽高比.
    /// </summary>
    public static MediaAspectRatio CalcVideoAspectRatio(int width, int height)
    {
        // 计算最大公约数
        var gcd = GCD(width, height);

        // 计算宽和高的最简比率
        var aspectWidth = width / gcd;
        var aspectHeight = height / gcd;

        static int GCD(int a, int b)
        {
            while (b != 0)
            {
                var temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        return new MediaAspectRatio(aspectWidth, aspectHeight);
    }
}
