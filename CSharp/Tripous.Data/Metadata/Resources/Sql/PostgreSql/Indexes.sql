select
    n.nspname as SchemaName,
    t.relname as TableName,
    i.relname as IndexName,
    a.attname as FieldName,
    k.position as FieldPosition,
    am.amname as IndexType,
    ix.indisunique as IsUnique,
    pg_catalog.pg_get_indexdef(i.oid) as Definition
from pg_catalog.pg_index ix
join pg_catalog.pg_class i on i.oid = ix.indexrelid
join pg_catalog.pg_class t on t.oid = ix.indrelid
join pg_catalog.pg_namespace n on n.oid = t.relnamespace
join pg_catalog.pg_am am on am.oid = i.relam
cross join lateral unnest(ix.indkey) with ordinality as k(attnum, position)
left join pg_catalog.pg_attribute a on a.attrelid = t.oid and a.attnum = k.attnum
where k.position <= ix.indnkeyatts
    and n.nspname not in ('pg_catalog', 'information_schema')
    and exists (
        select 1 from information_schema.tables it
        where it.table_schema = n.nspname and it.table_name = t.relname
            and it.table_type = 'BASE TABLE'
    )
order by n.nspname, t.relname, i.relname, k.position
