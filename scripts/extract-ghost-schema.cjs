// Usage: node scripts/extract-ghost-schema.cjs /absolute/path/to/Ghost/schema.{js,ts}
const schemaPath = require('node:path').resolve(process.argv[2]);
const isTypeScript = schemaPath.endsWith('.ts');
if (isTypeScript) {
    const schemaUrl = require('node:url').pathToFileURL(schemaPath).href;
    // Node strips TypeScript types but needs an explicit extension on this import.
    require('node:module').registerHooks({
        resolve(specifier, context, nextResolve) {
            if (context.parentURL === schemaUrl && specifier === './lib/sortable') {
                return nextResolve('./lib/sortable.ts', context);
            }
            return nextResolve(specifier, context);
        }
    });
}
const schemaModule = require(schemaPath);
const schema = isTypeScript ? schemaModule.default : schemaModule;
const tables = Object.entries(schema).map(([name, specification]) => {
    const columns = [];
    const primaryKey = [];
    for (const [column, spec] of Object.entries(specification)) {
        if (column === '@@PRIMARY_KEY@@') {
            primaryKey.push(...spec);
            continue;
        }
        if (column.startsWith('@@')) continue;
        const declaration = {
            string: `varchar(${spec.maxlength ?? 191})`,
            boolean: 'tinyint(1)',
            integer: spec.unsigned ? 'int unsigned' : 'int',
            bigInteger: spec.unsigned ? 'bigint unsigned' : 'bigint',
            dateTime: 'datetime',
            binary: spec.maxlength ? `varbinary(${spec.maxlength})` : 'blob',
            text: spec.fieldtype === 'long' ? 'longtext' : 'text'
        }[spec.type];
        if (!declaration) throw new Error(`Unknown type ${spec.type} on ${name}.${column}`);
        columns.push({ Name: column, Declaration: declaration, IsNullable: spec.nullable === true });
        if (spec.primary === true) primaryKey.push(column);
    }
    return { Name: name, Columns: columns, PrimaryKey: primaryKey };
});
process.stdout.write(JSON.stringify(tables));
