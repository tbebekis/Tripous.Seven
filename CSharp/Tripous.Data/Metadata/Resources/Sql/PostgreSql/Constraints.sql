select
    n.nspname as SchemaName,
    t.relname as TableName,
    c.oid as ConstraintId,
    c.conname as ConstraintName,
    case c.contype
        when 'p' then 'PRIMARY KEY'
        when 'f' then 'FOREIGN KEY'
        when 'u' then 'UNIQUE'
        when 'c' then 'CHECK'
    end as ConstraintTypeText,
    case c.contype
        when 'p' then 1
        when 'f' then 2
        when 'u' then 3
        when 'c' then 4
    end as ConstraintType,
    a.attname as FieldName,
    ft.relname as ForeignTable,
    fa.attname as ForeignField,
    fn.nspname as ForeignSchema,
    case c.confupdtype
        when 'a' then 'NO ACTION'
        when 'r' then 'RESTRICT'
        when 'c' then 'CASCADE'
        when 'n' then 'SET NULL'
        when 'd' then 'SET DEFAULT'
    end as UpdateRule,
    case c.confdeltype
        when 'a' then 'NO ACTION'
        when 'r' then 'RESTRICT'
        when 'c' then 'CASCADE'
        when 'n' then 'SET NULL'
        when 'd' then 'SET DEFAULT'
    end as DeleteRule,
    k.position as FieldPosition
from pg_catalog.pg_constraint c
join pg_catalog.pg_class t on t.oid = c.conrelid
join pg_catalog.pg_namespace n on n.oid = t.relnamespace
left join lateral unnest(c.conkey, c.confkey) with ordinality as k(local_key, foreign_key, position) on true
left join pg_catalog.pg_attribute a on a.attrelid = t.oid and a.attnum = k.local_key
left join pg_catalog.pg_class ft on ft.oid = c.confrelid
left join pg_catalog.pg_namespace fn on fn.oid = ft.relnamespace
left join pg_catalog.pg_attribute fa on fa.attrelid = ft.oid and fa.attnum = k.foreign_key
where c.contype in ('p', 'f', 'u', 'c')
    and n.nspname not in ('pg_catalog', 'information_schema')
    and exists (
        select 1 from information_schema.tables it
        where it.table_schema = n.nspname and it.table_name = t.relname
            and it.table_type = 'BASE TABLE'
    )
order by n.nspname, t.relname, c.conname, k.position
