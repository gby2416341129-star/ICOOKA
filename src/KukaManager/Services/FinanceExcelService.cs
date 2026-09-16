using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ExcelDataReader;
using KukaManager.Data;
using KukaManager.Utils;
using static KukaManager.Utils.Value;

namespace KukaManager.Services;

public sealed class FinanceExcelService(KukaStore store)
{
    static FinanceExcelService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public sealed record ParsedFinanceFile(Dictionary<string,object?> Meta,List<Dictionary<string,object?>> Rows,List<string> Warnings);

    public ParsedFinanceFile Parse(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant(); if (ext != ".xls" && ext != ".xlsx" && ext != ".xlsm") throw new InvalidOperationException("请选择 .xls、.xlsx 或 .xlsm 财务表格");
        using var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        using var reader=ext==".xls"?ExcelReaderFactory.CreateBinaryReader(stream):ExcelReaderFactory.CreateOpenXmlReader(stream);
        var ds=reader.AsDataSet(new ExcelDataSetConfiguration{ConfigureDataTable=_=>new ExcelDataTableConfiguration{UseHeaderRow=false}});
        var all=new List<Dictionary<string,object?>>();var warnings=new List<string>();
        foreach(DataTable table in ds.Tables)
        {
            var rows=new List<List<object?>>();foreach(DataRow dr in table.Rows)rows.Add(dr.ItemArray.Cast<object?>().ToList());
            var parsed=ParseLedger(table.TableName,rows)??ParseCompact(table.TableName,rows);if(parsed is null)warnings.Add(table.TableName+"：未识别表格结构，已跳过");else all.AddRange(parsed);
        }
        if(all.Count==0)throw new InvalidOperationException("没有识别到可导入的财务流水");Enrich(all);
        var meta=new Dictionary<string,object?>{{"file_name",Path.GetFileName(path)},{"file_sha256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()},{"sheet_count",ds.Tables.Count},{"note",string.Join('；',warnings)}};
        return new(meta,all,warnings);
    }

    private static string T(object? v)=>v is null||v is DBNull?"":Convert.ToString(v,CultureInfo.InvariantCulture)?.Trim()??"";
    private static long Fen(object? v){if(v is null||v is DBNull||T(v).Length==0)return 0;if(v is double d)return decimal.ToInt64(decimal.Round((decimal)d*100m,0,MidpointRounding.AwayFromZero));if(v is decimal m)return decimal.ToInt64(decimal.Round(m*100m,0,MidpointRounding.AwayFromZero));return decimal.TryParse(T(v),NumberStyles.Any,CultureInfo.InvariantCulture,out var x)?decimal.ToInt64(decimal.Round(x*100m,0,MidpointRounding.AwayFromZero)):0;}
    private static int? FindHeader(List<List<object?>> rows)
    {
        for(var i=0;i<Math.Min(12,rows.Count);i++){var vals=rows[i].Select(T).ToList();if(vals.Contains("摘要")&&vals.Any(x=>x.Contains("借方金额"))&&vals.Any(x=>x.Contains("贷方金额")))return i;}return null;
    }
    private static int FindCol(List<string> h,Func<string,bool> p){for(var i=0;i<h.Count;i++)if(p(h[i]))return i;return -1;}

    private static List<Dictionary<string,object?>>? ParseLedger(string sheet,List<List<object?>> rows)
    {
        var hi=FindHeader(rows);if(hi is null)return null;var header=rows[hi.Value].Select(T).ToList();var summaryCol=FindCol(header,x=>x=="摘要");var incomeCol=FindCol(header,x=>x.Contains("借方金额"));var expenseCol=FindCol(header,x=>x.Contains("贷方金额"));var feeCol=FindCol(header,x=>x.Contains("手续费"));var directionCol=FindCol(header,x=>x.Contains("借或贷"));var balanceCol=FindCol(header,x=>x=="余额");
        var year=DateTime.Today.Year;foreach(var row in rows.Skip(Math.Max(0,hi.Value-2)).Take(4))foreach(var v in row)if(v is double n&&n is >=1900 and <=2100){year=(int)n;goto FoundYear;}FoundYear:;
        var courseCol=-1;foreach(var row in rows.Skip(hi.Value).Take(3))for(var i=0;i<row.Count;i++)if(T(row[i]).Contains("课程"))courseCol=i;
        var output=new List<Dictionary<string,object?>>();
        for(var ri=hi.Value+1;ri<rows.Count;ri++)
        {
            var vals=rows[ri];object? At(int c)=>c>=0&&c<vals.Count?vals[c]:null;var first=T(At(0));var summary=T(At(summaryCol));if(first.Contains("合计")||summary.Contains("合计"))continue;var income=At(incomeCol);var expense=At(expenseCol);var fee=At(feeCol);if(Fen(income)==0&&Fen(expense)==0&&Fen(fee)==0)continue;
            if(!double.TryParse(T(At(0)),NumberStyles.Any,CultureInfo.InvariantCulture,out var mon)||!double.TryParse(T(At(1)),NumberStyles.Any,CultureInfo.InvariantCulture,out var dayNum))continue;DateTime date;try{date=new DateTime(year,(int)mon,(int)dayNum);}catch{continue;}if(summary.Contains("余额")&&Fen(income)==0&&Fen(expense)==0)continue;
            long? balance=null;if(At(balanceCol) is double or decimal or int or long)balance=Fen(At(balanceCol));var known=new HashSet<int>{0,1,summaryCol,incomeCol,expenseCol,feeCol,directionCol,balanceCol,courseCol};var extra=new List<string>();for(var c=0;c<vals.Count;c++){var s=T(vals[c]);if(!known.Contains(c)&&s.Length>0&&vals[c] is not double and not decimal and not int and not long)extra.Add(s);}
            output.Add(new(){{"source_sheet",sheet},{"source_row",ri+1},{"tx_date",date.ToString("yyyy-MM-dd")},{"summary",summary},{"course_hint",T(At(courseCol))},{"income_fen",Fen(income)},{"expense_fen",Fen(expense)},{"fee_fen",Fen(fee)},{"balance_fen",balance},{"direction",T(At(directionCol))},{"raw_note",string.Join('；',extra)},{"raw_json",JsonSerializer.Serialize(vals)}});
        }
        return output;
    }

    private static List<Dictionary<string,object?>>? ParseCompact(string sheet,List<List<object?>> rows)
    {
        var output=new List<Dictionary<string,object?>>();for(var i=0;i<rows.Count;i++){var r=rows[i];if(r.Count<4)continue;if(!double.TryParse(T(r[0]),NumberStyles.Any,CultureInfo.InvariantCulture,out var serial)||serial is <=30000 or >=80000)continue;if(Fen(r[3])<=0)continue;var d=DateTime.FromOADate(serial);output.Add(new(){{"source_sheet",sheet},{"source_row",i+1},{"tx_date",d.ToString("yyyy-MM-dd")},{"summary",T(r.ElementAtOrDefault(1))},{"course_hint",T(r.ElementAtOrDefault(2))},{"income_fen",Fen(r.ElementAtOrDefault(3))},{"expense_fen",0L},{"fee_fen",Fen(r.ElementAtOrDefault(4))},{"balance_fen",null},{"direction","借"},{"raw_note",T(r.ElementAtOrDefault(5))},{"raw_json",JsonSerializer.Serialize(r)}});}return output.Count==0?null:output;
    }

    private static string Normalize(string? s)=>Regex.Replace((s??"").Replace(" ","").Replace("　",""),"[·•，,。.;；:：/\\_-]","").ToLowerInvariant();
    private static string NameCandidate(string summary){var raw=summary.Trim();var m=Regex.Match(raw,@"未备注[（(]([^）)]+)[）)]");if(m.Success)raw=m.Groups[1].Value;raw=Regex.Split(raw,@"[（(]")[0];raw=Regex.Split(raw,"转给|转至|转入|退款|退费")[0];return raw.Trim(' ','-','—','·');}

    private void Enrich(List<Dictionary<string,object?>> rows)
    {
        var students=store.Rows("SELECT id,name,active FROM students");var enrollments=store.Rows("SELECT e.*,c.name course,c.category FROM enrollments e JOIN courses c ON c.id=e.course_id ORDER BY e.enrolled_on DESC");
        foreach(var tx in rows)
        {
            var summary=S(tx,"summary");var cand=Normalize(NameCandidate(summary));var exact=students.Where(s=>cand.Length>0&&Normalize(S(s,"name"))==cand).ToList();var contains=students.Where(s=>Normalize(S(s,"name")).Length>=2&&Normalize(summary).Contains(Normalize(S(s,"name")))).ToList();var student=exact.Count==1?exact[0]:contains.Count==1?contains[0]:null;var confidence=exact.Count==1?1.0:contains.Count==1?0.92:0.0;string? enrollmentId=null;Dictionary<string,object?>? best=null;
            if(student is not null){var bestScore=-1;var gross=L(tx,"income_fen")!=0?L(tx,"income_fen"):L(tx,"expense_fen");var hint=Normalize(summary+" "+S(tx,"course_hint"));foreach(var e in enrollments.Where(e=>S(e,"student_id")==S(student,"id"))){var score=0;if(Normalize(S(e,"course")).Length>0&&hint.Contains(Normalize(S(e,"course"))))score+=3;if(Normalize(S(e,"category")).Length>0&&hint.Contains(Normalize(S(e,"category"))))score++;if(Math.Abs((L(e,"list_fen")-L(e,"discount_fen"))-gross)<=100)score+=2;if(score>bestScore){best=e;bestScore=score;}}if(best is not null&&bestScore>=2)enrollmentId=S(best,"id");}
            var (nature,category)=Classify(tx,student is not null);var group=CourseTaxonomy.Canonical(S(tx,"course_hint"),summary,S(tx,"raw_note"));if(group=="未分类课程"&&best is not null)group=CourseTaxonomy.Canonical(S(best,"course"),S(best,"category"));tx["suggested_student_id"]=student is null?null:S(student,"id");tx["suggested_enrollment_id"]=enrollmentId;tx["confidence"]=confidence;tx["match_status"]=student is not null&&confidence>=0.9?"matched":"unmatched";tx["nature"]=nature;tx["category"]=category;tx["course_group"]=group;tx["teacher_id"]=null;var key=string.Join('|',S(tx,"source_sheet"),I(tx,"source_row").ToString(),S(tx,"tx_date"),summary,L(tx,"income_fen").ToString(),L(tx,"expense_fen").ToString(),L(tx,"fee_fen").ToString(),tx["balance_fen"]?.ToString()??"");tx["fingerprint"]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        }
    }

    private static (string Nature,string Category) Classify(Dictionary<string,object?> tx,bool matched)
    {
        var text=(S(tx,"summary")+" "+S(tx,"course_hint")+" "+S(tx,"raw_note")).ToLowerInvariant();var income=L(tx,"income_fen");var expense=L(tx,"expense_fen");
        if(expense>0){if(new[]{"转给","内部转","划转","转至"}.Any(text.Contains))return("internal_transfer","内部划转");if(new[]{"退款","退费","退学"}.Any(text.Contains))return("refund","学员退款");if(text.Contains("工资")||text.Contains("薪资"))return("operating_expense","工资薪酬");if(text.Contains("课时费"))return("operating_expense","教师/合作课时费");return("operating_expense","其他经营支出");}
        if(income>0){if(text.Contains("课时费")&&new[]{"小学","学校","校"}.Any(text.Contains))return("operating_income","合作学校课时费");if(new[]{"器材","比赛","培训费"}.Any(text.Contains)&&!matched)return("operating_income","器材/比赛/培训收入");if(text.Contains("赔偿"))return("other_income","其他收入");if(matched||new[]{"数学","编程","python","c++","math code","暑假","秋季","定金","尾款","课程"}.Any(text.Contains))return("operating_income","学费/培训费");return("other_income","其他收入");}return("unknown","待分类");
    }

    public Dictionary<string,long> Summary(IReadOnlyList<Dictionary<string,object?>> rows)=>new(){{"rows",rows.Count},{"income_fen",rows.Sum(r=>L(r,"income_fen"))},{"expense_fen",rows.Sum(r=>L(r,"expense_fen"))},{"fee_fen",rows.Sum(r=>L(r,"fee_fen"))},{"matched",rows.Count(r=>S(r,"match_status")=="matched")},{"unmatched",rows.Count(r=>S(r,"match_status")!="matched")}};

    public void ExportFinance(string path)
    {
        using var wb=new XLWorkbook();
        static void SetCell(IXLCell cell, object? value)
        {
            switch (value)
            {
                case null: cell.Value = ""; break;
                case int x: cell.Value = x; break;
                case long x: cell.Value = x; break;
                case double x: cell.Value = x; break;
                case decimal x: cell.Value = x; break;
                case bool x: cell.Value = x; break;
                case DateTime x: cell.Value = x; break;
                default: cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""; break;
            }
        }
        void Sheet(string name,string[] headers,IEnumerable<object?[]> source,HashSet<int>? moneyCols=null){var ws=wb.Worksheets.Add(name);for(var i=0;i<headers.Length;i++){ws.Cell(1,i+1).Value=headers[i];ws.Cell(1,i+1).Style.Font.Bold=true;ws.Cell(1,i+1).Style.Fill.BackgroundColor=XLColor.FromHtml("#0F6CBD");ws.Cell(1,i+1).Style.Font.FontColor=XLColor.White;}var r=2;foreach(var row in source){for(var c=0;c<row.Length;c++){SetCell(ws.Cell(r,c+1),row[c]);if(moneyCols?.Contains(c+1)==true)ws.Cell(r,c+1).Style.NumberFormat.Format="¥#,##0.00;[Red](¥#,##0.00);-";}r++;}ws.SheetView.FreezeRows(1);ws.RangeUsed()?.SetAutoFilter();ws.Columns().AdjustToContents(8,34);}
        var rows=store.FinanceRows(limit:200000);Sheet("规范财务流水",["日期","户头/账户","借/贷","摘要/学生","关联学员","分析课程","课程目录","班级","老师","原课程提示","收入","支出","手续费","净现金变动","可确认余额","经营性质","财务分类","匹配状态","录入来源","来源行","原备注","流水ID"],rows.Select(r=>new object?[]{S(r,"tx_date"),S(r,"source_sheet"),S(r,"direction"),S(r,"summary"),S(r,"students"),S(r,"course_group"),S(r,"catalog_course"),S(r,"class_name"),S(r,"teacher"),S(r,"course_hint"),L(r,"income_fen")/100m,L(r,"expense_fen")/100m,L(r,"fee_fen")/100m,(L(r,"income_fen")-L(r,"expense_fen")-L(r,"fee_fen"))/100m,r["balance_fen"] is null?null:L(r,"balance_fen")/100m,S(r,"nature"),S(r,"category"),S(r,"match_status"),S(r,"entry_source")=="manual"?"手工录入":"Excel导入",I(r,"source_row"),S(r,"raw_note"),S(r,"id")}),new([11,12,13,14,15]));
        Sheet("总资金月度",["月份","笔数","收入","支出","手续费","净现金变动"],store.FinanceMonthlySummary().Select(r=>new object?[]{S(r,"month"),L(r,"n"),L(r,"income_fen")/100m,L(r,"expense_fen")/100m,L(r,"fee_fen")/100m,L(r,"net_fen")/100m}),new([3,4,5,6]));
        var accounts=store.FinanceAccountSummary();Sheet("户头汇总",["户头/账户","笔数","累计收入","累计支出","手续费","净现金变动","当前余额","最近确认余额","余额确认日期","确认后净变动","余额口径","最后交易日期"],accounts.Select(a=>new object?[]{a.Account,a.Count,a.IncomeFen/100m,a.ExpenseFen/100m,a.FeeFen/100m,a.NetFen/100m,a.CurrentBalanceFen/100m,a.LatestBalanceFen/100m,a.BalanceDate,a.PostBalanceNetFen/100m,a.Estimated?"推算":a.CurrentBalanceFen.HasValue?"确认":"无余额数据",a.LatestDate}),new([3,4,5,6,7,8,10]));
        Sheet("课程收入汇总",["标准课程","收入笔数","总收入","手续费","实收净额"],store.FinanceCourseSummary().Select(r=>new object?[]{S(r,"course_group"),L(r,"n"),L(r,"income_fen")/100m,L(r,"fee_fen")/100m,L(r,"net_income_fen")/100m}),new([3,4,5]));
        Sheet("老师收入",["老师","收入笔数","总收入","手续费","实收净额"],store.FinanceTeacherSummary().Select(r=>new object?[]{S(r,"teacher"),L(r,"n"),L(r,"income_fen")/100m,L(r,"fee_fen")/100m,(L(r,"income_fen")-L(r,"fee_fen"))/100m}),new([3,4,5]));
        var histories=store.FinanceClassHistoryRows();Sheet("班级缴费历史",["学员","课程","班级","阶段开始","阶段结束","星期","上课时间","老师","教室","缴费日期","缴费金额","户头","缴费摘要","流水ID"],histories.Select(r=>new object?[]{S(r,"student"),S(r,"course"),S(r,"class_name"),S(r,"started_on"),S(r,"ended_on").Length==0?"至今":S(r,"ended_on"),"星期"+"一二三四五六日"[I(r,"weekday")],S(r,"start_time")+"–"+S(r,"end_time"),S(r,"teacher"),S(r,"room"),S(r,"payment_date"),(L(r,"income_fen")!=0?L(r,"income_fen"):L(r,"expense_fen"))/100m,S(r,"account"),S(r,"payment_summary"),S(r,"transaction_id")}),new([11]));

        var accountMonths = new List<object?[]>();
        foreach (var a in accounts)
            foreach (var r in store.FinanceMonthlySummary(account:a.Account))
                accountMonths.Add([a.Account,S(r,"month"),L(r,"n"),L(r,"income_fen")/100m,L(r,"expense_fen")/100m,L(r,"fee_fen")/100m,L(r,"net_fen")/100m]);
        Sheet("户头月度",["户头/账户","月份","笔数","收入","支出","手续费","净现金变动"],accountMonths,new([4,5,6,7]));

        var courseMonths = new List<object?[]>();
        foreach (var c in store.FinanceCourseSummary())
            foreach (var r in store.FinanceMonthlySummary(courseGroup:S(c,"course_group")))
                courseMonths.Add([S(c,"course_group"),S(r,"month"),L(r,"n"),L(r,"income_fen")/100m,L(r,"fee_fen")/100m,(L(r,"income_fen")-L(r,"fee_fen"))/100m]);
        Sheet("课程月度",["标准课程","月份","收入笔数","总收入","手续费","实收净额"],courseMonths,new([4,5,6]));

        Sheet("经营分类",["经营性质","分类","笔数","收入","支出","手续费","净现金变动"],store.FinanceCategorySummary().Select(r=>new object?[]{S(r,"nature"),S(r,"category"),L(r,"n"),L(r,"income_fen")/100m,L(r,"expense_fen")/100m,L(r,"fee_fen")/100m,L(r,"net_fen")/100m}),new([4,5,6,7]));
        Sheet("导入记录",["导入时间","文件名","工作表数","识别行数","新增行数","重复跳过","自动匹配","备注"],store.FinanceBatches().Select(r=>new object?[]{S(r,"imported_at"),S(r,"file_name"),I(r,"sheet_count"),I(r,"row_count"),I(r,"imported_count"),I(r,"duplicate_count"),I(r,"matched_count"),S(r,"note")}));

        var info = wb.Worksheets.Add("字段说明");
        info.Cell(1,1).Value="字段"; info.Cell(1,2).Value="说明";
        info.Cell(2,1).Value="借方 / 收入"; info.Cell(2,2).Value="现金流入；沿用原财务表口径。";
        info.Cell(3,1).Value="贷方 / 支出"; info.Cell(3,2).Value="现金流出；手续费单独记录。";
        info.Cell(4,1).Value="可确认余额"; info.Cell(4,2).Value="原始流水中明确给出的余额快照；合并户头后当前余额按最近确认余额 + 之后净变动推算。";
        info.Cell(5,1).Value="经营性质"; info.Cell(5,2).Value="与现金方向分离，内部划转不计入经营利润。";
        info.Cell(6,1).Value="原始证据"; info.Cell(6,2).Value="来源表、来源行、原始 JSON、指纹在编辑业务字段时不改写。";
        info.Cell(7,1).Value="班级缴费历史"; info.Cell(7,2).Value="缴费可关联课程/班级；换班、升年级形成新阶段，不覆盖历史。";
        info.Columns().AdjustToContents(12,80);

        wb.SaveAs(path);store.SetSetting("finance_sync_path",path);store.SetSetting("finance_sync_at",DateTime.Now.ToString("s"));
    }
}
