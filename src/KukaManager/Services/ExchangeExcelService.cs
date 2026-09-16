using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using KukaManager.Data;
using static KukaManager.Utils.Value;

namespace KukaManager.Services;

public sealed class ExchangeExcelService(KukaStore store)
{
    private static readonly Dictionary<string,(string Title,string[] Headers)> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["courses"]=("课程目录",["编号","课程名称","科目","启用"]),
        ["teachers"]=("老师名录",["编号","姓名","电话","启用"]),
        ["students"]=("学员档案",["编号","姓名","性别","出生日期","家长姓名","家长关系","联系电话","微信号","家庭住址","备注","启用"]),
        ["enrollments"]=("报名原始记录",["编号","学员编号","课程编号","报名日期","报名项目","原价分","优惠分","优惠原因","审批人","邀约老师","报名老师","报名课次","备注"]),
        ["classes"]=("班级设置",["编号","班级名称","课程编号","老师编号","教室","星期索引","开始时间","结束时间","开始日期","结束日期","启用"]),
        ["members"]=("入班记录",["编号","班级编号","报名编号","入班日期","退班生效日期"]),
        ["sessions"]=("排课记录",["编号","班级编号","日期","开始时间","结束时间","老师编号","停课","原因"]),
        ["attendance"]=("出勤记录",["编号","排课编号","报名编号","出勤状态","原因"]),
        ["makeups"]=("补课记录",["编号","出勤编号","日期","开始时间","结束时间","老师编号","状态","备注"]),
        ["ledger"]=("课消原始流水",["编号","报名编号","出勤编号","发生时间","课次变化","金额变化分","剩余课次","剩余金额分","原因"]),
        ["growth"]=("成长原始记录",["编号","学员编号","课程编号","日期","开始时间","结束时间","老师编号","学习项目","学习成果","学习评价","下次目标"]),
        ["audit"]=("操作记录",["编号","时间","操作人","操作","详情"]),
        ["finance_import_batches"]=("财务导入批次原始记录",["编号","文件名","文件SHA256","导入时间","工作表数","识别行数","新增行数","重复跳过","自动匹配","备注"]),
        ["finance_transactions"]=("财务流水原始记录",["编号","导入批次编号","来源表","来源行","日期","摘要","课程提示","收入分","支出分","手续费分","余额分","方向","经营性质","分类","匹配状态","原备注","指纹","原始JSON","标准课程","老师编号","录入来源","课程编号","班级编号"]),
        ["finance_allocations"]=("财务学员关联原始记录",["编号","财务流水编号","学员编号","报名编号","关联金额分","置信度","来源","备注"]),
        ["student_class_history"]=("学员班级缴费历史",["编号","财务流水编号","学员编号","课程编号","班级编号","报名编号","老师编号","星期索引","开始时间","结束时间","教室","阶段开始","阶段结束","来源","备注","创建时间"])
    };

    public void StudentTemplate(string path)
    {
        using var wb=new XLWorkbook();var ws=wb.Worksheets.Add("学员导入");var headers=new[]{"姓名*","性别*","出生日期*","家长姓名*","关系","联系电话*","微信号","家庭住址","备注"};for(var i=0;i<headers.Length;i++){ws.Cell(1,i+1).Value=headers[i];Header(ws.Cell(1,i+1));}ws.Cell(2,1).Value="张三";ws.Cell(2,2).Value="男";ws.Cell(2,3).Value="2018-01-01";ws.Cell(2,4).Value="张妈妈";ws.Cell(2,5).Value="妈妈";ws.Cell(2,6).Value="13800000000";ws.Columns().AdjustToContents();wb.SaveAs(path);
    }

    public (List<Dictionary<string,object?>> Rows,int Skipped) ReadStudents(string path)
    {
        using var wb=new XLWorkbook(path);var ws=wb.Worksheet(1);var rows=new List<Dictionary<string,object?>>();var skipped=0;var existing=store.Rows("SELECT name,birth,phone FROM students").Select(r=>$"{S(r,"name")}|{S(r,"birth")}|{S(r,"phone")}").ToHashSet();
        foreach(var r in ws.RowsUsed().Skip(1))
        {
            var name=r.Cell(1).GetString().Trim();if(name.Length==0)continue;var gender=r.Cell(2).GetString().Trim();var birth=CellDate(r.Cell(3));var parent=r.Cell(4).GetString().Trim();var relation=r.Cell(5).GetString().Trim();var phone=r.Cell(6).GetString().Trim();if(gender is not "男" and not "女")throw new InvalidOperationException($"{name}：性别必须是男或女");if(parent.Length==0||phone.Length==0)throw new InvalidOperationException($"{name}：家长和联系电话为必填项");var key=$"{name}|{birth}|{phone}";if(existing.Contains(key)){skipped++;continue;}existing.Add(key);rows.Add(new(){{"name",name},{"gender",gender},{"birth",birth},{"parent",parent},{"relation",relation.Length==0?"其他监护人":relation},{"phone",phone},{"wechat",r.Cell(7).GetString().Trim()},{"address",r.Cell(8).GetString().Trim()},{"note",r.Cell(9).GetString().Trim()},{"active",1}});
        }
        return(rows,skipped);
    }

    public void ImportStudents(IEnumerable<Dictionary<string,object?>> rows)
    {
        var list = rows.ToList();
        foreach (var row in list) store.SaveStudent(row);
        store.Log("批量导入学员", list.Count + " 人");
    }

    public void ExportFull(string path,DateTime weekStart)
    {
        using var wb=new XLWorkbook();
        var students=store.Rows("SELECT * FROM students ORDER BY name");Sheet(wb,"学员信息",["姓名","性别","出生年月日","年龄","家长","关系","联系电话","微信号","家庭住址","备注"],students.Select(s=>new object?[]{S(s,"name"),S(s,"gender"),S(s,"birth"),KukaStore.Age(S(s,"birth")),S(s,"parent"),S(s,"relation"),S(s,"phone"),S(s,"wechat"),S(s,"address"),S(s,"note")}));
        var enrollments=store.Rows("SELECT e.*,s.name student,c.name course FROM enrollments e JOIN students s ON s.id=e.student_id JOIN courses c ON c.id=e.course_id ORDER BY enrolled_on DESC");var balances=store.BalanceMap();Sheet(wb,"报名与余额",["学员","课程","报名项目","报名日期","原价","优惠","折后课包金额","优惠原因","审批人","邀约老师","报名老师","报名课次","剩余课次","剩余金额"],enrollments.Select(e=>{var b=balances.GetValueOrDefault(S(e,"id"),new(0,0));return new object?[]{S(e,"student"),S(e,"course"),S(e,"project"),S(e,"enrolled_on"),L(e,"list_fen")/100m,L(e,"discount_fen")/100m,(L(e,"list_fen")-L(e,"discount_fen"))/100m,S(e,"reason"),S(e,"approver"),S(e,"inviter"),S(e,"seller"),I(e,"lessons"),b.Lessons,b.AmountFen/100m};}));
        var ledger=store.Rows("SELECT l.*,s.name student,c.name course,cl.name class_name,se.day FROM ledger l JOIN enrollments e ON e.id=l.enrollment_id JOIN students s ON s.id=e.student_id JOIN courses c ON c.id=e.course_id JOIN attendance a ON a.id=l.attendance_id JOIN sessions se ON se.id=a.session_id JOIN classes cl ON cl.id=se.class_id ORDER BY l.at,l.rowid");Sheet(wb,"课消明细",["学员","课程","班级","原排课日期","记账时间","扣前课次","本次变化","本次金额","剩余课次","剩余金额","原因"],ledger.Select(r=>new object?[]{S(r,"student"),S(r,"course"),S(r,"class_name"),S(r,"day"),S(r,"at"),I(r,"remaining")-I(r,"delta"),I(r,"delta"),L(r,"amount_fen")/100m,I(r,"remaining"),L(r,"remaining_fen")/100m,S(r,"reason")}));
        var growth=store.Rows("SELECT g.*,s.name student,c.name course,t.name teacher FROM growth g JOIN students s ON s.id=g.student_id JOIN courses c ON c.id=g.course_id JOIN teachers t ON t.id=g.teacher_id ORDER BY day DESC");Sheet(wb,"学习成长档案",["学员","日期","开始时间","结束时间","课程","学习项目","授课老师","学习成果","学习评价","下次目标"],growth.Select(g=>new object?[]{S(g,"student"),S(g,"day"),S(g,"start_time"),S(g,"end_time"),S(g,"course"),S(g,"project"),S(g,"teacher"),S(g,"outcome"),S(g,"evaluation"),S(g,"next_goal")}));
        var monday=weekStart.Date.AddDays(-(((int)weekStart.DayOfWeek+6)%7));var scheduleRows=new List<object?[]>();for(var i=0;i<7;i++){var d=monday.AddDays(i);foreach(var s in store.Rows("SELECT s.*,c.name class_name,c.room,co.name course,t.name teacher FROM sessions s JOIN classes c ON c.id=s.class_id JOIN courses co ON co.id=c.course_id JOIN teachers t ON t.id=s.teacher_id WHERE s.day=? ORDER BY s.start_time",d.ToString("yyyy-MM-dd")))scheduleRows.Add(new object?[]{d.ToString("yyyy-MM-dd"),"星期"+"一二三四五六日"[i],S(s,"start_time")+"–"+S(s,"end_time"),S(s,"course"),S(s,"class_name"),S(s,"teacher"),S(s,"room"),string.Join('、',store.ClassRosterNames(S(s,"class_id"),S(s,"day"))),B(s,"cancelled")?"停课":"正常",S(s,"reason")});}Sheet(wb,"周排课表",["日期","星期","时间","课程","班级","老师","教室","学员","状态","原因"],scheduleRows);
        var snapshot=store.Snapshot();var meta=wb.Worksheets.Add("导入说明");meta.Cell(1,1).Value="酷咔全量交换文件";meta.Cell(1,2).Value="5";meta.Cell(2,1).Value="模式";meta.Cell(2,2).Value="完整恢复，不合并；恢复前自动备份";
        var mr=4;foreach(var table in new[]{"courses","teachers","students","enrollments","classes","members","sessions","attendance","makeups","ledger","growth","audit","finance_import_batches","finance_transactions","finance_allocations","student_class_history"}){var cols=store.Rows($"PRAGMA table_info({table})").Select(x=>S(x,"name")).ToArray();var title=Labels[table].Title;Sheet(wb,title,Labels[table].Headers,snapshot[table].Select(row=>cols.Select(c=>row.GetValueOrDefault(c)).ToArray()));meta.Cell(mr,1).Value=table;meta.Cell(mr,2).Value=JsonSerializer.Serialize(cols);mr++;}
        wb.SaveAs(path);
    }

    public Dictionary<string,List<Dictionary<string,object?>>> ReadFull(string path)
    {
        using var wb = new XLWorkbook(path);
        if (!wb.TryGetWorksheet("导入说明", out var meta)) throw new InvalidOperationException("请选择酷咔导出的完整 Excel 文件");
        var version = meta.Cell(1,2).GetString().Trim();
        if (version is not "5" and not "4" and not "3" and not "2" and not "1") throw new InvalidOperationException("不支持的交换文件版本");

        var map = new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in meta.RowsUsed().Skip(3))
        {
            var table = row.Cell(1).GetString().Trim();
            if (table.Length == 0) continue;
            try { map[table] = JsonSerializer.Deserialize<string[]>(row.Cell(2).GetString()) ?? []; }
            catch { }
        }

        var allTables = new[] { "courses","teachers","students","enrollments","classes","members","sessions","attendance","makeups","ledger","growth","audit","finance_import_batches","finance_transactions","finance_allocations","student_class_history" };
        var financeTables = new HashSet<string>(["finance_import_batches","finance_transactions","finance_allocations","student_class_history"], StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string,List<Dictionary<string,object?>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in allTables)
        {
            if (!map.TryGetValue(table, out var cols) || !wb.TryGetWorksheet(Labels[table].Title, out var ws))
            {
                if ((version == "1" && financeTables.Contains(table)) || ((version is "1" or "2" or "3" or "4") && table == "student_class_history"))
                {
                    result[table] = [];
                    continue;
                }
                throw new InvalidOperationException("交换文件缺少数据表：" + table);
            }

            var list = new List<Dictionary<string,object?>>();
            foreach (var r in ws.RowsUsed().Skip(3))
            {
                var values = new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);
                var empty = true;
                for (var i = 0; i < cols.Length; i++)
                {
                    var cell = r.Cell(i + 1);
                    object? value = cell.IsEmpty() ? null : cell.DataType switch
                    {
                        XLDataType.Number => cell.GetDouble(),
                        XLDataType.Boolean => cell.GetBoolean() ? 1 : 0,
                        XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd"),
                        _ => cell.GetString()
                    };
                    if (value is not null) empty = false;
                    values[cols[i]] = value;
                }
                if (empty) continue;

                if (table == "finance_transactions" && (version is "1" or "2"))
                {
                    if (!values.ContainsKey("course_group")) values["course_group"] = CourseTaxonomy.Canonical(S(values,"course_hint"), S(values,"summary"), S(values,"raw_note"));
                    if (!values.ContainsKey("teacher_id")) values["teacher_id"] = null;
                    if (!values.ContainsKey("entry_source")) values["entry_source"] = "import";
                }
                if (table == "finance_transactions" && (version is "1" or "2" or "3"))
                {
                    if (!values.ContainsKey("course_id")) values["course_id"] = null;
                    if (!values.ContainsKey("class_id")) values["class_id"] = null;
                }
                list.Add(values);
            }
            result[table] = list;
        }
        return result;
    }

    private static string CellDate(IXLCell c){if(c.TryGetValue<DateTime>(out var dt))return dt.ToString("yyyy-MM-dd");var t=c.GetString().Trim();if(DateTime.TryParse(t,CultureInfo.CurrentCulture,DateTimeStyles.None,out dt))return dt.ToString("yyyy-MM-dd");throw new InvalidOperationException("出生日期格式不正确："+t);}
    private static void Header(IXLCell c){c.Style.Font.Bold=true;c.Style.Fill.BackgroundColor=XLColor.FromHtml("#216B60");c.Style.Font.FontColor=XLColor.White;}
    private static void SetCell(IXLCell cell,object? value){switch(value){case null:cell.Value="";break;case int x:cell.Value=x;break;case long x:cell.Value=x;break;case double x:cell.Value=x;break;case decimal x:cell.Value=x;break;case bool x:cell.Value=x;break;case DateTime x:cell.Value=x;break;default:cell.Value=Convert.ToString(value,CultureInfo.InvariantCulture)??"";break;}}
    private static void Sheet(XLWorkbook wb,string title,string[] headers,IEnumerable<object?[]> rows){var ws=wb.Worksheets.Add(title);ws.Cell(1,1).Value="酷咔管理系统 · "+title;ws.Range(1,1,1,headers.Length).Merge();ws.Cell(1,1).Style.Font.Bold=true;ws.Cell(1,1).Style.Font.FontSize=18;ws.Cell(2,1).Value=$"导出时间 {DateTime.Now:yyyy-MM-dd HH:mm}";ws.Range(2,1,2,headers.Length).Merge();for(var i=0;i<headers.Length;i++){ws.Cell(3,i+1).Value=headers[i];Header(ws.Cell(3,i+1));}var rr=4;foreach(var row in rows){for(var i=0;i<row.Length;i++)SetCell(ws.Cell(rr,i+1),row[i]);rr++;}ws.SheetView.FreezeRows(3);ws.RangeUsed()?.SetAutoFilter();ws.Columns().AdjustToContents(10,40);}
}
