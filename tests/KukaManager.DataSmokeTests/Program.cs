using KukaManager.Data;
using static KukaManager.Utils.Value;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("SMOKE FAILED: " + name);
    passed++;
    Console.WriteLine("[PASS] " + name);
}

var root = Path.Combine(Path.GetTempPath(), "KukaManager-DataSmoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    using var store = new KukaStore(Path.Combine(root, "kuka.sqlite3"));
    Check(store.One("SELECT id FROM courses WHERE name='数学'") is not null, "course catalog seeded");
    store.SetupAdmin("测试管理员", "Admin.Test", "12345678");
    Check(store.VerifyLogin("admin.test", "12345678"), "login and password hash");

    store.SaveStudent(new()
    {
        ["name"]="张三", ["gender"]="男", ["birth"]="2018-01-01", ["parent"]="张妈妈", ["relation"]="妈妈",
        ["phone"]="13800000000", ["wechat"]="wx-test", ["address"]="", ["note"]="", ["active"]=1
    });
    var studentId = S(store.One("SELECT id FROM students WHERE name='张三'")!, "id");
    Check(studentId.Length > 0, "student create and read");

    store.SaveTeacher(new() { ["name"]="王老师", ["phone"]="13900000000", ["active"]=1 });
    Check(store.One("SELECT id FROM teachers WHERE name='王老师'") is not null, "teacher create and read");
    store.IntegrityCheck();
    Check(true, "SQLite integrity and foreign keys");
    Console.WriteLine($"SMOKE PASS: {passed} checks");
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
