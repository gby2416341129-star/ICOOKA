using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KukaManager.Models;
using KukaManager.Design;
using KukaManager.Services;
using static KukaManager.Utils.Value;

namespace KukaManager.Views;

public sealed partial class MainWindow
{
    private void DashboardPage()
    {
        var bmap=_store.BalanceMap();var balances=bmap.Values.ToList();var active=I(_store.One("SELECT COUNT(*) n FROM students WHERE active=1")??new(),"n");var makeup=I(_store.One("SELECT COUNT(*) n FROM attendance a WHERE status='缺课' AND NOT EXISTS(SELECT 1 FROM makeups m WHERE m.attendance_id=a.id AND m.status='已补课')")??new(),"n");
        MetricRow([("在读学员",active.ToString(),"学员与家庭档案"),("待消课时",balances.Sum(x=>x.Lessons).ToString(),"所有报名记录合计"),("剩余课时金额",Fen(balances.Sum(x=>x.AmountFen)),"未消课余额"),("待安排 / 待完成补课",makeup.ToString(),"缺课后续跟进")]);
        var welcome=new Grid();welcome.ColumnDefinitions.Add(new ColumnDefinition());welcome.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var text=new StackPanel();text.Children.Add(WindowChrome.Text("让招生、教务与课消记录更清晰。",20,FontWeights.SemiBold));text.Children.Add(WindowChrome.Text("先建学员档案，再完成报名与入班。课程结束后自动记账，缺课、补课和停课都保留痕迹。",13,null,Theme.Brush(Theme.MutedColor)));welcome.Children.Add(text);var add=Btn("新增学员",()=>EditStudent(null),true);Grid.SetColumn(add,1);welcome.Children.Add(add);_body.Children.Add(SectionCard(welcome,new Thickness(24)));
        _body.Children.Add(SectionTitle("今日课程"));var sessions=SessionRows(DateTime.Today,DateTime.Today);Table(["时间","课程 / 班级","授课老师","教室","学员","状态"],sessions.Select(s=>new object?[]{S(s,"start_time")+"–"+S(s,"end_time"),S(s,"course")+" / "+S(s,"class_name"),S(s,"teacher"),S(s,"room"),S(s,"student_names"),B(s,"cancelled")?"停课":string.CompareOrdinal(S(s,"end_time"),DateTime.Now.ToString("HH:mm"))<=0?"已结束":"待上课"}),null,260);
        var latest=_store.Rows("SELECT e.id,e.student_id,e.course_id,s.name,c.name course,e.enrolled_on FROM enrollments e JOIN students s ON s.id=e.student_id JOIN courses c ON c.id=e.course_id WHERE s.active=1 ORDER BY e.enrolled_on DESC,e.rowid DESC");var seen=new HashSet<string>();var low=new List<string>();foreach(var r in latest){var key=S(r,"student_id")+"|"+S(r,"course_id");if(!seen.Add(key))continue;var rem=bmap.TryGetValue(S(r,"id"),out var b)?b.Lessons:0;if(rem<=3)low.Add(S(r,"name")+" · "+S(r,"course")+" "+(rem<=0?"已用完":rem+" 次"));}_body.Children.Add(SectionNote("续费提醒："+(low.Count==0?"暂无最新课包剩余 3 次及以下的在读学员":string.Join("；",low.Take(8)))));
    }

    private void MetricRow(IEnumerable<(string Title,string Value,string Sub)> metrics)
    {
        var grid=new WrapPanel{Margin=new Thickness(-6,0,-6,10),Orientation=Orientation.Horizontal};
        var list=metrics.ToList();
        for(var i=0;i<list.Count;i++)
        {
            var m=list[i];
            var p=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
            var title=WindowChrome.Text(m.Title,12,FontWeights.SemiBold,Theme.Brush(Theme.MutedColor));title.Margin=new Thickness(0,0,0,7);p.Children.Add(title);
            var value=WindowChrome.Text(m.Value,26,FontWeights.SemiBold);value.Margin=new Thickness(0,0,0,4);p.Children.Add(value);
            p.Children.Add(WindowChrome.Text(m.Sub,12,null,Theme.Brush(Theme.MutedColor)));
            var card=WindowChrome.Card(p,new Thickness(18,16,18,16));card.Width=195;card.MinHeight=106;card.Margin=new Thickness(6,0,6,12);grid.Children.Add(card);
        }
        _body.Children.Add(grid);
    }

