using System.Text.RegularExpressions;

namespace KukaManager.Services;

public static class CourseTaxonomy
{
    public static readonly string[] Groups =
    [
        "编程数学", "图形化编程", "Python", "C++", "数学", "语文",
        "合作学校课时费", "编程赛事 / 器材 / 培训", "组合课程", "其他课程", "未分类课程"
    ];

    public static string Canonical(params string?[] values)
    {
        var raw = string.Join(' ', values.Select(x => (x ?? string.Empty).Trim()));
        var text = raw.ToLowerInvariant().Replace('＋', '+');
        var compact = Regex.Replace(text, @"\s+", string.Empty);

        if ((compact.Contains("mathcode") || text.Contains("math code")) && compact.Contains("编程") && !compact.Contains("编程数学"))
        {
            if (new[] { "编程+mathcode", "编程/mathcode", "编程、mathcode", "各一期" }.Any(compact.Contains))
                return "组合课程";
        }
        if (new[] { "mathcode", "mathcode秋季班", "mathcode暑假班", "数学编程", "编程数学" }.Any(compact.Contains) || text.Contains("math code")) return "编程数学";
        if (compact.Contains("图形化编程") || compact.Contains("图形化")) return "图形化编程";
        if (compact.Contains("python")) return "Python";
        if (compact.Contains("c++") || compact.Contains("c＋＋") || compact.Contains("cpp")) return "C++";
        if (compact.Contains("语文")) return "语文";
        if (compact.Contains("课时费") && new[] { "小学", "学校", "校" }.Any(compact.Contains)) return "合作学校课时费";
        if (new[] { "比赛", "器材", "培训费" }.Any(compact.Contains)) return "编程赛事 / 器材 / 培训";
        if (compact.Contains("数学")) return "数学";
        if (compact.Contains("课程") || compact.Contains("培训")) return "其他课程";
        return "未分类课程";
    }

    public static string DisplayMonth(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length >= 7 && v[4] == '-' && int.TryParse(v[..4], out var year) && int.TryParse(v.Substring(5, 2), out var month))
            return $"{year}年{month}月";
        return v;
    }
}
