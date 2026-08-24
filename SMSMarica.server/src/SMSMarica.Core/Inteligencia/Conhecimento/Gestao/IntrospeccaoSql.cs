using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Inteligencia.Conhecimento.Gestao;

/// <summary>
/// Monta as consultas read-only de introspecção do schema por dialeto. Cada uma é um único SELECT
/// (passa pelo guard read-only) e devolve colunas com nomes fixos, para o montador de markdown ler
/// pelo nome. Ver ADR-0023.
///
/// Colunas de <see cref="Colunas"/>: <c>esquema, tabela, coluna, tipo, tamanho, anulavel, ordem, objeto</c>
/// (<c>objeto</c> = TABLE | VIEW). Inclui tabelas-base E views — num HIS muito dado vem por view.
/// Colunas de <see cref="ChavesEstrangeiras"/>: <c>tabela_origem, coluna_origem, tabela_destino, coluna_destino</c>
/// (cada uma já com o esquema prefixado no nome da tabela).
/// </summary>
public static class IntrospeccaoSql
{
    public static string Colunas(DialetoSql dialeto) => dialeto switch
    {
        DialetoSql.SqlServer => """
            SELECT c.TABLE_SCHEMA AS esquema, c.TABLE_NAME AS tabela, c.COLUMN_NAME AS coluna,
                   c.DATA_TYPE AS tipo, c.CHARACTER_MAXIMUM_LENGTH AS tamanho,
                   c.IS_NULLABLE AS anulavel, c.ORDINAL_POSITION AS ordem,
                   CASE WHEN t.TABLE_TYPE = 'VIEW' THEN 'VIEW' ELSE 'TABLE' END AS objeto
              FROM INFORMATION_SCHEMA.COLUMNS c
              JOIN INFORMATION_SCHEMA.TABLES t
                ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
               AND t.TABLE_TYPE IN ('BASE TABLE', 'VIEW')
             ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION
            """,

        DialetoSql.Postgres => """
            SELECT c.table_schema AS esquema, c.table_name AS tabela, c.column_name AS coluna,
                   c.data_type AS tipo, c.character_maximum_length AS tamanho,
                   c.is_nullable AS anulavel, c.ordinal_position AS ordem,
                   CASE WHEN t.table_type = 'VIEW' THEN 'VIEW' ELSE 'TABLE' END AS objeto
              FROM information_schema.columns c
              JOIN information_schema.tables t
                ON t.table_schema = c.table_schema AND t.table_name = c.table_name
               AND t.table_type IN ('BASE TABLE', 'VIEW')
             WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema')
             ORDER BY c.table_schema, c.table_name, c.ordinal_position
            """,

        // Oracle: filtra o owner via ALL_TAB_COLUMNS (cobre tabelas E views).
        DialetoSql.Oracle => """
            SELECT tc.owner AS esquema, tc.table_name AS tabela, tc.column_name AS coluna,
                   tc.data_type AS tipo, tc.data_length AS tamanho, tc.nullable AS anulavel,
                   tc.column_id AS ordem,
                   CASE WHEN v.view_name IS NULL THEN 'TABLE' ELSE 'VIEW' END AS objeto
              FROM all_tab_columns tc
              LEFT JOIN all_views v ON v.owner = tc.owner AND v.view_name = tc.table_name
             WHERE tc.owner NOT IN ('SYS','SYSTEM','XDB','CTXSYS','MDSYS','ORDSYS')
             ORDER BY tc.owner, tc.table_name, tc.column_id
            """,

        _ => throw new NotSupportedException($"Introspecção não suportada para o dialeto {dialeto}."),
    };

    public static string ChavesEstrangeiras(DialetoSql dialeto) => dialeto switch
    {
        DialetoSql.SqlServer => """
            SELECT fk_s.name + '.' + fk_t.name AS tabela_origem, fk_c.name AS coluna_origem,
                   pk_s.name + '.' + pk_t.name AS tabela_destino, pk_c.name AS coluna_destino
              FROM sys.foreign_key_columns fkc
              JOIN sys.tables fk_t ON fk_t.object_id = fkc.parent_object_id
              JOIN sys.schemas fk_s ON fk_s.schema_id = fk_t.schema_id
              JOIN sys.columns fk_c ON fk_c.object_id = fkc.parent_object_id
                                   AND fk_c.column_id = fkc.parent_column_id
              JOIN sys.tables pk_t ON pk_t.object_id = fkc.referenced_object_id
              JOIN sys.schemas pk_s ON pk_s.schema_id = pk_t.schema_id
              JOIN sys.columns pk_c ON pk_c.object_id = fkc.referenced_object_id
                                   AND pk_c.column_id = fkc.referenced_column_id
             ORDER BY 1, 2
            """,

        DialetoSql.Postgres => """
            SELECT tc.table_schema || '.' || tc.table_name AS tabela_origem,
                   kcu.column_name AS coluna_origem,
                   ccu.table_schema || '.' || ccu.table_name AS tabela_destino,
                   ccu.column_name AS coluna_destino
              FROM information_schema.table_constraints tc
              JOIN information_schema.key_column_usage kcu
                ON kcu.constraint_name = tc.constraint_name AND kcu.table_schema = tc.table_schema
              JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema
             WHERE tc.constraint_type = 'FOREIGN KEY'
             ORDER BY 1, 2
            """,

        DialetoSql.Oracle => """
            SELECT a.owner || '.' || a.table_name AS tabela_origem, acc.column_name AS coluna_origem,
                   c_pk.owner || '.' || c_pk.table_name AS tabela_destino, pcc.column_name AS coluna_destino
              FROM all_constraints a
              JOIN all_cons_columns acc ON acc.owner = a.owner AND acc.constraint_name = a.constraint_name
              JOIN all_constraints c_pk ON c_pk.owner = a.r_owner AND c_pk.constraint_name = a.r_constraint_name
              JOIN all_cons_columns pcc ON pcc.owner = c_pk.owner AND pcc.constraint_name = c_pk.constraint_name
                                       AND pcc.position = acc.position
             WHERE a.constraint_type = 'R'
               AND a.owner NOT IN ('SYS','SYSTEM','XDB','CTXSYS','MDSYS','ORDSYS')
             ORDER BY 1, 2
            """,

        _ => throw new NotSupportedException($"Introspecção de FKs não suportada para {dialeto}."),
    };
}