    private void StudentsPage()
    {
        Toolbar([("班级 / 缴费历史",()=>new StudentHistoryWindow(this,_store,SelectedId()).ShowDialog(),false),("查看财务",()=>{_financeStudentId=SelectedId();_financeView="transactions";Navigate("财务中心");},false),("编辑档案",()=>EditStudent(SelectedId()),false),("新增学员",()=>EditStudent(null),true)],true);
        var rows=_store.Rows("SELECT * FROM students ORDER BY active DESC,name");Table(["姓名","性别","年龄","出生日期","家长 / 关系","联系电话","微信号","家庭住址","状态"],rows.Select(r=>new object?[]{S(r,"name"),S(r,"gender"),KukaManager.Data.KukaStore.Age(S(r,"birth"))+" 岁",S(r,"birth"),S(r,"parent")+" / "+S(r,"relation"),S(r,"phone"),S(r,"wechat"),S(r,"address"),B(r,"active")?"在读":"归档"}),rows.Select(r=>S(r,"id")),520,id=>EditStudent(id));
    }

    private void EditStudent(string? id)
    {
        var initial=id is null?new Dictionary<string,object?>():_store.Get("students",id);var fields=new List<FormField>{new("name","姓名 *",FormFieldKind.Text,Required:true),new("gender","性别 *",FormFieldKind.Choice,Choices:[new("男","男"),new("女","女")],Required:true),new("birth","出生年月日 *",FormFieldKind.Date,"2018-01-01",Required:true),new("parent","家长姓名 *",FormFieldKind.Text,Required:true),new("relation","与学员关系",FormFieldKind.Choice,Choices:[new("妈妈","妈妈"),new("爸爸","爸爸"),new("其他监护人","其他监护人")]),new("phone","联系电话 *",FormFieldKind.Text,Required:true),new("wechat","家长微信号",FormFieldKind.Text),new("address","家庭住址",FormFieldKind.Text),new("note","备注 / 其他家长联系方式",FormFieldKind.LongText),new("active","档案状态",FormFieldKind.Choice,1,[new("在读",1),new("归档",0)])};var d=EditForm("学员档案",fields,initial);if(d is null)return;_store.SaveStudent(d,id);Navigate(_page);
    }

    private void EnrollmentsPage()
    {
        Toolbar([("编辑报名",()=>EditEnrollment(SelectedId()),false),("报名 / 续费",()=>EditEnrollment(null),true)],true);var rows=_store.Rows("SELECT e.*,s.name student,c.name course FROM enrollments e JOIN students s ON s.id=e.student_id JOIN courses c ON c.id=e.course_id ORDER BY enrolled_on DESC,e.rowid DESC");var b=_store.BalanceMap();Table(["学员","课程 / 项目","报名日期","原价","优惠","折后课包金额","报名课次","剩余课次","剩余金额","邀约 / 报名老师","审批人"],rows.Select(r=>{var bb=b.GetValueOrDefault(S(r,"id"),new(0,0));return new object?[]{S(r,"student"),S(r,"course")+" / "+S(r,"project"),S(r,"enrolled_on"),Fen(L(r,"list_fen")),Fen(L(r,"discount_fen")),Fen(L(r,"list_fen")-L(r,"discount_fen")),I(r,"lessons"),bb.Lessons,Fen(bb.AmountFen),S(r,"inviter")+" / "+S(r,"seller"),S(r,"approver")};}),rows.Select(r=>S(r,"id")),520,id=>EditEnrollment(id));
    }

