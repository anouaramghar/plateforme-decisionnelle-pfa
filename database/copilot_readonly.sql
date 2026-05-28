-- ─────────────────────────────────────────────────────────────────────────────
-- Copilot read-only DW access — agent-service /query_dw tool (P1.B).
--
-- Run by entrypoint.sh on EVERY boot (not only cold init), so it rolls forward
-- onto volumes that were seeded before Copilot existed. Fully idempotent.
--
-- pfa_app_readonly is granted db_datareader ONLY on PFA_DW. Even if every
-- application-layer safety guard is bypassed, this login physically cannot
-- mutate the warehouse, and has no access to the OLTP database (PFA_DB) at all.
--
-- Password comes from -v READONLY_PASSWORD=... (distinct from APP_PASSWORD).
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. Server-level login (idempotent).
IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = 'pfa_app_readonly')
BEGIN
    DECLARE @sql_ro NVARCHAR(MAX) =
        N'CREATE LOGIN pfa_app_readonly WITH PASSWORD = ''' + REPLACE('$(READONLY_PASSWORD)', '''', '''''') + ''', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
    EXEC sp_executesql @sql_ro;
END
GO

-- 2. DW database user + read-only role (guarded: only if PFA_DW exists).
--    Wrapped in EXEC so the USE-context switch is scoped and the whole batch
--    does not fail when PFA_DW is somehow absent.
IF DB_ID('PFA_DW') IS NOT NULL
    EXEC('USE PFA_DW;
        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = ''pfa_app_readonly'')
            CREATE USER pfa_app_readonly FOR LOGIN pfa_app_readonly;
        IF IS_ROLEMEMBER(''db_datareader'', ''pfa_app_readonly'') = 0
            ALTER ROLE db_datareader ADD MEMBER pfa_app_readonly;');
GO
