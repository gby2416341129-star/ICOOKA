using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using KukaManager.Models;
using KukaManager.Services;
using KukaManager.Utils;
using static KukaManager.Utils.Value;

namespace KukaManager.Data;

public sealed class KukaStore : IDisposable
{
    private readonly SqliteConnection _db;
    private SqliteTransaction? _tx;
    public string DatabasePath { get; }
    public string Actor { get; set; } = "管理员";

    public KukaStore(string path)
    {
        DatabasePath = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _db = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate;Cache=Shared");
        _db.Open();
        Execute("PRAGMA foreign_keys=ON");
        Execute("PRAGMA busy_timeout=5000");
        Execute("PRAGMA journal_mode=WAL");
        Execute("PRAGMA synchronous=NORMAL");
        ExecuteScript(LegacySchema.Sql);
        MigrateLegacy();
        SeedDefaults();
    }

    public void Dispose() => _db.Dispose();

    public void ExecuteScript(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.Transaction = _tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public int Execute(string sql, params object?[] args)
    {
        using var cmd = CreateCommand(sql, args);
        return cmd.ExecuteNonQuery();
    }

    private SqliteCommand CreateCommand(string sql, IReadOnlyList<object?> args)
    {
        var cmd = _db.CreateCommand();
        cmd.Transaction = _tx;
        cmd.CommandText = sql;
        for (var i = 0; i < args.Count; i++) cmd.Parameters.AddWithValue($"$p{i}", args[i] ?? DBNull.Value);
        // Python source uses ? placeholders. Convert in order to named parameters.
        var idx = 0;
        cmd.CommandText = Regex.Replace(sql, @"\?", _ => $"$p{idx++}");
        return cmd;
    }

    public List<Dictionary<string, object?>> Rows(string sql, params object?[] args)
    {
        using var cmd = CreateCommand(sql, args);
        using var r = cmd.ExecuteReader();
        var list = new List<Dictionary<string, object?>>();
        while (r.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            list.Add(row);
        }
        return list;
    }

    public Dictionary<string, object?>? One(string sql, params object?[] args) => Rows(sql, args).FirstOrDefault();

    public T InTransaction<T>(Func<T> work)
    {
        if (_tx is not null) return work();
        using var tx = _db.BeginTransaction();
        _tx = tx;
        try { var result = work(); tx.Commit(); return result; }
        catch { tx.Rollback(); throw; }
        finally { _tx = null; }
    }

    public void InTransaction(Action work) => InTransaction(() => { work(); return 0; });

    private void MigrateLegacy()
    {
        var version = Convert.ToInt32(One("PRAGMA user_version")?.Values.FirstOrDefault() ?? 0);
        if (version < 2)
        {
            InTransaction(() =>
            {
                if (!string.IsNullOrWhiteSpace(Setting("password")) && string.IsNullOrWhiteSpace(Setting("admin_username")))
                {
                    SetSetting("admin_username", "admin"); SetSetting("admin_name", "管理员");
                }
                Execute("PRAGMA user_version=2");
            });
            version = 2;
        }
        if (version < 3)
        {
            var cols = Rows("PRAGMA table_info(finance_transactions)").Select(x => S(x, "name")).ToHashSet();
            InTransaction(() =>
            {
                if (!cols.Contains("course_group")) Execute("ALTER TABLE finance_transactions ADD COLUMN course_group TEXT NOT NULL DEFAULT ''");
                if (!cols.Contains("teacher_id")) Execute("ALTER TABLE finance_transactions ADD COLUMN teacher_id TEXT REFERENCES teachers(id)");
                if (!cols.Contains("entry_source")) Execute("ALTER TABLE finance_transactions ADD COLUMN entry_source TEXT NOT NULL DEFAULT 'import'");
                Execute("PRAGMA user_version=3");
            });
            version = 3;
        }
        if (version < 4)
        {
            InTransaction(() =>
            {
                foreach (var r in Rows("SELECT id,course_hint,summary,raw_note,course_group FROM finance_transactions"))
                    if (string.IsNullOrWhiteSpace(S(r, "course_group")))
                        Execute("UPDATE finance_transactions SET course_group=? WHERE id=?", CourseTaxonomy.Canonical(S(r,"course_hint"), S(r,"summary"), S(r,"raw_note")), S(r,"id"));
                Execute("PRAGMA user_version=4");
            });
            version = 4;
        }
        if (version < 5)
        {
            var cols = Rows("PRAGMA table_info(finance_transactions)").Select(x => S(x, "name")).ToHashSet();
            InTransaction(() =>
            {
                if (!cols.Contains("course_id")) Execute("ALTER TABLE finance_transactions ADD COLUMN course_id TEXT REFERENCES courses(id)");
                if (!cols.Contains("class_id")) Execute("ALTER TABLE finance_transactions ADD COLUMN class_id TEXT REFERENCES classes(id)");
                ExecuteScript("""
CREATE TABLE IF NOT EXISTS student_class_history(id TEXT PRIMARY KEY,transaction_id TEXT UNIQUE REFERENCES finance_transactions(id) ON DELETE CASCADE,student_id TEXT NOT NULL REFERENCES students(id),course_id TEXT NOT NULL REFERENCES courses(id),class_id TEXT NOT NULL REFERENCES classes(id),enrollment_id TEXT REFERENCES enrollments(id),teacher_id TEXT NOT NULL REFERENCES teachers(id),weekday INTEGER NOT NULL,start_time TEXT NOT NULL,end_time TEXT NOT NULL,room TEXT NOT NULL DEFAULT '',started_on TEXT NOT NULL,ended_on TEXT NOT NULL DEFAULT '',source TEXT NOT NULL DEFAULT 'finance',note TEXT NOT NULL DEFAULT '',created_at TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS student_class_history_student ON student_class_history(student_id,course_id,started_on);
CREATE INDEX IF NOT EXISTS student_class_history_class ON student_class_history(class_id,started_on,ended_on);
""");
                foreach (var r in Rows("SELECT id,course_group FROM finance_transactions WHERE course_id IS NULL"))
                {
                    var name = S(r,"course_group").Trim(); if (name.Length == 0) continue;
                    var c = One("SELECT id FROM courses WHERE name=? LIMIT 1", name);
                    if (c is not null) Execute("UPDATE finance_transactions SET course_id=? WHERE id=?", S(c,"id"), S(r,"id"));
                }
                Execute("PRAGMA user_version=5");
            });
        }
    }

    private void SeedDefaults()
    {
        if (Setting("initialized") == "1") return;
        InTransaction(() =>
        {
            var names = new List<string> { "小颗粒搭建课", "大颗粒搭建课" };
            names.AddRange(Enumerable.Range(1, 8).Select(i => $"图形化编程 L{i}"));
            names.AddRange(["Python", "C++", "信息学奥赛课"]);
            foreach (var n in names) Insert("courses", new() { ["name"] = n, ["category"] = "编程", ["active"] = 1 });
            Insert("courses", new() { ["name"] = "数学", ["category"] = "数学", ["active"] = 1 });
            SetSetting("initialized", "1");
        });
    }

    public Dictionary<string, object?> Get(string table, string id)
    {
        EnsureTable(table);
        return One($"SELECT * FROM {table} WHERE id=?", id) ?? throw new InvalidOperationException("记录不存在");
    }

    public string Insert(string table, Dictionary<string, object?> data)
    {
        EnsureTable(table);
        var d = new Dictionary<string, object?>(data, StringComparer.OrdinalIgnoreCase);
        if (!d.ContainsKey("id")) d["id"] = NewId();
        var cols = Rows($"PRAGMA table_info({table})").Select(x => S(x,"name")).ToHashSet();
        if (d.Keys.Any(k => !cols.Contains(k))) throw new InvalidOperationException("未知字段");
        var names = d.Keys.ToArray();
        Execute($"INSERT INTO {table} ({string.Join(',', names)}) VALUES ({string.Join(',', names.Select(_ => "?"))})", names.Select(x => d[x]).ToArray());
        return Convert.ToString(d["id"])!;
    }

    private static readonly HashSet<string> Tables = new(StringComparer.OrdinalIgnoreCase)
    { "courses","teachers","students","enrollments","classes","members","sessions","attendance","makeups","ledger","growth","audit","finance_import_batches","finance_transactions","finance_allocations","student_class_history" };
    private static void EnsureTable(string table) { if (!Tables.Contains(table)) throw new InvalidOperationException("未知数据表"); }

    public void Log(string action, string detail) => Insert("audit", new()
    {
        ["at"] = DateTime.Now.ToString("s"), ["actor"] = Actor, ["action"] = action, ["detail"] = detail
    });

    public string Setting(string key) => S(One("SELECT value FROM settings WHERE key=?", key) ?? new(), "value");
    public void SetSetting(string key, object? value) => Execute("INSERT OR REPLACE INTO settings(key,value) VALUES (?,?)", key, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");

    public void IntegrityCheck()
    {
        var r = One("PRAGMA quick_check");
        var value = r?.Values.FirstOrDefault()?.ToString();
        if (!string.Equals(value,"ok",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("本地数据库完整性检查失败：" + value);
        if (Rows("PRAGMA foreign_key_check").Count > 0) throw new InvalidOperationException("本地数据库存在关联错误，请先从备份恢复");
    }

    public bool AccountInitialized() => !string.IsNullOrWhiteSpace(Setting("admin_username")) && !string.IsNullOrWhiteSpace(Setting("admin_name")) && !string.IsNullOrWhiteSpace(Setting("password"));

    public void SetupAdmin(string displayName, string username, string password)
    {
        displayName = displayName.Trim(); username = username.Trim();
        if (displayName.Length is < 2 or > 40) throw new InvalidOperationException("管理员姓名需要 2～40 个字符");
        if (!Regex.IsMatch(username, "^[A-Za-z0-9_.-]{3,32}$")) throw new InvalidOperationException("账号需为 3～32 位英文字母、数字、点、横线或下划线");
        Actor = displayName;
        InTransaction(() => { SetSetting("admin_name", displayName); SetSetting("admin_username", username); SetPassword(password); Log("初始化管理员", displayName + " / " + username); });
    }

    public void SetPassword(string password)
    {
        if (password.Length < 8) throw new InvalidOperationException("密码至少 8 位");
        if (password.Length > 128) throw new InvalidOperationException("密码过长");
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 300000, HashAlgorithmName.SHA256, 32);
        SetSetting("password", Convert.ToHexString(salt).ToLowerInvariant() + ":" + Convert.ToHexString(hash).ToLowerInvariant());
    }

    public bool VerifyPassword(string password)
    {
        try
        {
            var parts = Setting("password").Split(':'); if (parts.Length != 2) return false;
            var salt = Convert.FromHexString(parts[0]); var expected = Convert.FromHexString(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 300000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch { return false; }
    }

    public bool VerifyLogin(string username, string password) => string.Equals(username.Trim(), Setting("admin_username"), StringComparison.OrdinalIgnoreCase) && VerifyPassword(password);

    public void ChangeAdmin(string displayName, string username, string currentPassword, string newPassword = "")
    {
        if (!VerifyPassword(currentPassword)) throw new InvalidOperationException("当前密码不正确");
        displayName = displayName.Trim(); username = username.Trim();
        if (displayName.Length is < 2 or > 40) throw new InvalidOperationException("管理员姓名需要 2～40 个字符");
        if (!Regex.IsMatch(username, "^[A-Za-z0-9_.-]{3,32}$")) throw new InvalidOperationException("账号格式不正确");
        InTransaction(() =>
        {
            SetSetting("admin_name", displayName); SetSetting("admin_username", username);
            if (!string.IsNullOrWhiteSpace(newPassword)) SetPassword(newPassword);
            Actor = displayName; Log("修改管理员账户", displayName + " / " + username);
        });
    }

    public static int Age(string birth, DateTime? today = null)
    {
        var b = DateTime.ParseExact(birth,"yyyy-MM-dd",CultureInfo.InvariantCulture); var t = (today ?? DateTime.Today).Date;
        return t.Year - b.Year - (t.Month < b.Month || (t.Month == b.Month && t.Day < b.Day) ? 1 : 0);
    }

    public void SaveStudent(Dictionary<string, object?> d, string? id = null)
    {
        var name = Convert.ToString(d["name"])!.Trim(); var parent = Convert.ToString(d["parent"])!.Trim(); var phone = Convert.ToString(d["phone"])!.Trim(); var birth = Convert.ToString(d["birth"])!;
        if (name.Length == 0 || parent.Length == 0 || phone.Length == 0) throw new InvalidOperationException("姓名、家长姓名和联系电话为必填项");
        if (Age(birth) is < 0 or > 120) throw new InvalidOperationException("出生日期不合理");
        var dup = id is null ? One("SELECT id FROM students WHERE name=? AND birth=? AND phone=? LIMIT 1", name,birth,phone) : One("SELECT id FROM students WHERE name=? AND birth=? AND phone=? AND id<>? LIMIT 1", name,birth,phone,id);
        if (dup is not null) throw new InvalidOperationException("已存在相同姓名、生日和联系电话的学员档案");
        if (id is not null && Convert.ToInt32(d["active"]) == 0)
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            if (One("SELECT m.id FROM members m JOIN enrollments e ON e.id=m.enrollment_id WHERE e.student_id=? AND (m.left_on='' OR m.left_on>?) LIMIT 1", id,today) is not null ||
                One("SELECT id FROM student_class_history WHERE student_id=? AND (ended_on='' OR ended_on>?) LIMIT 1", id,today) is not null)
                throw new InvalidOperationException("该学员仍有在班/缴费关联班级记录，请先处理班级关系后再归档");
        }
        Upsert("students", d, id);
    }

    public void SaveCourse(Dictionary<string, object?> d, string? id = null)
    {
        var name = Convert.ToString(d["name"])!.Trim(); var category = Convert.ToString(d["category"])!.Trim();
        if (name.Length == 0 || category.Length == 0) throw new InvalidOperationException("课程名称和科目不能为空");
        var dup = id is null ? One("SELECT id FROM courses WHERE name=? AND category=? LIMIT 1", name,category) : One("SELECT id FROM courses WHERE name=? AND category=? AND id<>? LIMIT 1", name,category,id);
        if (dup is not null) throw new InvalidOperationException("已存在同名同科目的课程");
        Upsert("courses", d, id);
    }

    public void SaveTeacher(Dictionary<string, object?> d, string? id = null)
    {
        var name = Convert.ToString(d["name"])!.Trim(); var phone = Convert.ToString(d.GetValueOrDefault("phone"))?.Trim() ?? "";
        if (name.Length == 0) throw new InvalidOperationException("老师姓名不能为空");
        var dup = id is null ? One("SELECT id FROM teachers WHERE name=? AND phone=? LIMIT 1", name,phone) : One("SELECT id FROM teachers WHERE name=? AND phone=? AND id<>? LIMIT 1", name,phone,id);
        if (dup is not null) throw new InvalidOperationException("已存在相同姓名和电话的老师");
        Upsert("teachers", d, id);
    }

    public void SaveEnrollment(Dictionary<string, object?> d, string? id = null)
    {
        if (string.IsNullOrWhiteSpace(Convert.ToString(d["project"]))) throw new InvalidOperationException("请填写报名项目");
        _ = DateTime.ParseExact(Convert.ToString(d["enrolled_on"])!,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        if (id is not null && (One("SELECT id FROM ledger WHERE enrollment_id=? LIMIT 1",id) is not null || One("SELECT id FROM members WHERE enrollment_id=? LIMIT 1",id) is not null))
            throw new InvalidOperationException("已入班或已有课消的报名记录不能更改，请新增续费记录");
        var listFen = Convert.ToInt64(d["list_fen"]); var discountFen = Convert.ToInt64(d["discount_fen"]); var lessons = Convert.ToInt32(d["lessons"]);
        if (discountFen > listFen) throw new InvalidOperationException("优惠金额不能超过原价");
        if (lessons <= 0) throw new InvalidOperationException("课次必须大于零");
        if (discountFen > 0 && (string.IsNullOrWhiteSpace(Convert.ToString(d["reason"])) || string.IsNullOrWhiteSpace(Convert.ToString(d["approver"])))) throw new InvalidOperationException("优惠必须填写原因和审批人");
        if (!B(Get("students",Convert.ToString(d["student_id"])!),"active")) throw new InvalidOperationException("归档学员不能新建报名");
        if (!B(Get("courses",Convert.ToString(d["course_id"])!),"active")) throw new InvalidOperationException("停用课程不能新建报名");
        Upsert("enrollments", d, id);
    }

    public void SaveGrowth(Dictionary<string, object?> d, string? id = null)
    {
        foreach (var k in new[]{"project","outcome","evaluation"}) if (string.IsNullOrWhiteSpace(Convert.ToString(d[k]))) throw new InvalidOperationException("请填写学习项目、学习成果和老师评价");
        _ = DateTime.ParseExact(Convert.ToString(d["day"])!,"yyyy-MM-dd",CultureInfo.InvariantCulture); CheckTime(Convert.ToString(d["start_time"])!,Convert.ToString(d["end_time"])!);
        Upsert("growth", d, id);
    }

    private void Upsert(string table, Dictionary<string, object?> d, string? id)
    {
        var isEdit = id is not null;
        InTransaction(() =>
        {
            if (id is null) id = Insert(table,d);
            else
            {
                _ = Get(table,id); var keys = d.Keys.ToArray();
                Execute($"UPDATE {table} SET {string.Join(',', keys.Select(k => k+"=?"))} WHERE id=?", keys.Select(k=>d[k]).Append(id).ToArray());
            }
            Log(isEdit ? "修改" : "新增", table + ":" + id);
        });
    }

    public static void CheckTime(string start, string end)
    {
        _ = TimeOnly.ParseExact(start,"HH:mm",CultureInfo.InvariantCulture); _ = TimeOnly.ParseExact(end,"HH:mm",CultureInfo.InvariantCulture);
        if (string.CompareOrdinal(start,end) >= 0) throw new InvalidOperationException("结束时间必须晚于开始时间");
    }

    public BalanceInfo Balance(string enrollmentId)
    {
        var e = Get("enrollments", enrollmentId); var r = One("SELECT COALESCE(SUM(delta),0) d,COALESCE(SUM(amount_fen),0) a FROM ledger WHERE enrollment_id=?", enrollmentId)!;
        return new BalanceInfo(I(e,"lessons") + I(r,"d"), L(e,"list_fen") - L(e,"discount_fen") + L(r,"a"));
    }

    public Dictionary<string,BalanceInfo> BalanceMap() => Rows("""SELECT e.id,e.lessons + COALESCE(SUM(l.delta),0) remaining,e.list_fen-e.discount_fen+COALESCE(SUM(l.amount_fen),0) remaining_fen FROM enrollments e LEFT JOIN ledger l ON l.enrollment_id=e.id GROUP BY e.id""")
        .ToDictionary(r=>S(r,"id"), r=>new BalanceInfo(I(r,"remaining"),L(r,"remaining_fen")));

    public string CreateClass(Dictionary<string, object?> d)
    {
        var startTime = Convert.ToString(d["start_time"])!; var endTime=Convert.ToString(d["end_time"])!; CheckTime(startTime,endTime);
        var start=DateTime.ParseExact(Convert.ToString(d["start_date"])!,"yyyy-MM-dd",CultureInfo.InvariantCulture); var end=DateTime.ParseExact(Convert.ToString(d["end_date"])!,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        if (end < start || (end-start).TotalDays > 1096) throw new InvalidOperationException("排课周期应在 0～3 年内");
        if (string.IsNullOrWhiteSpace(Convert.ToString(d["name"]))) throw new InvalidOperationException("请填写班级名称");
        if (!B(Get("courses",Convert.ToString(d["course_id"])!),"active")) throw new InvalidOperationException("停用课程不能新建班级");
        if (!B(Get("teachers",Convert.ToString(d["teacher_id"])!),"active")) throw new InvalidOperationException("停用老师不能新建班级");
        if (One("SELECT id FROM classes WHERE active=1 AND weekday=? AND start_date<=? AND end_date>=? AND start_time<? AND end_time>? AND (teacher_id=? OR (room<>'' AND room=?))",
            d["weekday"],d["end_date"],d["start_date"],d["end_time"],d["start_time"],d["teacher_id"],d["room"]) is not null) throw new InvalidOperationException("该老师或教室在这个时段已有排课");
        return InTransaction(() =>
        {
            var cid=Insert("classes",d); var wd=Convert.ToInt32(d["weekday"]); var offset=(wd - (((int)start.DayOfWeek+6)%7)+7)%7; var day=start.AddDays(offset);
            while(day<=end)
            {
                Insert("sessions",new(){["class_id"]=cid,["day"]=day.ToString("yyyy-MM-dd"),["start_time"]=d["start_time"],["end_time"]=d["end_time"],["teacher_id"]=d["teacher_id"],["cancelled"]=0,["reason"]=""}); day=day.AddDays(7);
            }
            Log("创建班级",cid); return cid;
        });
    }

    public string AddMember(string classId,string enrollmentId,string joinedOn)
    {
        var c=Get("classes",classId); var e=Get("enrollments",enrollmentId); _=DateTime.ParseExact(joinedOn,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        if (S(c,"course_id")!=S(e,"course_id")) throw new InvalidOperationException("报名课程和班级课程不一致");
        if (!B(Get("students",S(e,"student_id")),"active")) throw new InvalidOperationException("归档学员不能入班，请先恢复学员档案");
        if (!B(c,"active")) throw new InvalidOperationException("班级已停用");
        if (string.CompareOrdinal(joinedOn,S(e,"enrolled_on"))<0 || string.CompareOrdinal(joinedOn,S(c,"end_date"))>0) throw new InvalidOperationException("入班日期需在报名日期之后且在排课周期内");
        if (Balance(enrollmentId).Lessons<=0) throw new InvalidOperationException("该报名已无剩余课次，不能入班");
        if (One("SELECT id FROM members WHERE class_id=? AND enrollment_id=? AND joined_on<=? AND (left_on='' OR left_on>?)",classId,enrollmentId,S(c,"end_date"),joinedOn) is not null) throw new InvalidOperationException("该报名在此班级已有重叠的入班记录");
        if (One("""SELECT m.id FROM members m JOIN enrollments e ON e.id=m.enrollment_id JOIN classes c ON c.id=m.class_id WHERE e.student_id=? AND c.active=1 AND c.weekday=? AND c.start_time<? AND c.end_time>? AND c.end_date>=? AND c.start_date<=? AND m.joined_on<=? AND (m.left_on='' OR m.left_on>?)""",
            S(e,"student_id"),I(c,"weekday"),S(c,"end_time"),S(c,"start_time"),joinedOn,S(c,"end_date"),S(c,"end_date"),joinedOn) is not null) throw new InvalidOperationException("学员在这个时段已有班级，请先办理退班");
        return InTransaction(()=>{var mid=Insert("members",new(){["class_id"]=classId,["enrollment_id"]=enrollmentId,["joined_on"]=joinedOn,["left_on"]=""});Log("入班",mid);return mid;});
    }

    public void LeaveMember(string memberId,string day,string reason)
    {
        var m=Get("members",memberId); _=DateTime.ParseExact(day,"yyyy-MM-dd",CultureInfo.InvariantCulture); if(string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("请填写退班原因");
        var today=DateTime.Today.ToString("yyyy-MM-dd"); if(!string.IsNullOrEmpty(S(m,"left_on")) && string.CompareOrdinal(S(m,"left_on"),today)<=0) throw new InvalidOperationException("该入班记录已经完成退班，历史记录不能直接改写");
        if(string.CompareOrdinal(day,S(m,"joined_on"))<0 || string.CompareOrdinal(day,today)<0) throw new InvalidOperationException("退班从今天或未来日期生效");
        InTransaction(()=>{Execute("UPDATE members SET left_on=? WHERE id=?",day,memberId);Log(string.IsNullOrEmpty(S(m,"left_on"))?"计划退班":"调整计划退班",memberId+" "+day+" "+reason);});
    }

    public void CancelScheduledLeave(string memberId,string reason)
    {
        var m=Get("members",memberId); if(string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("请填写撤销原因"); var today=DateTime.Today.ToString("yyyy-MM-dd"); var old=S(m,"left_on");
        if(old.Length==0) throw new InvalidOperationException("该学员没有计划退班"); if(string.CompareOrdinal(old,today)<=0) throw new InvalidOperationException("退班已经生效，不能撤销历史记录");
        InTransaction(()=>{Execute("UPDATE members SET left_on='' WHERE id=?",memberId);Log("撤销计划退班",memberId+" 原生效日 "+old+" "+reason);});
    }

    public void CancelFutureMember(string memberId,string reason)
    {
        var m=Get("members",memberId); if(string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("请填写取消原因"); if(string.CompareOrdinal(S(m,"joined_on"),DateTime.Today.ToString("yyyy-MM-dd"))<=0) throw new InvalidOperationException("入班已经生效，不能删除历史入班记录；请办理退班");
        InTransaction(()=>{Execute("DELETE FROM members WHERE id=?",memberId);Log("取消计划入班",memberId+" "+reason);});
    }
    public bool Charge(string attendanceId,string at,string reason)
    {
        var a=Get("attendance",attendanceId); var eid=S(a,"enrollment_id"); var balance=Balance(eid);
        var net=I(One("SELECT COALESCE(SUM(delta),0) n FROM ledger WHERE attendance_id=?",attendanceId)!,"n");
        if(net==-1) return true; if(net!=0) throw new InvalidOperationException("课消流水不一致"); if(balance.Lessons<=0) return false;
        var amount=decimal.ToInt64(decimal.Round((decimal)balance.AmountFen/balance.Lessons,0,MidpointRounding.AwayFromZero));
        Insert("ledger",new(){["enrollment_id"]=eid,["attendance_id"]=attendanceId,["at"]=at,["delta"]=-1,["amount_fen"]=-amount,["remaining"]=balance.Lessons-1,["remaining_fen"]=balance.AmountFen-amount,["reason"]=reason});
        return true;
    }

    public void RefundAttendance(string attendanceId,string reason)
    {
        var a=Get("attendance",attendanceId); var r=One("SELECT COALESCE(SUM(delta),0) d,COALESCE(SUM(amount_fen),0) a FROM ledger WHERE attendance_id=?",attendanceId)!;
        var d=I(r,"d"); if(d==0) return; if(d!=-1) throw new InvalidOperationException("课消流水不一致"); var balance=Balance(S(a,"enrollment_id"));
        Insert("ledger",new(){["enrollment_id"]=S(a,"enrollment_id"),["attendance_id"]=attendanceId,["at"]=DateTime.Now.ToString("s"),["delta"]=1,["amount_fen"]=-L(r,"a"),["remaining"]=balance.Lessons+1,["remaining_fen"]=balance.AmountFen-L(r,"a"),["reason"]=reason});
    }

    public int ProcessDue(DateTime? now=null)
    {
        var cutoff=(now??DateTime.Now).ToString("yyyy-MM-ddTHH:mm"); var count=0;
        InTransaction(()=>
        {
            var due=Rows("""SELECT s.*,m.enrollment_id FROM sessions s JOIN classes c ON c.id=s.class_id JOIN members m ON m.class_id=s.class_id JOIN enrollments e ON e.id=m.enrollment_id WHERE c.active=1 AND s.day||'T'||s.end_time<=? AND s.day>=m.joined_on AND (m.left_on='' OR s.day<m.left_on) AND s.day>=e.enrolled_on ORDER BY s.day,s.end_time,s.id""",cutoff);
            foreach(var s in due)
            {
                if(One("SELECT id FROM attendance WHERE session_id=? AND enrollment_id=?",S(s,"id"),S(s,"enrollment_id")) is not null) continue;
                var aid=Insert("attendance",new(){["session_id"]=S(s,"id"),["enrollment_id"]=S(s,"enrollment_id"),["status"]=B(s,"cancelled")?"停课":"余额不足",["reason"]=S(s,"reason")});
                if(!B(s,"cancelled") && Charge(aid,S(s,"day")+"T"+S(s,"end_time"),"定时课消")){Execute("UPDATE attendance SET status='已扣课' WHERE id=?",aid);count++;}
            }
            foreach(var m in Rows("SELECT * FROM makeups WHERE status='待补课' AND day||'T'||end_time<=? ORDER BY day,end_time",cutoff))
                if(Charge(S(m,"attendance_id"),S(m,"day")+"T"+S(m,"end_time"),"补课课消")){Execute("UPDATE makeups SET status='已补课' WHERE id=?",S(m,"id"));count++;}
            if(count>0) Log("自动课消",count+" 次");
        });
        return count;
    }

    public void CorrectAttendance(string attendanceId,bool present,string reason)
    {
        if(string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("更正必须填写原因"); var a=Get("attendance",attendanceId); var session=One("SELECT * FROM sessions WHERE id=?",S(a,"session_id"));
        if(session is not null && B(session,"cancelled")) throw new InvalidOperationException("该课次已经停课，不能再更正为到课；如需恢复课程请重新排课");
        InTransaction(()=>
        {
            if(present){if(!Charge(attendanceId,DateTime.Now.ToString("s"),"手动更正："+reason)) throw new InvalidOperationException("剩余课次不足");}
            else RefundAttendance(attendanceId,"缺课撤回："+reason);
            Execute("UPDATE attendance SET status=?,reason=? WHERE id=?",present?"已扣课":"缺课",reason,attendanceId); Execute("UPDATE makeups SET status='取消' WHERE attendance_id=?",attendanceId); Log("出勤更正",attendanceId+" "+reason);
        });
    }

    public void ScheduleMakeup(string attendanceId,string day,string startTime,string endTime,string teacherId,string note)
    {
        var a=Get("attendance",attendanceId); if(S(a,"status")!="缺课") throw new InvalidOperationException("请先将这次出勤更正为缺课"); CheckTime(startTime,endTime);
        var d=DateTime.ParseExact(day,"yyyy-MM-dd",CultureInfo.InvariantCulture); if(d.Date<DateTime.Today) throw new InvalidOperationException("补课请选择今天或未来日期"); if(string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("请填写补课备注");
        if(!B(Get("teachers",teacherId),"active")) throw new InvalidOperationException("停用老师不能安排补课"); var existing=One("SELECT * FROM makeups WHERE attendance_id=?",attendanceId); if(existing is not null && S(existing,"status")=="已补课") throw new InvalidOperationException("这次补课已经完成");
        var existingId=existing is null?"":S(existing,"id");
        if(One("SELECT id FROM makeups WHERE teacher_id=? AND day=? AND start_time<? AND end_time>? AND status<>'取消' AND attendance_id<>?",teacherId,day,endTime,startTime,attendanceId) is not null || One("SELECT id FROM sessions WHERE cancelled=0 AND teacher_id=? AND day=? AND start_time<? AND end_time>?",teacherId,day,endTime,startTime) is not null) throw new InvalidOperationException("补课老师在此时段已有安排");
        var student=One("SELECT e.student_id FROM attendance a JOIN enrollments e ON e.id=a.enrollment_id WHERE a.id=?",attendanceId);
        if(student is not null)
        {
            if(One("""SELECT s.id FROM sessions s JOIN members m ON m.class_id=s.class_id JOIN enrollments e ON e.id=m.enrollment_id WHERE e.student_id=? AND s.cancelled=0 AND s.day=? AND s.start_time<? AND s.end_time>? AND s.day>=m.joined_on AND (m.left_on='' OR s.day<m.left_on) LIMIT 1""",S(student,"student_id"),day,endTime,startTime) is not null) throw new InvalidOperationException("该学员在补课时段已有班级课程");
        }
        InTransaction(()=>
        {
            if(existing is null) Insert("makeups",new(){["attendance_id"]=attendanceId,["day"]=day,["start_time"]=startTime,["end_time"]=endTime,["teacher_id"]=teacherId,["status"]="待补课",["note"]=note});
            else Execute("UPDATE makeups SET day=?,start_time=?,end_time=?,teacher_id=?,status='待补课',note=? WHERE id=?",day,startTime,endTime,teacherId,note,existingId);
            Log(existing is null?"安排补课":"调整补课",attendanceId+" "+day+" "+startTime);
        });
    }

    public void CancelSession(string sessionId,string reason)
    {
        if(string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("请填写停课原因"); var s=Get("sessions",sessionId); if(B(s,"cancelled")) throw new InvalidOperationException("该课次已经停课");
        InTransaction(()=>
        {
            foreach(var a in Rows("SELECT * FROM attendance WHERE session_id=?",sessionId)){RefundAttendance(S(a,"id"),"班级停课："+reason);Execute("UPDATE attendance SET status='停课',reason=? WHERE id=?",reason,S(a,"id"));Execute("UPDATE makeups SET status='取消' WHERE attendance_id=?",S(a,"id"));}
            Execute("UPDATE sessions SET cancelled=1,reason=? WHERE id=?",reason,sessionId); Log("停课",sessionId+" "+reason);
        });
    }

    public IReadOnlyList<string> ClassRosterNames(string classId,string onDate)
    {
        var manual=Rows("""SELECT DISTINCT s.name FROM members m JOIN enrollments e ON e.id=m.enrollment_id JOIN students s ON s.id=e.student_id WHERE m.class_id=? AND m.joined_on<=? AND (m.left_on='' OR m.left_on>?)""",classId,onDate,onDate).Select(x=>S(x,"name"));
        var finance=Rows("""SELECT DISTINCT s.name FROM student_class_history h JOIN students s ON s.id=h.student_id WHERE h.class_id=? AND h.started_on<=? AND (h.ended_on='' OR h.ended_on>?)""",classId,onDate,onDate).Select(x=>S(x,"name"));
        return manual.Concat(finance).Where(x=>x.Length>0).Distinct().OrderBy(x=>x).ToList();
    }
    public int ClassRosterCount(string classId,string onDate)=>ClassRosterNames(classId,onDate).Count;

    public List<Dictionary<string,object?>> StudentAcademicHistory(string studentId)=>Rows("""SELECT h.*,c.name course,cl.name class_name,t.name teacher,ft.tx_date payment_date,ft.source_sheet account,ft.summary payment_summary,ft.income_fen,ft.expense_fen FROM student_class_history h JOIN courses c ON c.id=h.course_id JOIN classes cl ON cl.id=h.class_id JOIN teachers t ON t.id=h.teacher_id LEFT JOIN finance_transactions ft ON ft.id=h.transaction_id WHERE h.student_id=? ORDER BY h.started_on DESC,h.created_at DESC""",studentId);

    // ---------------- Finance ----------------
    public string NormalizeAccountName(string? value)
    {
        var text=(value??"").Normalize(NormalizationForm.FormKC).Replace("\u200b","").Replace("\ufeff","").Replace('\u00a0',' '); return Regex.Replace(text,@"\s+"," ").Trim();
    }

    private Dictionary<string,string> FinanceAliases()
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string,string>>(Setting("finance_account_aliases"));
            return parsed is null ? new(StringComparer.OrdinalIgnoreCase) : new(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch { return new(StringComparer.OrdinalIgnoreCase); }
    }
    private void SaveFinanceAliases(Dictionary<string,string> aliases)=>SetSetting("finance_account_aliases",JsonSerializer.Serialize(aliases));
    public string ResolveFinanceAccountName(string? value)
    {
        var n=NormalizeAccountName(value); if(n.Length==0) return ""; var aliases=FinanceAliases(); var seen=new HashSet<string>(); while(aliases.TryGetValue(n,out var next)&&seen.Add(n)) n=NormalizeAccountName(next); return n;
    }

    public void NormalizeFinanceAccounts()
    {
        var changes=Rows("SELECT DISTINCT source_sheet FROM finance_transactions").Select(r=>(Old:S(r,"source_sheet"),New:ResolveFinanceAccountName(S(r,"source_sheet")))).Where(x=>x.New.Length>0&&x.Old!=x.New).ToList(); if(changes.Count==0)return;
        InTransaction(()=>{foreach(var x in changes)Execute("UPDATE finance_transactions SET source_sheet=? WHERE source_sheet=?",x.New,x.Old);});
    }

    public List<(string Left, string Right)> FinanceSimilarAccountPairs()
    {
        var names = FinanceAccountSummary().Select(x => x.Account).ToList();
        var pairs = new List<(string Left, string Right)>();
        for (var i = 0; i < names.Count; i++)
        {
            for (var j = i + 1; j < names.Count; j++)
            {
                var a = NormalizeAccountName(names[i]);
                var b = NormalizeAccountName(names[j]);
                if (a.Length != b.Length || a.Length < 3) continue;
                var diff = 0;
                for (var k = 0; k < a.Length; k++) if (a[k] != b[k]) diff++;
                if (diff == 1) pairs.Add((a, b));
            }
        }
        return pairs;
    }

    public void MergeFinanceAccounts(string source,string target)
    {
        source=ResolveFinanceAccountName(source); target=ResolveFinanceAccountName(target); if(source.Length==0||target.Length==0) throw new InvalidOperationException("请选择有效户头"); if(source==target)return;
        var aliases=FinanceAliases(); aliases[source]=target; foreach(var k in aliases.Keys.ToList()) if(ResolveAliasValue(aliases[k],aliases)==source) aliases[k]=target;
        InTransaction(()=>{Execute("UPDATE finance_transactions SET source_sheet=? WHERE source_sheet=?",target,source);SaveFinanceAliases(aliases);Log("合并财务户头",source+" -> "+target);});
    }
    private string ResolveAliasValue(string value,Dictionary<string,string> aliases){var n=NormalizeAccountName(value);var seen=new HashSet<string>();while(aliases.TryGetValue(n,out var next)&&seen.Add(n))n=NormalizeAccountName(next);return n;}
    public void RenameFinanceAccount(string oldName,string newName){oldName=ResolveFinanceAccountName(oldName);newName=NormalizeAccountName(newName);if(newName.Length==0)throw new InvalidOperationException("户头名称不能为空");if(oldName==newName)return;if(FinanceAccountSummary().Any(x=>x.Account==newName)){MergeFinanceAccounts(oldName,newName);return;}InTransaction(()=>{Execute("UPDATE finance_transactions SET source_sheet=? WHERE source_sheet=?",newName,oldName);var aliases=FinanceAliases();aliases[oldName]=newName;SaveFinanceAliases(aliases);Log("重命名财务户头",oldName+" -> "+newName);});}


    public Dictionary<string,int> ImportFinanceBatch(Dictionary<string,object?> meta,List<Dictionary<string,object?>> rows)
    {
        var sha=Convert.ToString(meta.GetValueOrDefault("file_sha256"))??"";if(One("SELECT id FROM finance_import_batches WHERE file_sha256=?",sha) is not null)throw new InvalidOperationException("这份财务文件已经导入过，已阻止重复导入");
        var batchId=NewId();var imported=0;var duplicate=0;var matched=0;
        InTransaction(()=>
        {
            Insert("finance_import_batches",new(){["id"]=batchId,["file_name"]=Convert.ToString(meta.GetValueOrDefault("file_name"))??"",["file_sha256"]=sha,["imported_at"]=DateTime.Now.ToString("s"),["sheet_count"]=Convert.ToInt32(meta.GetValueOrDefault("sheet_count")??0),["row_count"]=rows.Count,["imported_count"]=0,["duplicate_count"]=0,["matched_count"]=0,["note"]=Convert.ToString(meta.GetValueOrDefault("note"))??""});
            foreach(var r in rows)
            {
                var fp=S(r,"fingerprint");if(One("SELECT id FROM finance_transactions WHERE fingerprint=?",fp) is not null){duplicate++;continue;}var txId=NewId();var group=S(r,"course_group");if(string.IsNullOrWhiteSpace(group))group=CourseTaxonomy.Canonical(S(r,"course_hint"),S(r,"summary"),S(r,"raw_note"));var exact=One("SELECT id FROM courses WHERE name=? ORDER BY active DESC LIMIT 1",group);var sid=S(r,"suggested_student_id");var isMatched=sid.Length>0&&S(r,"match_status")=="matched";
                Insert("finance_transactions",new(){["id"]=txId,["batch_id"]=batchId,["source_sheet"]=ResolveFinanceAccountName(S(r,"source_sheet")),["source_row"]=I(r,"source_row"),["tx_date"]=S(r,"tx_date"),["summary"]=S(r,"summary"),["course_hint"]=S(r,"course_hint"),["income_fen"]=L(r,"income_fen"),["expense_fen"]=L(r,"expense_fen"),["fee_fen"]=L(r,"fee_fen"),["balance_fen"]=r.GetValueOrDefault("balance_fen"),["direction"]=S(r,"direction",L(r,"income_fen")>0?"借":"贷"),["nature"]=S(r,"nature","unknown"),["category"]=S(r,"category","待分类"),["match_status"]=S(r,"match_status","unmatched"),["raw_note"]=S(r,"raw_note"),["fingerprint"]=fp,["raw_json"]=S(r,"raw_json"),["course_group"]=group,["teacher_id"]=r.GetValueOrDefault("teacher_id"),["entry_source"]="import",["course_id"]=exact is null?null:S(exact,"id"),["class_id"]=null});
                if(isMatched)
                {
                    var amount=L(r,"income_fen")!=0?L(r,"income_fen"):L(r,"expense_fen");Insert("finance_allocations",new(){["transaction_id"]=txId,["student_id"]=sid,["enrollment_id"]=r.GetValueOrDefault("suggested_enrollment_id"),["amount_fen"]=amount,["confidence"]=D(r,"confidence"),["source"]="auto",["note"]="导入时按姓名/课程/金额自动匹配"});var teacher=S(r,"teacher_id");if(teacher.Length==0)teacher=InferFinanceTeacher(sid,group)??"";if(teacher.Length>0)Execute("UPDATE finance_transactions SET teacher_id=? WHERE id=?",teacher,txId);matched++;
                }
                imported++;
            }
            Execute("UPDATE finance_import_batches SET imported_count=?,duplicate_count=?,matched_count=? WHERE id=?",imported,duplicate,matched,batchId);Log("导入财务流水",$"{Convert.ToString(meta.GetValueOrDefault("file_name"))}：新增 {imported}，重复 {duplicate}，自动匹配 {matched}");
        });
        return new(){{"imported",imported},{"duplicate",duplicate},{"matched",matched}};
    }

    public List<Dictionary<string,object?>> FinanceRows(string? studentId=null,int limit=10000,string? account=null,string? month=null,string? courseGroup=null,string? teacherId=null,string? search=null)
    {
        var clauses=new List<string>();var args=new List<object?>();
        if(!string.IsNullOrWhiteSpace(studentId)){clauses.Add("EXISTS(SELECT 1 FROM finance_allocations fx WHERE fx.transaction_id=t.id AND fx.student_id=?)");args.Add(studentId);}
        if(!string.IsNullOrWhiteSpace(account)){clauses.Add("t.source_sheet=?");args.Add(ResolveFinanceAccountName(account));}
        if(!string.IsNullOrWhiteSpace(month)){clauses.Add("substr(t.tx_date,1,7)=?");args.Add(month);}
        if(!string.IsNullOrWhiteSpace(courseGroup)){clauses.Add("t.course_group=?");args.Add(courseGroup);}
        if(!string.IsNullOrWhiteSpace(teacherId)){clauses.Add("t.teacher_id=?");args.Add(teacherId);}
        if(!string.IsNullOrWhiteSpace(search)){var needle="%"+search.Trim()+"%";clauses.Add("(t.summary LIKE ? OR t.raw_note LIKE ? OR t.course_hint LIKE ? OR EXISTS(SELECT 1 FROM finance_allocations fs JOIN students ss ON ss.id=fs.student_id WHERE fs.transaction_id=t.id AND ss.name LIKE ?))");args.AddRange([needle,needle,needle,needle]);}
        var where=clauses.Count>0?"WHERE "+string.Join(" AND ",clauses):"";args.Add(limit);
        return Rows($"""SELECT t.*,COALESCE((SELECT GROUP_CONCAT(s.name,'、') FROM finance_allocations f JOIN students s ON s.id=f.student_id WHERE f.transaction_id=t.id),'') students,COALESCE((SELECT name FROM teachers tt WHERE tt.id=t.teacher_id),'') teacher,COALESCE((SELECT name FROM courses cc WHERE cc.id=t.course_id),'') catalog_course,COALESCE((SELECT name FROM classes cl WHERE cl.id=t.class_id),'') class_name FROM finance_transactions t {where} ORDER BY t.tx_date DESC,t.source_sheet,t.source_row DESC,t.rowid DESC LIMIT ?""",args.ToArray());
    }

    public Dictionary<string,long> FinanceSummary(string? studentId=null,string? account=null,string? month=null,string? courseGroup=null,string? teacherId=null,string? search=null)
    {
        var rows=FinanceRows(studentId,200000,account,month,courseGroup,teacherId,search); long n=rows.Count,income=0,expense=0,fee=0,opIncome=0,opExpense=0,refund=0,unmatched=0;
        foreach(var r in rows){income+=L(r,"income_fen");expense+=L(r,"expense_fen");fee+=L(r,"fee_fen");if(S(r,"nature")=="operating_income")opIncome+=L(r,"income_fen");if(S(r,"nature")=="operating_expense")opExpense+=L(r,"expense_fen");if(S(r,"nature")=="refund")refund+=L(r,"expense_fen");if(S(r,"match_status")=="unmatched")unmatched++;}
        return new(){{"n",n},{"income_fen",income},{"expense_fen",expense},{"fee_fen",fee},{"net_fen",income-expense-fee},{"operating_income_fen",opIncome},{"operating_expense_fen",opExpense},{"refund_fen",refund},{"unmatched",unmatched},{"operating_net_fen",opIncome-opExpense-refund-fee}};
    }

    private (long? Latest,long? Current,string? Date,long Delta,bool Estimated,string? LatestDate) AccountBalanceState(string account)
    {
        account=ResolveFinanceAccountName(account);var latest=One("SELECT tx_date FROM finance_transactions WHERE source_sheet=? ORDER BY tx_date DESC,source_row DESC,rowid DESC LIMIT 1",account);var latestDate=latest is null?null:S(latest,"tx_date");
        var anchor=One("SELECT balance_fen,tx_date FROM finance_transactions WHERE source_sheet=? AND balance_fen IS NOT NULL ORDER BY tx_date DESC,source_row DESC,rowid DESC LIMIT 1",account);if(anchor is null)return(null,null,null,0,false,latestDate);
        var post=One("SELECT COALESCE(SUM(income_fen-expense_fen-fee_fen),0) n FROM finance_transactions WHERE source_sheet=? AND tx_date>?",account,S(anchor,"tx_date"))!;var delta=L(post,"n");var confirmed=L(anchor,"balance_fen");return(confirmed,confirmed+delta,S(anchor,"tx_date"),delta,latestDate is not null&&string.CompareOrdinal(latestDate,S(anchor,"tx_date"))>0,latestDate);
    }

    public List<FinanceAccountSummary> FinanceAccountSummary()
    {
        NormalizeFinanceAccounts();var rows=Rows("SELECT t.source_sheet,COUNT(*) n,COALESCE(SUM(t.income_fen),0) income_fen,COALESCE(SUM(t.expense_fen),0) expense_fen,COALESCE(SUM(t.fee_fen),0) fee_fen,(SELECT tx_date FROM finance_transactions z WHERE z.source_sheet=t.source_sheet ORDER BY z.tx_date DESC,z.source_row DESC,z.rowid DESC LIMIT 1) latest_date FROM finance_transactions t GROUP BY t.source_sheet ORDER BY income_fen DESC,t.source_sheet");
        return rows.Select(r=>{var s=AccountBalanceState(S(r,"source_sheet"));var income=L(r,"income_fen");var expense=L(r,"expense_fen");var fee=L(r,"fee_fen");return new FinanceAccountSummary(S(r,"source_sheet"),L(r,"n"),income,expense,fee,income-expense-fee,s.Current,s.Latest,s.Date,s.Delta,s.Estimated,s.LatestDate);}).ToList();
    }

    public long? FinanceKnownBalance(){var a=FinanceAccountSummary().Where(x=>x.CurrentBalanceFen.HasValue).ToList();return a.Count==0?null:a.Sum(x=>x.CurrentBalanceFen!.Value);}

    public List<Dictionary<string,object?>> FinanceMonthlySummary(string? account=null,string? courseGroup=null,string? teacherId=null,string? search=null)
    {
        var rows=FinanceRows(null,200000,account,null,courseGroup,teacherId,search);return rows.GroupBy(r=>S(r,"tx_date").Length>=7?S(r,"tx_date")[..7]:"").Where(g=>g.Key.Length>0).Select(g=>new Dictionary<string,object?>{{"month",g.Key},{"n",g.LongCount()},{"income_fen",g.Sum(x=>L(x,"income_fen"))},{"expense_fen",g.Sum(x=>L(x,"expense_fen"))},{"fee_fen",g.Sum(x=>L(x,"fee_fen"))},{"net_fen",g.Sum(x=>L(x,"income_fen")-L(x,"expense_fen")-L(x,"fee_fen"))}}).OrderByDescending(x=>S(x,"month")).ToList();
    }

    public List<Dictionary<string,object?>> FinanceCourseSummary(string? teacherId=null,string? search=null)
    {
        var rows=FinanceRows(null,200000,null,null,null,teacherId,search).Where(r=>L(r,"income_fen")>0).ToList();return rows.GroupBy(r=>string.IsNullOrWhiteSpace(S(r,"course_group"))?"未分类课程":S(r,"course_group")).Select(g=>new Dictionary<string,object?>{{"course_group",g.Key},{"n",g.LongCount()},{"income_fen",g.Sum(x=>L(x,"income_fen"))},{"fee_fen",g.Sum(x=>L(x,"fee_fen"))},{"net_income_fen",g.Sum(x=>L(x,"income_fen")-L(x,"fee_fen"))}}).OrderByDescending(x=>L(x,"income_fen")).ToList();
    }

    public List<Dictionary<string,object?>> FinanceTeacherSummary(string? courseGroup=null,string? month=null)
    {
        var rows=FinanceRows(null,200000,null,month,courseGroup,null,null).Where(r=>L(r,"income_fen")>0&&!string.IsNullOrWhiteSpace(S(r,"teacher"))).ToList();return rows.GroupBy(r=>S(r,"teacher")).Select(g=>new Dictionary<string,object?>{{"teacher",g.Key},{"n",g.LongCount()},{"income_fen",g.Sum(x=>L(x,"income_fen"))},{"fee_fen",g.Sum(x=>L(x,"fee_fen"))}}).OrderByDescending(x=>L(x,"income_fen")).ToList();
    }

    public List<Dictionary<string,object?>> FinanceCategorySummary()
    {
        return FinanceRows(limit:200000).GroupBy(r=>(S(r,"nature"),S(r,"category"))).Select(g=>new Dictionary<string,object?>{{"nature",g.Key.Item1},{"category",g.Key.Item2},{"n",g.LongCount()},{"income_fen",g.Sum(x=>L(x,"income_fen"))},{"expense_fen",g.Sum(x=>L(x,"expense_fen"))},{"fee_fen",g.Sum(x=>L(x,"fee_fen"))},{"net_fen",g.Sum(x=>L(x,"income_fen")-L(x,"expense_fen")-L(x,"fee_fen"))}}).OrderByDescending(x=>L(x,"income_fen")+L(x,"expense_fen")).ToList();
    }

    public List<Dictionary<string,object?>> FinanceBatches()=>Rows("SELECT * FROM finance_import_batches ORDER BY imported_at DESC,rowid DESC");

    private static long? OptionalBalanceFen(object? value){var t=Convert.ToString(value,CultureInfo.InvariantCulture)?.Trim();if(string.IsNullOrEmpty(t))return null;if(!decimal.TryParse(t,NumberStyles.Number,CultureInfo.InvariantCulture,out var d))throw new InvalidOperationException("余额格式不正确");return decimal.ToInt64(decimal.Round(d*100m,0,MidpointRounding.AwayFromZero));}

    private string? BestEnrollmentForCourse(string studentId,string? courseId,string onDate)
    {
        if(string.IsNullOrWhiteSpace(courseId))return null;var r=One("SELECT id FROM enrollments WHERE student_id=? AND course_id=? AND enrolled_on<=? ORDER BY enrolled_on DESC,rowid DESC LIMIT 1",studentId,courseId,onDate)??One("SELECT id FROM enrollments WHERE student_id=? AND course_id=? ORDER BY enrolled_on DESC,rowid DESC LIMIT 1",studentId,courseId);return r is null?null:S(r,"id");
    }

    private void RebuildStudentCourseHistoryEnds(string studentId,string courseId)
    {
        var rows=Rows("SELECT id,started_on FROM student_class_history WHERE student_id=? AND course_id=? ORDER BY started_on,created_at,id",studentId,courseId);for(var i=0;i<rows.Count;i++)Execute("UPDATE student_class_history SET ended_on=? WHERE id=?",i+1<rows.Count?S(rows[i+1],"started_on"):"",S(rows[i],"id"));
    }

    public void SetFinanceAcademicLink(string txId,string? studentId,string? courseId,string? classId,string? effectiveOn=null)
    {
        var tx=Get("finance_transactions",txId);var old=One("SELECT * FROM student_class_history WHERE transaction_id=?",txId);effectiveOn??=S(tx,"tx_date");
        if(string.IsNullOrWhiteSpace(studentId)||string.IsNullOrWhiteSpace(courseId)||string.IsNullOrWhiteSpace(classId))
        {
            InTransaction(()=>{Execute("DELETE FROM student_class_history WHERE transaction_id=?",txId);Execute("UPDATE finance_transactions SET course_id=?,class_id=? WHERE id=?",courseId,classId,txId);if(old is not null)RebuildStudentCourseHistoryEnds(S(old,"student_id"),S(old,"course_id"));});return;
        }
        var klass=Get("classes",classId);if(S(klass,"course_id")!=courseId)throw new InvalidOperationException("所选班级不属于当前课程");var enrollment=BestEnrollmentForCourse(studentId,courseId,effectiveOn);var teacherId=S(klass,"teacher_id");
        InTransaction(()=>
        {
            Execute("DELETE FROM student_class_history WHERE transaction_id=?",txId);Insert("student_class_history",new(){["transaction_id"]=txId,["student_id"]=studentId,["course_id"]=courseId,["class_id"]=classId,["enrollment_id"]=enrollment,["teacher_id"]=teacherId,["weekday"]=I(klass,"weekday"),["start_time"]=S(klass,"start_time"),["end_time"]=S(klass,"end_time"),["room"]=S(klass,"room"),["started_on"]=effectiveOn,["ended_on"]="",["source"]="finance",["note"]="由财务缴费记录关联班级",["created_at"]=DateTime.Now.ToString("s")});Execute("UPDATE finance_transactions SET course_id=?,class_id=?,teacher_id=?,course_group=? WHERE id=?",courseId,classId,teacherId,S(Get("courses",courseId),"name"),txId);if(old is not null)RebuildStudentCourseHistoryEnds(S(old,"student_id"),S(old,"course_id"));RebuildStudentCourseHistoryEnds(studentId,courseId);
        });
    }

    public List<Dictionary<string,object?>> FinanceClassHistoryRows(string? studentId=null,string? classId=null,string? activeOn=null)
    {
        var where=new List<string>();var args=new List<object?>();if(studentId is not null){where.Add("h.student_id=?");args.Add(studentId);}if(classId is not null){where.Add("h.class_id=?");args.Add(classId);}if(activeOn is not null){where.Add("h.started_on<=? AND (h.ended_on='' OR h.ended_on>?)");args.Add(activeOn);args.Add(activeOn);}var clause=where.Count>0?"WHERE "+string.Join(" AND ",where):"";
        return Rows($"""SELECT h.*,s.name student,c.name course,cl.name class_name,t.name teacher,ft.tx_date payment_date,ft.source_sheet account,ft.summary payment_summary,ft.income_fen,ft.expense_fen FROM student_class_history h JOIN students s ON s.id=h.student_id JOIN courses c ON c.id=h.course_id JOIN classes cl ON cl.id=h.class_id JOIN teachers t ON t.id=h.teacher_id LEFT JOIN finance_transactions ft ON ft.id=h.transaction_id {clause} ORDER BY h.started_on DESC,h.created_at DESC""",args.ToArray());
    }

    private string? InferFinanceTeacher(string studentId,string courseGroup)
    {
        var rows=Rows("""SELECT DISTINCT c.teacher_id FROM members m JOIN enrollments e ON e.id=m.enrollment_id JOIN classes c ON c.id=m.class_id JOIN courses co ON co.id=e.course_id WHERE e.student_id=? AND (m.left_on='' OR m.left_on>?) AND (co.name=? OR co.category=? OR ?='')""",studentId,DateTime.Today.ToString("yyyy-MM-dd"),courseGroup,courseGroup,courseGroup);return rows.Count==1?S(rows[0],"teacher_id"):null;
    }

    public void LinkFinanceStudent(string txId,string studentId)
    {
        var tx=Get("finance_transactions",txId);var student=Get("students",studentId);var gross=L(tx,"income_fen")!=0?L(tx,"income_fen"):L(tx,"expense_fen");var enrollment=BestEnrollmentForCourse(studentId,S(tx,"course_id"),S(tx,"tx_date"));var teacher=string.IsNullOrWhiteSpace(S(tx,"teacher_id"))?InferFinanceTeacher(studentId,S(tx,"course_group")):S(tx,"teacher_id");
        InTransaction(()=>{Execute("DELETE FROM finance_allocations WHERE transaction_id=?",txId);Insert("finance_allocations",new(){["transaction_id"]=txId,["student_id"]=studentId,["enrollment_id"]=enrollment,["amount_fen"]=gross,["confidence"]=1.0,["source"]="manual",["note"]="人工确认"});Execute("UPDATE finance_transactions SET match_status='matched',teacher_id=? WHERE id=?",teacher,txId);Log("财务匹配学员",txId+" -> "+S(student,"name"));});
        if(!string.IsNullOrWhiteSpace(S(tx,"class_id"))&&!string.IsNullOrWhiteSpace(S(tx,"course_id")))SetFinanceAcademicLink(txId,studentId,S(tx,"course_id"),S(tx,"class_id"),S(tx,"tx_date"));
    }

    public void UnlinkFinanceStudent(string txId)
    {
        var h=One("SELECT * FROM student_class_history WHERE transaction_id=?",txId);InTransaction(()=>{Execute("DELETE FROM finance_allocations WHERE transaction_id=?",txId);Execute("DELETE FROM student_class_history WHERE transaction_id=?",txId);Execute("UPDATE finance_transactions SET match_status='unmatched',class_id=NULL WHERE id=?",txId);if(h is not null)RebuildStudentCourseHistoryEnds(S(h,"student_id"),S(h,"course_id"));Log("取消财务匹配",txId);});
    }
    public void MarkFinanceNonStudent(string txId){var h=One("SELECT * FROM student_class_history WHERE transaction_id=?",txId);InTransaction(()=>{Execute("DELETE FROM finance_allocations WHERE transaction_id=?",txId);Execute("DELETE FROM student_class_history WHERE transaction_id=?",txId);Execute("UPDATE finance_transactions SET match_status='nonstudent',class_id=NULL WHERE id=?",txId);if(h is not null)RebuildStudentCourseHistoryEnds(S(h,"student_id"),S(h,"course_id"));Log("财务标记非学员",txId);});}
    public void UpdateFinanceClassification(string txId,string nature,string category){var ok=new[]{"operating_income","operating_expense","internal_transfer","refund","other_income","unknown"};if(!ok.Contains(nature))throw new InvalidOperationException("经营性质不合法");if(string.IsNullOrWhiteSpace(category))throw new InvalidOperationException("请填写财务分类");_ = Get("finance_transactions",txId);InTransaction(()=>{Execute("UPDATE finance_transactions SET nature=?,category=? WHERE id=?",nature,category.Trim(),txId);Log("修改财务分类",txId+" "+nature+" / "+category.Trim());});}

    public void UpdateFinanceAnalysisTags(string txId, string courseGroup, string? teacherId = null)
    {
        _ = Get("finance_transactions", txId);
        courseGroup = string.IsNullOrWhiteSpace(courseGroup) ? "未分类课程" : courseGroup.Trim();
        if (!string.IsNullOrWhiteSpace(teacherId)) _ = Get("teachers", teacherId);
        InTransaction(() =>
        {
            Execute("UPDATE finance_transactions SET course_group=?,teacher_id=? WHERE id=?", courseGroup, string.IsNullOrWhiteSpace(teacherId) ? null : teacherId, txId);
            Log("修改财务课程/老师", txId + " " + courseGroup + " / " + (teacherId ?? ""));
        });
    }

    public void UpdateManualFinance(string txId, Dictionary<string, object?> data)
    {
        var tx = Get("finance_transactions", txId);
        if (S(tx, "entry_source") != "manual") throw new InvalidOperationException("这不是手工流水，请使用通用财务编辑功能");
        UpdateFinanceTransaction(txId, data);
    }

    public string AddManualFinance(Dictionary<string,object?> d)
    {
        var txDate=Convert.ToString(d.GetValueOrDefault("tx_date"))?.Trim()??"";_=DateTime.ParseExact(txDate,"yyyy-MM-dd",CultureInfo.InvariantCulture);var account=ResolveFinanceAccountName(Convert.ToString(d.GetValueOrDefault("source_sheet")));var summary=Convert.ToString(d.GetValueOrDefault("summary"))?.Trim()??"";if(account.Length==0)throw new InvalidOperationException("请填写户头 / 账户");if(summary.Length==0)throw new InvalidOperationException("请填写摘要；学员缴费建议直接填写学员姓名");
        var direction=Convert.ToString(d.GetValueOrDefault("direction"));var amount=Convert.ToInt64(d.GetValueOrDefault("amount_fen")??0);var fee=Convert.ToInt64(d.GetValueOrDefault("fee_fen")??0);if(amount<=0)throw new InvalidOperationException("金额必须大于 0");if(fee<0)throw new InvalidOperationException("手续费不能为负数");var income=direction is "借" or "收入" or "income"?amount:0;var expense=direction is "贷" or "支出" or "expense"?amount:0;if(income==0&&expense==0)throw new InvalidOperationException("请选择收入或支出");
        var studentId=Convert.ToString(d.GetValueOrDefault("student_id"));var courseId=Convert.ToString(d.GetValueOrDefault("course_id"));var classId=Convert.ToString(d.GetValueOrDefault("class_id"));if(string.IsNullOrWhiteSpace(studentId))studentId=null;if(string.IsNullOrWhiteSpace(courseId))courseId=null;if(string.IsNullOrWhiteSpace(classId))classId=null;Dictionary<string,object?>? course=courseId is null?null:Get("courses",courseId);Dictionary<string,object?>? klass=classId is null?null:Get("classes",classId);if(klass is not null&&courseId is null)throw new InvalidOperationException("选择班级前请先选择课程");if(klass is not null&&S(klass,"course_id")!=courseId)throw new InvalidOperationException("所选班级不属于当前课程");if(klass is not null&&studentId is null)throw new InvalidOperationException("选择班级后必须关联学员");if(studentId is not null)_=Get("students",studentId);
        var teacher=klass is null?null:S(klass,"teacher_id");var group=course is null?CourseTaxonomy.Canonical(Convert.ToString(d.GetValueOrDefault("course_hint")),summary,Convert.ToString(d.GetValueOrDefault("raw_note"))):S(course,"name");var nature=Convert.ToString(d.GetValueOrDefault("nature"))??(income>0?"operating_income":"operating_expense");var category=Convert.ToString(d.GetValueOrDefault("category"))?.Trim();if(string.IsNullOrWhiteSpace(category))category=income>0&&studentId is not null?"学费/培训费":expense>0?"其他经营支出":"其他收入";var balance=d.ContainsKey("balance")?OptionalBalanceFen(d["balance"]):null;var batch=NewId();var txId=NewId();var nonce=NewId();
        InTransaction(()=>{Insert("finance_import_batches",new(){["id"]=batch,["file_name"]="手工录入",["file_sha256"]="manual:"+nonce,["imported_at"]=DateTime.Now.ToString("s"),["sheet_count"]=1,["row_count"]=1,["imported_count"]=1,["duplicate_count"]=0,["matched_count"]=studentId is null?0:1,["note"]="软件内手工录入"});Insert("finance_transactions",new(){["id"]=txId,["batch_id"]=batch,["source_sheet"]=account,["source_row"]=0,["tx_date"]=txDate,["summary"]=summary,["course_hint"]="",["income_fen"]=income,["expense_fen"]=expense,["fee_fen"]=fee,["balance_fen"]=balance,["direction"]=income>0?"借":"贷",["nature"]=nature,["category"]=category,["match_status"]=studentId is not null?"matched":Convert.ToBoolean(d.GetValueOrDefault("nonstudent")??false)?"nonstudent":"unmatched",["raw_note"]=Convert.ToString(d.GetValueOrDefault("raw_note"))??"",["fingerprint"]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("manual|"+nonce))).ToLowerInvariant(),["raw_json"]="{\"manual\":true}",["course_group"]=group,["teacher_id"]=teacher,["entry_source"]="manual",["course_id"]=courseId,["class_id"]=classId});var enrollment=studentId is not null&&courseId is not null?BestEnrollmentForCourse(studentId,courseId,txDate):null;if(studentId is not null)Insert("finance_allocations",new(){["transaction_id"]=txId,["student_id"]=studentId,["enrollment_id"]=enrollment,["amount_fen"]=amount,["confidence"]=1.0,["source"]="manual",["note"]="软件内手工录入"});if(classId is not null&&studentId is not null&&courseId is not null&&klass is not null){Insert("student_class_history",new(){["transaction_id"]=txId,["student_id"]=studentId,["course_id"]=courseId,["class_id"]=classId,["enrollment_id"]=enrollment,["teacher_id"]=S(klass,"teacher_id"),["weekday"]=I(klass,"weekday"),["start_time"]=S(klass,"start_time"),["end_time"]=S(klass,"end_time"),["room"]=S(klass,"room"),["started_on"]=txDate,["ended_on"]="",["source"]="finance",["note"]="由财务缴费记录关联班级",["created_at"]=DateTime.Now.ToString("s")});RebuildStudentCourseHistoryEnds(studentId,courseId);}Log("手工新增财务流水",$"{account} {txDate} {summary} {amount}");});return txId;
    }

    public void UpdateFinanceTransaction(string txId,Dictionary<string,object?> d)
    {
        var tx=Get("finance_transactions",txId);var oldHistory=One("SELECT * FROM student_class_history WHERE transaction_id=?",txId);var account=ResolveFinanceAccountName(Convert.ToString(d.GetValueOrDefault("source_sheet"))??S(tx,"source_sheet"));var txDate=Convert.ToString(d.GetValueOrDefault("tx_date"))??S(tx,"tx_date");_=DateTime.ParseExact(txDate,"yyyy-MM-dd",CultureInfo.InvariantCulture);var summary=Convert.ToString(d.GetValueOrDefault("summary"))?.Trim()??S(tx,"summary");if(account.Length==0||summary.Length==0)throw new InvalidOperationException("户头和摘要不能为空");
        var direction=Convert.ToString(d.GetValueOrDefault("direction"))??S(tx,"direction");var amount=d.ContainsKey("amount_fen")?Convert.ToInt64(d["amount_fen"]):Math.Max(L(tx,"income_fen"),L(tx,"expense_fen"));var fee=d.ContainsKey("fee_fen")?Convert.ToInt64(d["fee_fen"]):L(tx,"fee_fen");var income=direction is "借" or "收入" or "income"?amount:0;var expense=direction is "贷" or "支出" or "expense"?amount:0;var studentId=Convert.ToString(d.GetValueOrDefault("student_id"));if(string.IsNullOrWhiteSpace(studentId))studentId=null;var courseId=Convert.ToString(d.GetValueOrDefault("course_id"));if(string.IsNullOrWhiteSpace(courseId))courseId=null;var classId=Convert.ToString(d.GetValueOrDefault("class_id"));if(string.IsNullOrWhiteSpace(classId))classId=null;var nature=Convert.ToString(d.GetValueOrDefault("nature"))??S(tx,"nature");var category=Convert.ToString(d.GetValueOrDefault("category"))??S(tx,"category");var rawNote=Convert.ToString(d.GetValueOrDefault("raw_note"))??S(tx,"raw_note");var balance=d.ContainsKey("balance")?OptionalBalanceFen(d["balance"]):(tx["balance_fen"] is null?null:L(tx,"balance_fen"));Dictionary<string,object?>? course=courseId is null?null:Get("courses",courseId);Dictionary<string,object?>? klass=classId is null?null:Get("classes",classId);if(klass is not null&&courseId is null)throw new InvalidOperationException("选择班级前请先选择课程");if(klass is not null&&S(klass,"course_id")!=courseId)throw new InvalidOperationException("所选班级不属于当前课程");if(klass is not null&&studentId is null)throw new InvalidOperationException("选择班级后必须关联学员");var teacher=klass is not null?S(klass,"teacher_id"):Convert.ToString(d.GetValueOrDefault("teacher_id"));var group=course is not null?S(course,"name"):CourseTaxonomy.Canonical(S(tx,"course_hint"),summary,rawNote);
        InTransaction(()=>{Execute("UPDATE finance_transactions SET source_sheet=?,tx_date=?,summary=?,income_fen=?,expense_fen=?,fee_fen=?,balance_fen=?,direction=?,nature=?,category=?,course_group=?,teacher_id=?,course_id=?,class_id=?,raw_note=? WHERE id=?",account,txDate,summary,income,expense,fee,balance,income>0?"借":"贷",nature,category,group,teacher,courseId,classId,rawNote,txId);Execute("DELETE FROM finance_allocations WHERE transaction_id=?",txId);if(studentId is not null){var enrollment=courseId is null?null:BestEnrollmentForCourse(studentId,courseId,txDate);Insert("finance_allocations",new(){["transaction_id"]=txId,["student_id"]=studentId,["enrollment_id"]=enrollment,["amount_fen"]=Math.Max(income,expense),["confidence"]=1.0,["source"]="manual",["note"]="人工编辑"});Execute("UPDATE finance_transactions SET match_status='matched' WHERE id=?",txId);}else Execute("UPDATE finance_transactions SET match_status=? WHERE id=?",Convert.ToString(d.GetValueOrDefault("match_status"))??S(tx,"match_status"),txId);Execute("DELETE FROM student_class_history WHERE transaction_id=?",txId);if(oldHistory is not null)RebuildStudentCourseHistoryEnds(S(oldHistory,"student_id"),S(oldHistory,"course_id"));if(studentId is not null&&courseId is not null&&classId is not null)SetFinanceAcademicLink(txId,studentId,courseId,classId,txDate);Log("编辑财务流水",txId);});
    }

    public void DeleteManualFinance(string txId)
    {
        var tx=Get("finance_transactions",txId);if(S(tx,"entry_source")!="manual")throw new InvalidOperationException("Excel 导入流水不能直接删除");var batch=S(tx,"batch_id");InTransaction(()=>{Execute("DELETE FROM finance_allocations WHERE transaction_id=?",txId);Execute("DELETE FROM student_class_history WHERE transaction_id=?",txId);Execute("DELETE FROM finance_transactions WHERE id=?",txId);Execute("DELETE FROM finance_import_batches WHERE id=?",batch);Log("删除手工财务流水",txId);});
    }

    public void Backup(string? target=null)
    {
        Directory.CreateDirectory(AppPaths.BackupDirectory);target??=Path.Combine(AppPaths.BackupDirectory,$"kuka-{DateTime.Now:yyyyMMdd-HHmmss-fff}.sqlite3");if(Path.GetFullPath(target)==Path.GetFullPath(DatabasePath))throw new InvalidOperationException("备份不能覆盖正在使用的数据");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);using var dest=new SqliteConnection($"Data Source={target}");dest.Open();_db.BackupDatabase(dest);if(target.StartsWith(AppPaths.BackupDirectory,StringComparison.OrdinalIgnoreCase)){foreach(var f in Directory.GetFiles(AppPaths.BackupDirectory,"kuka-*.sqlite3").OrderByDescending(File.GetLastWriteTimeUtc).Skip(40))try{File.Delete(f);}catch{}}
    }

    public void RestoreDatabase(string source)
    {
        if(!File.Exists(source))throw new FileNotFoundException("备份不存在",source);using(var test=new SqliteConnection($"Data Source={source};Mode=ReadOnly")){test.Open();using var c=test.CreateCommand();c.CommandText="PRAGMA quick_check";if(Convert.ToString(c.ExecuteScalar())!="ok")throw new InvalidOperationException("备份数据库完整性检查失败");}
        Backup();using var src=new SqliteConnection($"Data Source={source};Mode=ReadOnly");src.Open();src.BackupDatabase(_db);IntegrityCheck();Log("恢复数据库",Path.GetFileName(source));
    }

    public IReadOnlyList<string> BusinessTables => Tables.OrderBy(x=>x).ToList();

    public Dictionary<string,List<Dictionary<string,object?>>> Snapshot()
        => Tables.ToDictionary(t=>t,t=>Rows($"SELECT * FROM {t}"),StringComparer.OrdinalIgnoreCase);

    public void RestoreSnapshot(Dictionary<string,List<Dictionary<string,object?>>> data)
    {
        if(Tables.Any(t=>!data.ContainsKey(t))||data.Keys.Any(k=>!Tables.Contains(k)))throw new InvalidOperationException("数据表不完整");
        Backup();
        InTransaction(()=>
        {
            Execute("PRAGMA defer_foreign_keys=ON");
            foreach(var t in new[]{"student_class_history","finance_allocations","finance_transactions","finance_import_batches","ledger","makeups","attendance","sessions","members","growth","classes","enrollments","students","teachers","courses","audit"}) Execute($"DELETE FROM {t}");
            foreach(var t in new[]{"courses","teachers","students","enrollments","classes","members","sessions","attendance","makeups","ledger","growth","audit","finance_import_batches","finance_transactions","finance_allocations","student_class_history"})
                foreach(var row in data[t]) Insert(t,new Dictionary<string,object?>(row,StringComparer.OrdinalIgnoreCase));
            Log("恢复全部业务数据","完整交换文件 / 数据库恢复");
        });
        IntegrityCheck();
    }

}