    private void EditEnrollment(string? id)
    {
        var initial=id is null?new Dictionary<string,object?>():_store.Get("enrollments",id);if(id is not null){initial=new(initial,StringComparer.OrdinalIgnoreCase){{"list_price",L(initial,"list_fen")/100m},{"discount",L(initial,"discount_fen")/100m}};}
        var fields=new List<FormField>{new("student_id","学员 *",FormFieldKind.Reference,Choices:Refs("students",S(initial,"student_id")),Required:true),new("course_id","课程 *",FormFieldKind.Reference,Choices:Refs("courses",S(initial,"course_id")),Required:true),new("enrolled_on","报名日期 *",FormFieldKind.Date,DateTime.Today.ToString("yyyy-MM-dd"),Required:true),new("project","报名项目 *",FormFieldKind.Text,"常规课程",Required:true),new("list_price","原价 *",FormFieldKind.Money,2900m,Required:true),new("discount","优惠金额",FormFieldKind.Money,0m),new("reason","优惠原因",FormFieldKind.LongText),new("approver","优惠审批人",FormFieldKind.Text),new("inviter","邀约老师",FormFieldKind.Text),new("seller","报名老师",FormFieldKind.Text),new("lessons","本次购入 / 期初剩余课次 *",FormFieldKind.Integer,16,Required:true),new("note","报名备注",FormFieldKind.LongText)};var d=EditForm("报名与续费",fields,initial,"录入历史学员时，课次填当前剩余次数，金额填对应余额。续费请新建报名；入班时选择该报名。");if(d is null)return;d["list_fen"]=Cents(d["list_price"]);d["discount_fen"]=Cents(d["discount"]);d.Remove("list_price");d.Remove("discount");_store.SaveEnrollment(d,id);Navigate(_page);
    }

    private void ClassesPage()
    {
        Toolbar([("查看成员 / 退班",ClassMembers,false),("学员入班",JoinClass,false),("新建班级",AddClass,true)],true);var rows=_store.Rows("SELECT c.*,co.name course,t.name teacher FROM classes c JOIN courses co ON co.id=c.course_id JOIN teachers t ON t.id=c.teacher_id ORDER BY active DESC,end_date DESC,weekday,start_time");var today=DateTime.Today.ToString("yyyy-MM-dd");Table(["班级名称","课程","老师","教室","星期","上课时间","排课起止","当前在班","状态"],rows.Select(r=>new object?[]{S(r,"name"),S(r,"course"),S(r,"teacher"),S(r,"room"),"星期"+"一二三四五六日"[I(r,"weekday")],S(r,"start_time")+"–"+S(r,"end_time"),S(r,"start_date")+" ～ "+S(r,"end_date"),_store.ClassRosterCount(S(r,"id"),today),B(r,"active")&&string.CompareOrdinal(S(r,"end_date"),today)>=0?"进行中":"已结束"}),rows.Select(r=>S(r,"id")),520);
    }

    private void AddClass()
    {
        var fields=new List<FormField>{new("name","班级名称 *",FormFieldKind.Text,Required:true),new("course_id","课程 *",FormFieldKind.Reference,Choices:Refs("courses"),Required:true),new("teacher_id","授课老师 *",FormFieldKind.Reference,Choices:Refs("teachers"),Required:true),new("room","教室",FormFieldKind.Text),new("weekday","星期 *",FormFieldKind.Choice,0,Enumerable.Range(0,7).Select(i=>new FormChoice("星期"+"一二三四五六日"[i],i)).ToList(),true),new("start_time","开始时间",FormFieldKind.Text,"09:00",Required:true),new("end_time","结束时间",FormFieldKind.Text,"10:30",Required:true),new("start_date","排课开始",FormFieldKind.Date,DateTime.Today.ToString("yyyy-MM-dd"),Required:true),new("end_date","排课结束",FormFieldKind.Date,DateTime.Today.AddDays(112).ToString("yyyy-MM-dd"),Required:true)};var d=EditForm("新建每周固定班级",fields,null,"保存后生成每周课表。停课请在周排课表选中对应日期办理，保留历史记录。");if(d is null)return;d["active"]=1;_store.CreateClass(d);Navigate(_page);
    }

