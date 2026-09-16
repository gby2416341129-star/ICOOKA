namespace KukaManager.Data;

public static class LegacySchema
{
    public const string Sql = """
CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS courses(id TEXT PRIMARY KEY,name TEXT NOT NULL,category TEXT NOT NULL,active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)));
CREATE TABLE IF NOT EXISTS teachers(id TEXT PRIMARY KEY,name TEXT NOT NULL,phone TEXT NOT NULL DEFAULT '',active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)));
CREATE TABLE IF NOT EXISTS students(id TEXT PRIMARY KEY,name TEXT NOT NULL,gender TEXT NOT NULL CHECK(gender IN ('男','女')),birth TEXT NOT NULL,parent TEXT NOT NULL,relation TEXT NOT NULL,phone TEXT NOT NULL,wechat TEXT NOT NULL,address TEXT NOT NULL,note TEXT NOT NULL,active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)));
CREATE TABLE IF NOT EXISTS enrollments(id TEXT PRIMARY KEY,student_id TEXT NOT NULL REFERENCES students(id),course_id TEXT NOT NULL REFERENCES courses(id),enrolled_on TEXT NOT NULL,project TEXT NOT NULL,list_fen INTEGER NOT NULL CHECK(list_fen>=0),discount_fen INTEGER NOT NULL CHECK(discount_fen>=0 AND discount_fen<=list_fen),reason TEXT NOT NULL,approver TEXT NOT NULL,inviter TEXT NOT NULL,seller TEXT NOT NULL,lessons INTEGER NOT NULL CHECK(lessons>0),note TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS classes(id TEXT PRIMARY KEY,name TEXT NOT NULL,course_id TEXT NOT NULL REFERENCES courses(id),teacher_id TEXT NOT NULL REFERENCES teachers(id),room TEXT NOT NULL,weekday INTEGER NOT NULL CHECK(weekday BETWEEN 0 AND 6),start_time TEXT NOT NULL,end_time TEXT NOT NULL,start_date TEXT NOT NULL,end_date TEXT NOT NULL,active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)));
CREATE TABLE IF NOT EXISTS members(id TEXT PRIMARY KEY,class_id TEXT NOT NULL REFERENCES classes(id),enrollment_id TEXT NOT NULL REFERENCES enrollments(id),joined_on TEXT NOT NULL,left_on TEXT NOT NULL DEFAULT '',UNIQUE(class_id,enrollment_id,joined_on));
CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY,class_id TEXT NOT NULL REFERENCES classes(id),day TEXT NOT NULL,start_time TEXT NOT NULL,end_time TEXT NOT NULL,teacher_id TEXT NOT NULL REFERENCES teachers(id),cancelled INTEGER NOT NULL DEFAULT 0 CHECK(cancelled IN (0,1)),reason TEXT NOT NULL DEFAULT '',UNIQUE(class_id,day));
CREATE TABLE IF NOT EXISTS attendance(id TEXT PRIMARY KEY,session_id TEXT NOT NULL REFERENCES sessions(id),enrollment_id TEXT NOT NULL REFERENCES enrollments(id),status TEXT NOT NULL CHECK(status IN ('已扣课','缺课','停课','余额不足')),reason TEXT NOT NULL DEFAULT '',UNIQUE(session_id,enrollment_id));
CREATE TABLE IF NOT EXISTS makeups(id TEXT PRIMARY KEY,attendance_id TEXT NOT NULL UNIQUE REFERENCES attendance(id),day TEXT NOT NULL,start_time TEXT NOT NULL,end_time TEXT NOT NULL,teacher_id TEXT NOT NULL REFERENCES teachers(id),status TEXT NOT NULL CHECK(status IN ('待补课','已补课','取消')),note TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS ledger(id TEXT PRIMARY KEY,enrollment_id TEXT NOT NULL REFERENCES enrollments(id),attendance_id TEXT NOT NULL REFERENCES attendance(id),at TEXT NOT NULL,delta INTEGER NOT NULL CHECK(delta IN(-1,1)),amount_fen INTEGER NOT NULL,remaining INTEGER NOT NULL,remaining_fen INTEGER NOT NULL,reason TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS growth(id TEXT PRIMARY KEY,student_id TEXT NOT NULL REFERENCES students(id),course_id TEXT NOT NULL REFERENCES courses(id),day TEXT NOT NULL,start_time TEXT NOT NULL,end_time TEXT NOT NULL,teacher_id TEXT NOT NULL REFERENCES teachers(id),project TEXT NOT NULL,outcome TEXT NOT NULL,evaluation TEXT NOT NULL,next_goal TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS audit(id TEXT PRIMARY KEY,at TEXT NOT NULL,actor TEXT NOT NULL,action TEXT NOT NULL,detail TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS ledger_enrollment ON ledger(enrollment_id);
CREATE INDEX IF NOT EXISTS ledger_attendance ON ledger(attendance_id);
CREATE INDEX IF NOT EXISTS attendance_session ON attendance(session_id,enrollment_id);
CREATE INDEX IF NOT EXISTS members_class ON members(class_id,enrollment_id);
CREATE INDEX IF NOT EXISTS sessions_day ON sessions(day,start_time);
CREATE INDEX IF NOT EXISTS makeups_day ON makeups(day,start_time);
CREATE TABLE IF NOT EXISTS finance_import_batches(id TEXT PRIMARY KEY,file_name TEXT NOT NULL,file_sha256 TEXT NOT NULL UNIQUE,imported_at TEXT NOT NULL,sheet_count INTEGER NOT NULL,row_count INTEGER NOT NULL,imported_count INTEGER NOT NULL,duplicate_count INTEGER NOT NULL,matched_count INTEGER NOT NULL,note TEXT NOT NULL DEFAULT '');
CREATE TABLE IF NOT EXISTS finance_transactions(id TEXT PRIMARY KEY,batch_id TEXT NOT NULL REFERENCES finance_import_batches(id),source_sheet TEXT NOT NULL,source_row INTEGER NOT NULL,tx_date TEXT NOT NULL,summary TEXT NOT NULL,course_hint TEXT NOT NULL DEFAULT '',income_fen INTEGER NOT NULL DEFAULT 0 CHECK(income_fen>=0),expense_fen INTEGER NOT NULL DEFAULT 0 CHECK(expense_fen>=0),fee_fen INTEGER NOT NULL DEFAULT 0 CHECK(fee_fen>=0),balance_fen INTEGER,direction TEXT NOT NULL,nature TEXT NOT NULL,category TEXT NOT NULL,match_status TEXT NOT NULL,raw_note TEXT NOT NULL DEFAULT '',fingerprint TEXT NOT NULL UNIQUE,raw_json TEXT NOT NULL DEFAULT '',course_group TEXT NOT NULL DEFAULT '',teacher_id TEXT REFERENCES teachers(id),entry_source TEXT NOT NULL DEFAULT 'import',course_id TEXT REFERENCES courses(id),class_id TEXT REFERENCES classes(id));
CREATE TABLE IF NOT EXISTS finance_allocations(id TEXT PRIMARY KEY,transaction_id TEXT NOT NULL REFERENCES finance_transactions(id) ON DELETE CASCADE,student_id TEXT NOT NULL REFERENCES students(id),enrollment_id TEXT REFERENCES enrollments(id),amount_fen INTEGER NOT NULL CHECK(amount_fen>=0),confidence REAL NOT NULL DEFAULT 0,source TEXT NOT NULL DEFAULT 'manual',note TEXT NOT NULL DEFAULT '',UNIQUE(transaction_id,student_id));
CREATE INDEX IF NOT EXISTS finance_tx_date ON finance_transactions(tx_date);
CREATE INDEX IF NOT EXISTS finance_tx_sheet ON finance_transactions(source_sheet,tx_date);
CREATE INDEX IF NOT EXISTS finance_alloc_student ON finance_allocations(student_id,transaction_id);
CREATE TABLE IF NOT EXISTS student_class_history(id TEXT PRIMARY KEY,transaction_id TEXT UNIQUE REFERENCES finance_transactions(id) ON DELETE CASCADE,student_id TEXT NOT NULL REFERENCES students(id),course_id TEXT NOT NULL REFERENCES courses(id),class_id TEXT NOT NULL REFERENCES classes(id),enrollment_id TEXT REFERENCES enrollments(id),teacher_id TEXT NOT NULL REFERENCES teachers(id),weekday INTEGER NOT NULL,start_time TEXT NOT NULL,end_time TEXT NOT NULL,room TEXT NOT NULL DEFAULT '',started_on TEXT NOT NULL,ended_on TEXT NOT NULL DEFAULT '',source TEXT NOT NULL DEFAULT 'finance',note TEXT NOT NULL DEFAULT '',created_at TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS student_class_history_student ON student_class_history(student_id,course_id,started_on);
CREATE INDEX IF NOT EXISTS student_class_history_class ON student_class_history(class_id,started_on,ended_on);
""";
}
