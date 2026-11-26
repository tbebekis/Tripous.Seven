select
    c.table_schema                                       as SchemaName,
    c.table_name                                         as TableName,
    c.column_name                                        as FieldName,
    c.udt_name                                           as DataType,
    ''                                                   as DataSubType,
    case c.is_nullable when 'YES' then 1 else 0 end      as IsNullable, 
    coalesce(c.character_maximum_length, 0)              as SizeInChars, 
    coalesce(c.character_octet_length, 0)                as SizeInBytes,
    coalesce(c.numeric_precision, 0)                     as DecimalPrecision, 
    coalesce(c.numeric_scale, 0)                         as DecimalScale, 
    coalesce(c.column_default, '')                       as DefaultValue, 
    ''                                                   as Expression,
    c.ordinal_position                                   as OrdinalPosition
from 
    information_schema.columns c
      inner join information_schema.views  t 
            on  c.table_schema = t.table_schema 
            and c.table_name = t.table_name
where 
    t.table_schema not in ('pg_catalog', 'information_schema')

union all

select
    n.nspname as SchemaName,
    v.relname as TableName,
    a.attname as FieldName,
    typ.typname as DataType,
    '' as DataSubType,
    case when a.attnotnull then 0 else 1 end as IsNullable,
    case when typ.typname in ('varchar', 'bpchar') and a.atttypmod >= 4
        then a.atttypmod - 4 else 0 end as SizeInChars,
    0 as SizeInBytes,
    0 as DecimalPrecision,
    0 as DecimalScale,
    '' as DefaultValue,
    '' as Expression,
    a.attnum as OrdinalPosition
from pg_catalog.pg_class v
join pg_catalog.pg_namespace n on n.oid = v.relnamespace
join pg_catalog.pg_attribute a on a.attrelid = v.oid
join pg_catalog.pg_type typ on typ.oid = a.atttypid
where v.relkind = 'm'
    and a.attnum > 0 and not a.attisdropped
    and n.nspname not in ('pg_catalog', 'information_schema')
    and (pg_catalog.pg_has_role(v.relowner, 'USAGE')
        or pg_catalog.has_table_privilege(v.oid, 'SELECT')
        or pg_catalog.has_column_privilege(v.oid, a.attnum, 'SELECT'))

order by SchemaName, TableName, OrdinalPosition