    private void JoinClass()
    {
        var cid=SelectedId();var c=_store.Get("classes",cid);var es=_store.Rows("SELECT e.*,s.name student FROM enrollments e JOIN students s ON s.id=e.student_id WHERE course_id=? AND s.active=1 ORDER BY e.enrolled_on DESC",S(c,"course_id"));var b=_store.BalanceMap();var opts=es.Where(r=>b.GetValueOrDefault(S(r,"id"),new(0,0)).Lessons>0).Select(r=>new FormChoice(S(r,"student")+" · "+S(r,"enrolled_on")+" · 剩余 "+b.GetValueOrDefault(S(r,"id"),new(0,0)).Lessons+" 次",S(r,"id"))).ToList();if(opts.Count==0)throw new InvalidOperationException("没有可加入此班的有效报名。请先办理对应课程报名或续费。");var d=EditForm("加入 "+S(c,"name"),[new("enrollment_id","选择报名 *",FormFieldKind.Reference,Choices:opts,Required:true),new("joined_on","入班日期 *",FormFieldKind.Date,DateTime.Today.ToString("yyyy-MM-dd"),Required:true)],null,"入班日期之前不扣课。若选择过去日期，下次自动检查会补算已结束课程。");if(d is null)return;_store.AddMember(cid,Convert.ToString(d["enrollment_id"])!,Convert.ToString(d["joined_on"])!);Navigate(_page);
    }

    private void ClassMembers()
    {
        var cid=SelectedId();var today=DateTime.Today.ToString("yyyy-MM-dd");var manual=_store.Rows("SELECT m.*,s.name FROM members m JOIN enrollments e ON e.id=m.enrollment_id JOIN students s ON s.id=e.student_id WHERE m.class_id=? AND (m.left_on='' OR m.left_on>?) ORDER BY s.name",cid,today);var finance=_store.FinanceClassHistoryRows(classId:cid,activeOn:today);var opts=new List<FormChoice>();foreach(var r in manual)opts.Add(new(S(r,"name")+" · 教务入班 "+S(r,"joined_on")+(S(r,"left_on").Length>0?" · 计划 "+S(r,"left_on")+" 退班":""),"member:"+S(r,"id")));foreach(var r in finance)opts.Add(new(S(r,"student")+" · 缴费关联 "+S(r,"started_on")+" · "+S(r,"course"),"finance:"+S(r,"id")));if(opts.Count==0)throw new InvalidOperationException("这个班级当前没有在班学员。");var d=EditForm("成员与退班",[new("record","学员",FormFieldKind.Reference,Choices:opts,Required:true),new("operation","操作",FormFieldKind.Choice,"leave",[new("计划 / 调整退班日期","leave"),new("撤销尚未生效的计划退班","cancel_leave"),new("取消尚未生效的计划入班","cancel_join")],true),new("day","退班生效日",FormFieldKind.Date,DateTime.Today.ToString("yyyy-MM-dd")),new("reason","原因 *",FormFieldKind.LongText,Required:true)],null,"缴费关联记录以财务流水为源；要修改缴费关联班级，请到财务中心编辑对应流水。");if(d is null)return;var rec=Convert.ToString(d["record"])!;if(rec.StartsWith("finance:"))throw new InvalidOperationException("该学员由财务缴费记录关联班级，请到财务中心修改对应缴费流水。");var id=rec[7..];var op=Convert.ToString(d["operation"]);if(op=="cancel_leave")_store.CancelScheduledLeave(id,Convert.ToString(d["reason"])!);else if(op=="cancel_join")_store.CancelFutureMember(id,Convert.ToString(d["reason"])!);else _store.LeaveMember(id,Convert.ToString(d["day"])!,Convert.ToString(d["reason"])!);Navigate(_page);
    }

