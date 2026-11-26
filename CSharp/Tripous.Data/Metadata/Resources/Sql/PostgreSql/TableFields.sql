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
    coalesce(c.generation_expression, '')                as Expression,
    case when c.is_identity = 'YES' or (
        c.column_default like 'nextval(%'
        and pg_catalog.pg_get_serial_sequence(
            pg_catalog.format('%I.%I', c.table_schema, c.table_name), c.column_name
        ) is not null
    ) then 1 else 0 end                                   as IsIdentity,
    case when c.is_generated = 'ALWAYS' then 1 else 0 end as IsComputed,
    c.ordinal_position                                   as OrdinalPosition
from 
    information_schema.columns c
      inner join information_schema.tables  t 
            on  c.table_schema = t.table_schema 
            and c.table_name = t.table_name
where 
    t.table_type = 'BASE TABLE'
    and t.table_schema not in ('pg_catalog', 'information_schema')
order by 
    t.table_schema, 
    t.table_name, 
    c.ordinal_position
