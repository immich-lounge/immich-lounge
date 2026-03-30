const fs = require('fs');
const path = require('path');

const testsDir = path.resolve(__dirname, '..', 'tests');
const files = fs.readdirSync(testsDir)
    .filter((file) => file.endsWith('.spec.bs'))
    .sort();

for (const file of files) {
    const text = fs.readFileSync(path.join(testsDir, file), 'utf8');
    for (const line of text.split(/\r?\n/)) {
        const suite = line.match(/@suite\("([^"]+)"\)/);
        if (suite) {
            console.log(`SUITE ${suite[1]} [${file}]`);
            continue;
        }

        const describe = line.match(/@describe\("([^"]+)"\)/);
        if (describe) {
            console.log(`  DESCRIBE ${describe[1]}`);
            continue;
        }

        const test = line.match(/@it\("([^"]+)"\)/);
        if (test) {
            console.log(`    IT ${test[1]}`);
        }
    }
}