    private List<Dictionary<string,object?>> SessionRows(DateTime start,DateTime end)
    {
        var rs=_store.Rows("SELECT s.*,c.name class_name,c.room,co.name course,t.name teacher FROM sessions s JOIN classes c ON c.id=s.class_id JOIN courses co ON co.id=c.course_id JOIN teachers t ON t.id=s.teacher_id WHERE s.day BETWEEN ? AND ? ORDER BY s.day,s.start_time",start.ToString("yyyy-MM-dd"),end.ToString("yyyy-MM-dd"));foreach(var r in rs)r["student_names"]=string.Join('、',_store.ClassRosterNames(S(r,"class_id"),S(r,"day")));return rs;
    }

    private void CalendarPage()
    {
        Toolbar([("上一周",()=>{_week=_week.AddDays(-7);Navigate(_page);},false),("下一周",()=>{_week=_week.AddDays(7);Navigate(_page);},false),("导出本周与全量数据",ExportFull,true)],false);_body.Children.Add(SectionNote($"{_week:yyyy年MM月dd日} — {_week.AddDays(6):MM月dd日} · 双击课程可办理停课"));var s=SessionRows(_week,_week.AddDays(6));var rows=new List<object?[]>();var ids=new List<string>();foreach(var r in s){rows.Add([S(r,"day"),"星期"+"一二三四五六日"[((int)DateTime.Parse(S(r,"day")).DayOfWeek+6)%7],S(r,"start_time")+"–"+S(r,"end_time"),S(r,"course"),S(r,"class_name"),S(r,"teacher"),S(r,"room"),S(r,"student_names"),B(r,"cancelled")?"已停课":"正常",S(r,"reason")]);ids.Add(S(r,"id"));}foreach(var m in _store.Rows("SELECT m.*,st.name student,t.name teacher FROM makeups m JOIN attendance a ON a.id=m.attendance_id JOIN enrollments e ON e.id=a.enrollment_id JOIN students st ON st.id=e.student_id JOIN teachers t ON t.id=m.teacher_id WHERE day BETWEEN ? AND ? AND m.status<>'取消' ORDER BY day,start_time",_week.ToString("yyyy-MM-dd"),_week.AddDays(6).ToString("yyyy-MM-dd"))){rows.Add([S(m,"day"),"补课",S(m,"start_time")+"–"+S(m,"end_time"),"补课",S(m,"student"),S(m,"teacher"),"",S(m,"student"),S(m,"status"),S(m,"note")]);ids.Add("");}Table(["日期","星期","时间","课程","班级 / 学员","老师","教室","学员","状态","原因"],rows,ids,570,id=>{if(string.IsNullOrWhiteSpace(id))return;CancelSession(id);});
    }

    private void CancelSession(string id)
    {
        var d=new TextPromptWindow(this,"本次停课","停课原因（已扣课将退回）：","",true);if(d.ShowDialog()!=true)return;if(string.IsNullOrWhiteSpace(d.Value))throw new InvalidOperationException("请填写停课原因");_store.CancelSession(id,d.Value);Navigate(_page);
    }

