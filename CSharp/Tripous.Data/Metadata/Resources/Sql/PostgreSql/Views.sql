select
    v.table_schema as SchemaName,
    v.table_name as TableName,
    v.view_definition as Definition
from information_schema.views v
where v.table_schema not in ('pg_catalog', 'information_schema')

union all

select
    n.nspname as SchemaName,
    v.relname as TableName,
    case when pg_catalog.pg_has_role(v.relowner, 'USAGE')
        then pg_catalog.pg_get_viewdef(v.oid) end as Definition
from pg_catalog.pg_class v
join pg_catalog.pg_namespace n on n.oid = v.relnamespace
where v.relkind = 'm'
    and n.nspname not in ('pg_catalog', 'information_schema')
    and (pg_catalog.pg_has_role(v.relowner, 'USAGE')
        or pg_catalog.has_table_privilege(v.oid, 'SELECT')
        or pg_catalog.has_any_column_privilege(v.oid, 'SELECT'))

order by SchemaName, TableName
