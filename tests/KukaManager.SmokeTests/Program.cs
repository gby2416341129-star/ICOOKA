using System.IO;
using System.Windows;
using System.Windows.Controls;
using KukaManager.Design;
using KukaManager.Data;
using KukaManager.Utils;
using static KukaManager.Utils.Value;

static class Smoke
{
    private static int _passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("SMOKE FAILED: " + name);
        _passed++;
        Console.WriteLine("[PASS] " + name);
    }

    [STAThread]
    public static int Main()
    {
        var app = new Application();
        Theme.Apply(app);
        Check(app.Resources.MergedDictionaries.Count > 0, "modern WPF theme parses and loads");

        var controlsHost = new StackPanel { Width = 900, Height = 600 };
        var combo = new ComboBox { Width = 240, SelectedIndex = 0 };
        combo.Items.Add("数学");
        combo.Items.Add("Python");
        var datePicker = new DatePicker { Width = 240, SelectedDate = DateTime.Today };
        var calendar = new Calendar();
        var grid = new DataGrid { Width = 720, Height = 180 };
        controlsHost.Children.Add(combo);
        controlsHost.Children.Add(datePicker);
        controlsHost.Children.Add(calendar);
        controlsHost.Children.Add(grid);
        controlsHost.Measure(new Size(900, 600));
        controlsHost.Arrange(new Rect(0, 0, 900, 600));
        combo.ApplyTemplate();
        datePicker.ApplyTemplate();
        calendar.ApplyTemplate();
        grid.ApplyTemplate();
        controlsHost.UpdateLayout();
        Check(combo.Template is not null && datePicker.Template is not null && grid.Template is not null,
            "modern input, picker and grid templates instantiate and layout");
        var root = Path.Combine(Path.GetTempPath(), "KukaManager-Smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var store = new KukaStore(Path.Combine(root, "kuka.sqlite3"));
            Check(store.One("SELECT id FROM courses WHERE name='数学'") is not null, "new database seeds course catalog");
            store.SetupAdmin("测试管理员", "Admin.Test", "12345678");
            Check(store.VerifyLogin("admin.test", "12345678"), "admin login is case-insensitive and password-compatible");

            store.SaveStudent(new()
            {
                ["name"]="张三", ["gender"]="男", ["birth"]="2018-01-01", ["parent"]="张妈妈", ["relation"]="妈妈",
                ["phone"]="13800000000", ["wechat"]="wx-test", ["address"]="", ["note"]="", ["active"]=1
            });
            var studentId = S(store.One("SELECT id FROM students WHERE name='张三'")!, "id");
            Check(studentId.Length > 0, "student CRUD");

            store.SaveTeacher(new() { ["name"]="王老师", ["phone"]="13900000000", ["active"]=1 });
            var teacherId = S(store.One("SELECT id FROM teachers WHERE name='王老师'")!, "id");
            var courseId = S(store.One("SELECT id FROM courses WHERE name='数学'")!, "id");
            Check(teacherId.Length > 0 && courseId.Length > 0, "teacher/course catalog");

            store.SaveEnrollment(new()
            {
                ["student_id"]=studentId, ["course_id"]=courseId, ["enrolled_on"]=DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd"),
                ["project"]="数学春季班", ["list_fen"]=258800L, ["discount_fen"]=0L, ["reason"]="", ["approver"]="",
                ["inviter"]="测试", ["seller"]="测试", ["lessons"]=8, ["note"]=""
            });
            var enrollmentId = S(store.One("SELECT id FROM enrollments WHERE student_id=?", studentId)!, "id");
            Check(store.Balance(enrollmentId).Lessons == 8, "enrollment and credit balance");

            var targetDay = DateTime.Today.AddDays(-7);
            var weekday = ((int)targetDay.DayOfWeek + 6) % 7;
            var classId = store.CreateClass(new()
            {
                ["name"]="创思班", ["course_id"]=courseId, ["teacher_id"]=teacherId, ["room"]="A101", ["weekday"]=weekday,
                ["start_time"]="08:00", ["end_time"]="09:00", ["start_date"]=targetDay.AddDays(-1).ToString("yyyy-MM-dd"),
                ["end_date"]=DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"), ["active"]=1
            });
            store.AddMember(classId, enrollmentId, targetDay.AddDays(-1).ToString("yyyy-MM-dd"));
            Check(store.ClassRosterNames(classId, targetDay.ToString("yyyy-MM-dd")).Contains("张三"), "class roster");
            var charged = store.ProcessDue(DateTime.Now);
            Check(charged >= 1 && store.Balance(enrollmentId).Lessons < 8, "schedule -> attendance -> automatic credit consumption");

            store.AddManualFinance(new()
            {
                ["tx_date"]="2025-06-02", ["source_sheet"]="刘春诚", ["direction"]="借", ["amount_fen"]=100L, ["fee_fen"]=0L,
                ["balance"]="129205.16", ["summary"]="余额锚点", ["nature"]="other_income", ["category"]="其他收入", ["raw_note"]=""
            });
            store.AddManualFinance(new()
            {
                ["tx_date"]="2025-06-03", ["source_sheet"]="刘春诚", ["direction"]="借", ["amount_fen"]=6502633L, ["fee_fen"]=0L,
                ["balance"]="", ["summary"]="后续流水", ["nature"]="other_income", ["category"]="其他收入", ["raw_note"]=""
            });
            var account = store.FinanceAccountSummary().Single(x => x.Account == "刘春诚");
            Check(account.CurrentBalanceFen == 19423149L, "finance confirmed balance + post-anchor net change");

            store.AddManualFinance(new()
            {
                ["tx_date"]="2025-06-04", ["source_sheet"]="刘春城", ["direction"]="借", ["amount_fen"]=10000L, ["fee_fen"]=0L,
                ["balance"]="", ["summary"]="别名流水", ["nature"]="other_income", ["category"]="其他收入", ["raw_note"]=""
            });
            store.MergeFinanceAccounts("刘春城", "刘春诚");
            var merged = store.FinanceAccountSummary().Single(x => x.Account == "刘春诚");
            Check(merged.CurrentBalanceFen == 19433149L && !store.FinanceAccountSummary().Any(x => x.Account == "刘春城"), "finance account merge and balance recomputation");

            var linkedTx = store.AddManualFinance(new()
            {
                ["tx_date"]=DateTime.Today.ToString("yyyy-MM-dd"), ["source_sheet"]="三峡银行0042", ["direction"]="借", ["amount_fen"]=258800L,
                ["fee_fen"]=0L, ["balance"]="", ["summary"]="张三", ["student_id"]=studentId, ["course_id"]=courseId, ["class_id"]=classId,
                ["nature"]="operating_income", ["category"]="学费/培训费", ["raw_note"]=""
            });
            Check(store.One("SELECT id FROM student_class_history WHERE transaction_id=?", linkedTx) is not null, "finance payment -> class history link");
            Check(store.ClassRosterNames(classId, DateTime.Today.ToString("yyyy-MM-dd")).Contains("张三"), "finance-linked student appears in class/week roster");

            store.IntegrityCheck();
            Check(true, "SQLite integrity and foreign keys");
            Console.WriteLine($"SMOKE PASS: {_passed} checks");
            app.Shutdown();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