    private void AttendancePage()
    {
        Toolbar([("更正出勤",CorrectAttendance,false),("安排补课",ScheduleMakeup,false),("检查到期课程",()=>{var n=_store.ProcessDue();MessageBox.Show(this,$"本次扣课 {n} 次。已经处理过的课程不会重复扣减。","检查完成");Navigate(_page);},true)],true);var rs=_store.Rows("SELECT a.*,s.name student,co.name course,se.day,se.start_time,cl.name class_name FROM attendance a JOIN enrollments e ON e.id=a.enrollment_id JOIN students s ON s.id=e.student_id JOIN courses co ON co.id=e.course_id JOIN sessions se ON se.id=a.session_id JOIN classes cl ON cl.id=se.class_id ORDER BY se.day DESC,se.start_time DESC");var bm=_store.BalanceMap();Table(["学员","课程","班级","原排课日期","出勤","更正原因","补课安排","当前剩余"],rs.Select(r=>{var m=_store.One("SELECT m.*,t.name teacher FROM makeups m JOIN teachers t ON t.id=m.teacher_id WHERE attendance_id=?",S(r,"id"));return new object?[]{S(r,"student"),S(r,"course"),S(r,"class_name"),S(r,"day")+" "+S(r,"start_time"),S(r,"status"),S(r,"reason"),m is null?"":S(m,"day")+" "+S(m,"start_time")+" "+S(m,"teacher")+" "+S(m,"status"),bm.GetValueOrDefault(S(r,"enrollment_id"),new(0,0)).Lessons};}),rs.Select(r=>S(r,"id")),520);
    }
    private void CorrectAttendance(){var id=SelectedId();var d=EditForm("更正本次出勤",[new("present","出勤结果",FormFieldKind.Choice,false,[new("缺课 / 撤回本次扣课",false),new("已上课 / 扣课",true)],true),new("reason","更正原因 *",FormFieldKind.LongText,Required:true)],null,"更正会保留原流水并新增调整流水。已有补课安排会取消，需要时请重新安排。");if(d is null)return;_store.CorrectAttendance(id,Convert.ToBoolean(d["present"]),Convert.ToString(d["reason"])!);Navigate(_page);}
    private void ScheduleMakeup(){var id=SelectedId();var d=EditForm("安排补课",[new("day","补课日期",FormFieldKind.Date,DateTime.Today.ToString("yyyy-MM-dd"),Required:true),new("start_time","开始时间",FormFieldKind.Text,"16:00",Required:true),new("end_time","结束时间",FormFieldKind.Text,"17:30",Required:true),new("teacher_id","补课老师",FormFieldKind.Reference,Choices:Refs("teachers"),Required:true),new("note","原因与备注 *",FormFieldKind.LongText,Required:true)],null,"先将原课程更正为缺课。补课结束后自动扣一次。");if(d is null)return;_store.ScheduleMakeup(id,Convert.ToString(d["day"])!,Convert.ToString(d["start_time"])!,Convert.ToString(d["end_time"])!,Convert.ToString(d["teacher_id"])!,Convert.ToString(d["note"])!);Navigate(_page);}

    private void LedgerPage()
    {
        Toolbar([],true);var rs=_store.Rows("SELECT l.*,s.name student,c.name course,cl.name class_name,se.day FROM ledger l JOIN enrollments e ON e.id=l.enrollment_id JOIN students s ON s.id=e.student_id JOIN courses c ON c.id=e.course_id JOIN attendance a ON a.id=l.attendance_id JOIN sessions se ON se.id=a.session_id JOIN classes cl ON cl.id=se.class_id ORDER BY l.at DESC,l.rowid DESC");Table(["学员","课程","班级","原排课日期","记账时间","本次变化","本次金额","剩余课次","剩余金额","原因"],rs.Select(r=>new object?[]{S(r,"student"),S(r,"course"),S(r,"class_name"),S(r,"day"),S(r,"at"),I(r,"delta"),Fen(L(r,"amount_fen")),I(r,"remaining"),Fen(L(r,"remaining_fen")),S(r,"reason")}),null,560);
    }

