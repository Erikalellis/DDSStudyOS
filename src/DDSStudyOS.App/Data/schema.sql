-- DDS StudyOS - Schema v2
-- Adicionado: Course Notes, User Stats (Streak), App Preferences

PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;

-- Tabela de Cursos (já existe, mas garantindo IF NOT EXISTS)
-- Nota: 'notes' em courses é para anotações rápidas.
-- Para anotações ricas do navegador, expandiremos o uso deste campo ou criaremos tabela dedicada.
-- Para simplificar o MVP v2, usaremos o campo 'notes' já existente na tabela courses para armazenar o Markdown.
-- Adicionaremos colunas de rastreamento de progresso se necessário, mas manteremos simples por enquanto.

CREATE TABLE IF NOT EXISTS courses (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    name          TEXT NOT NULL,
    platform      TEXT,
    url           TEXT,
    username      TEXT,
    password_blob BLOB, -- DPAPI protected bytes (opcional)
    is_favorite   INTEGER NOT NULL DEFAULT 0,
    start_date    TEXT,
    due_date      TEXT,
    status        TEXT,
    notes         TEXT, -- Markdown content do Smart Notes
    last_accessed TEXT, -- Novo: Rastrear última interação para Dashboard "Continuar"
    created_at    TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at    TEXT
);

CREATE TABLE IF NOT EXISTS materials (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    course_id  INTEGER,
    file_name  TEXT NOT NULL,
    file_path  TEXT NOT NULL,
    file_type  TEXT,
    storage_mode TEXT NOT NULL DEFAULT 'reference',
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY(course_id) REFERENCES courses(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS reminders (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    course_id  INTEGER,
    title      TEXT NOT NULL,
    due_at     TEXT NOT NULL,
    notes      TEXT,
    is_completed INTEGER DEFAULT 0, -- Novo: Marcar como feito
    last_notified_at TEXT, -- Novo: evita repetição de notificações entre sessões
    recurrence_pattern TEXT NOT NULL DEFAULT 'none',
    snooze_minutes INTEGER NOT NULL DEFAULT 10,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY(course_id) REFERENCES courses(id) ON DELETE SET NULL
);

-- Nova Tabela: Preferências e Estatísticas do Usuário
CREATE TABLE IF NOT EXISTS user_stats (
    key   TEXT PRIMARY KEY,
    value TEXT
);

-- Inicializar estatísticas se não existirem
INSERT OR IGNORE INTO user_stats (key, value) VALUES ('study_streak', '0');
INSERT OR IGNORE INTO user_stats (key, value) VALUES ('last_open_date', '');

CREATE TABLE IF NOT EXISTS course_favorites (
    profile_key TEXT NOT NULL,
    course_id INTEGER NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (profile_key, course_id),
    FOREIGN KEY(course_id) REFERENCES courses(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_course_favorites_profile ON course_favorites(profile_key);
CREATE INDEX IF NOT EXISTS idx_course_favorites_course ON course_favorites(course_id);

CREATE TABLE IF NOT EXISTS course_history (
    profile_key TEXT NOT NULL,
    course_id INTEGER NOT NULL,
    last_accessed TEXT NOT NULL,
    PRIMARY KEY (profile_key, course_id),
    FOREIGN KEY(course_id) REFERENCES courses(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_course_history_profile ON course_history(profile_key, last_accessed DESC);
CREATE INDEX IF NOT EXISTS idx_course_history_course ON course_history(course_id);

CREATE TABLE IF NOT EXISTS study_activity (
    profile_key TEXT NOT NULL,
    activity_date TEXT NOT NULL, -- yyyy-MM-dd (hora local)
    activity_count INTEGER NOT NULL DEFAULT 0,
    total_minutes INTEGER NOT NULL DEFAULT 0,
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (profile_key, activity_date)
);

CREATE INDEX IF NOT EXISTS idx_study_activity_profile_date ON study_activity(profile_key, activity_date DESC);
