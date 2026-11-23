// Usage: node scripts/extract-ghost-schema.cjs /absolute/path/to/Ghost/schema.js
const schema = require(require('node:path').resolve(process.argv[2]));
const tables = Object.entries(schema).map(([name, specification]) => {
    const columns = [];
    const primaryKey = [];
    for (const [column, spec] of Object.entries(specification)) {
        if (column === '@@INDEXES@@' || column === '@@UNIQUE_CONSTRAINTS@@') continue;
        const declaration = {
            string: `varchar(${spec.maxlength ?? 191})`,
            boolean: 'tinyint(1)',
            integer: spec.unsigned ? 'int unsigned' : 'int',
            bigInteger: spec.unsigned ? 'bigint unsigned' : 'bigint',
            dateTime: 'datetime',
            text: spec.fieldtype === 'long' ? 'longtext' : 'text'
        }[spec.type];
        if (!declaration) throw new Error(`Unknown type ${spec.type} on ${name}.${column}`);
        columns.push({ Name: column, Declaration: declaration, IsNullable: spec.nullable === true });
        if (spec.primary === true) primaryKey.push(column);
    }
    return { Name: name, Columns: columns, PrimaryKey: primaryKey };
});
process.stdout.write(JSON.stringify(tables));