    private void GrowthPage()
    {
        Toolbar([("编辑记录",()=>EditGrowth(SelectedId()),false),("新增成长记录",()=>EditGrowth(null),true)],true);var rs=_store.Rows("SELECT g.*,s.name student,c.name course,t.name teacher FROM growth g JOIN students s ON s.id=g.student_id JOIN courses c ON c.id=g.course_id JOIN teachers t ON t.id=g.teacher_id ORDER BY day DESC,start_time DESC");Table(["学员","日期","时间","课程 / 项目","授课老师","学习成果","学习评价","下次目标"],rs.Select(r=>new object?[]{S(r,"student"),S(r,"day"),S(r,"start_time")+"–"+S(r,"end_time"),S(r,"course")+" / "+S(r,"project"),S(r,"teacher"),S(r,"outcome"),S(r,"evaluation"),S(r,"next_goal")}),rs.Select(r=>S(r,"id")),520,id=>EditGrowth(id));
    }
    private void EditGrowth(string? id){var initial=id is null?new Dictionary<string,object?>():_store.Get("growth",id);var d=EditForm("学习成长记录",[new("student_id","学员 *",FormFieldKind.Reference,Choices:Refs("students",S(initial,"student_id")),Required:true),new("course_id","课程 *",FormFieldKind.Reference,Choices:Refs("courses",S(initial,"course_id")),Required:true),new("day","日期 *",FormFieldKind.Date,DateTime.Today.ToString("yyyy-MM-dd"),Required:true),new("start_time","开始时间",FormFieldKind.Text,"16:00",Required:true),new("end_time","结束时间",FormFieldKind.Text,"17:30",Required:true),new("teacher_id","授课老师 *",FormFieldKind.Reference,Choices:Refs("teachers",S(initial,"teacher_id")),Required:true),new("project","学习项目 *",FormFieldKind.Text,Required:true),new("outcome","学习成果 *",FormFieldKind.LongText,Required:true),new("evaluation","学习评价 *",FormFieldKind.LongText,Required:true),new("next_goal","下次目标",FormFieldKind.LongText)],initial);if(d is null)return;_store.SaveGrowth(d,id);Navigate(_page);}

    private void CatalogPage()
    {
        Toolbar([("新增课程",()=>EditCourse(null),false),("新增老师",()=>EditTeacher(null),true)],false);
        _body.Children.Add(SectionTitle("课程目录"));
        var cs=_store.Rows("SELECT * FROM courses ORDER BY active DESC,category,name");
        Table(["课程名称","科目 / 分类","状态"],cs.Select(r=>new object?[]{S(r,"name"),S(r,"category"),B(r,"active")?"启用":"停用"}),cs.Select(r=>S(r,"id")),320,id=>EditCourse(id));
        _body.Children.Add(SectionTitle("老师"));
        var ts=_store.Rows("SELECT * FROM teachers ORDER BY active DESC,name");
        Table(["老师姓名","联系电话","状态"],ts.Select(r=>new object?[]{S(r,"name"),S(r,"phone"),B(r,"active")?"启用":"停用"}),ts.Select(r=>S(r,"id")),320,id=>EditTeacher(id));
    }
    private void EditCourse(string? id){var initial=id is null?new Dictionary<string,object?>{{"active",1}}:_store.Get("courses",id);var d=EditForm("课程",[new("name","课程名称 *",FormFieldKind.Text,Required:true),new("category","科目 / 分类 *",FormFieldKind.Text,Required:true),new("active","状态",FormFieldKind.Choice,1,[new("启用",1),new("停用",0)])],initial);if(d is null)return;_store.SaveCourse(d,id);Navigate(_page);}
    private void EditTeacher(string? id){var initial=id is null?new Dictionary<string,object?>{{"active",1}}:_store.Get("teachers",id);var d=EditForm("老师",[new("name","老师姓名 *",FormFieldKind.Text,Required:true),new("phone","联系电话",FormFieldKind.Text),new("active","状态",FormFieldKind.Choice,1,[new("启用",1),new("停用",0)])],initial);if(d is null)return;_store.SaveTeacher(d,id);Navigate(_page);}

    private void AuditPage()
    {
        Toolbar([],true);var rs=_store.Rows("SELECT * FROM audit ORDER BY at DESC,rowid DESC LIMIT 5000");Table(["时间","操作人","操作","详情"],rs.Select(r=>new object?[]{S(r,"at").Replace('T',' '),S(r,"actor"),S(r,"action"),S(r,"detail")}),null,580);
    }
}
