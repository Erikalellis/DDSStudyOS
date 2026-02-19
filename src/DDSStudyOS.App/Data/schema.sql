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
